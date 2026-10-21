using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using ControllerHP.Battery;

namespace ControllerHP.Tray;

/// <summary>An <see cref="Icon"/> that owns its native HICON, which Icon.FromHandle does not free.</summary>
internal sealed partial class OwnedIcon : IDisposable
{
    private IntPtr _handle;

    public Icon Icon { get; }

    public OwnedIcon(IntPtr handle)
    {
        _handle = handle;
        Icon = Icon.FromHandle(handle);
    }

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool DestroyIcon(IntPtr handle);

    public void Dispose()
    {
        Icon.Dispose();
        if (_handle != IntPtr.Zero)
        {
            DestroyIcon(_handle);
            _handle = IntPtr.Zero;
        }
    }
}

/// <summary>
/// Draws the percentage above a slim horizontal battery whose fill width and colour track the charge
/// continuously. A bolt through the battery marks charging.
/// </summary>
internal static class BatteryIconRenderer
{
    // Colour stops for the fill; values between stops are blended so the colour shifts smoothly.
    private static readonly (int Percent, Color Color)[] ColorStops =
    [
        (0, Color.FromArgb(229, 57, 53)),    // red
        (20, Color.FromArgb(229, 57, 53)),   // red
        (50, Color.FromArgb(250, 204, 21)),  // yellow
        (75, Color.FromArgb(52, 199, 89)),   // green
        (100, Color.FromArgb(52, 199, 89)),  // green
    ];

    private static readonly Color Halo = Color.FromArgb(200, 0, 0, 0);

    /// <param name="battery">null draws the app icon, the "no controllers" placeholder.</param>
    /// <param name="lightTaskbar">Whether the taskbar uses the light theme, which needs a dark outline.</param>
    public static OwnedIcon Render(ControllerBattery? battery, bool lightTaskbar)
    {
        using var bitmap = RenderBitmap(battery, lightTaskbar, SystemInformation.SmallIconSize.Width);
        return new OwnedIcon(bitmap.GetHicon());
    }

    /// <param name="battery">null draws the app icon, the "no controllers" placeholder.</param>
    public static Bitmap RenderBitmap(ControllerBattery? battery, bool lightBackground, int size)
    {
        if (battery is null)
            return AppIcon(size);

        var bitmap = new Bitmap(size, size);
        using var g = Graphics.FromImage(bitmap);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.Clear(Color.Transparent);
        // Everything below is laid out on a 16x16 grid and scaled to the real size.
        g.ScaleTransform(size / 16f, size / 16f);
        Draw(g, battery, lightBackground ? Color.FromArgb(32, 32, 32) : Color.White);
        return bitmap;
    }

    /// <summary>
    /// The app icon at <paramref name="size"/>, from its nearest hand-tuned frame. It's outlined, so it
    /// reads on light and dark taskbars alike.
    /// </summary>
    private static Bitmap AppIcon(int size)
    {
        using var stream = typeof(BatteryIconRenderer).Assembly.GetManifestResourceStream("ControllerHP.app.ico")!;
        using var icon = new Icon(stream, new Size(size, size));
        using var frame = icon.ToBitmap();
        if (frame.Width == size)
            return new Bitmap(frame);

        var bitmap = new Bitmap(size, size);
        using var g = Graphics.FromImage(bitmap);
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.DrawImage(frame, new Rectangle(0, 0, size, size));
        return bitmap;
    }

    public static Color FillColor(int percent)
    {
        percent = Math.Clamp(percent, 0, 100);
        for (int i = 1; i < ColorStops.Length; i++)
        {
            var (hiPercent, hi) = ColorStops[i];
            if (percent > hiPercent)
                continue;
            var (loPercent, lo) = ColorStops[i - 1];
            float t = hiPercent == loPercent ? 0 : (percent - loPercent) / (float)(hiPercent - loPercent);
            return Color.FromArgb(
                (int)(lo.R + (hi.R - lo.R) * t),
                (int)(lo.G + (hi.G - lo.G) * t),
                (int)(lo.B + (hi.B - lo.B) * t));
        }
        return ColorStops[^1].Color;
    }

    // Layout on the 16x16 grid: the percentage across the top, a slim battery meter along the bottom,
    // so the number never hides the fill.
    private static readonly RectangleF TextArea = new(0f, 0f, 16f, 8.6f);
    /// <summary>Digits stop growing at this height, so short numbers like "85" don't fill the whole area.</summary>
    private const float MaxTextHeight = 6.3f;
    private static readonly RectangleF Body = new(0.6f, 9.7f, 13.6f, 5.7f);
    private static readonly RectangleF Inner = new(1.9f, 11f, 11f, 3.1f);
    private static readonly RectangleF Nub = new(14.6f, 11.3f, 1.2f, 2.5f);

    private static void Draw(Graphics g, ControllerBattery battery, Color foreground)
    {

        int? percent = battery.EffectivePercent;
        bool wired = battery.Power == ChargeState.Wired;
        bool charging = battery.Power == ChargeState.Charging;

        if (percent is int p && !wired)
        {
            using var fill = new SolidBrush(FillColor(p));
            // Keep a sliver visible near 0% so "empty" still reads as red.
            float width = Math.Max(1.2f, Inner.Width * p / 100f);
            using var fillPath = RoundedRect(new RectangleF(Inner.X, Inner.Y, width, Inner.Height), 0.8f);
            g.FillPath(fill, fillPath);
        }

        using (var pen = new Pen(foreground, 1.1f))
        using (var bodyPath = RoundedRect(Body, 1.6f))
            g.DrawPath(pen, bodyPath);
        using (var nub = new SolidBrush(foreground))
        using (var nubPath = RoundedRect(Nub, 0.5f))
            g.FillPath(nub, nubPath);

        string label = wired ? "USB" : percent is int value ? value.ToString() : "?";
        DrawText(g, label, foreground);

        if (wired || charging)
            DrawBolt(g);
    }

    /// <summary>Bold text scaled to fill the area above the battery ("100" shrinks more than "7").</summary>
    private static void DrawText(Graphics g, string text, Color color)
    {
        using var family = new FontFamily("Segoe UI");
        using var path = new GraphicsPath();
        path.AddString(text, family, (int)FontStyle.Bold, 10f, PointF.Empty, StringFormat.GenericTypographic);

        var bounds = path.GetBounds();
        float scale = Math.Min(TextArea.Width / bounds.Width, Math.Min(TextArea.Height, MaxTextHeight) / bounds.Height);
        using var matrix = new Matrix();
        matrix.Translate(TextArea.X + TextArea.Width / 2, TextArea.Y + TextArea.Height / 2);
        matrix.Scale(scale, scale);
        matrix.Translate(-(bounds.X + bounds.Width / 2), -(bounds.Y + bounds.Height / 2));
        path.Transform(matrix);

        using var brush = new SolidBrush(color);
        g.FillPath(brush, path);
    }

    /// <summary>A bolt through the battery, haloed so it stands out over the fill.</summary>
    private static void DrawBolt(Graphics g)
    {
        PointF[] bolt =
        [
            new(8.9f, 8.2f), new(4.9f, 13.1f), new(7.5f, 13.1f),
            new(6.6f, 16.6f), new(10.6f, 11.6f), new(8.0f, 11.6f),
        ];
        using (var halo = new Pen(Halo, 1.3f) { LineJoin = LineJoin.Round })
            g.DrawPolygon(halo, bolt);
        g.FillPolygon(Brushes.White, bolt);
    }

    private static GraphicsPath RoundedRect(RectangleF r, float radius)
    {
        float d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
        var path = new GraphicsPath();
        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}
