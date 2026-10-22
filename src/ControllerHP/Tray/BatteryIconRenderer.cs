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
/// Draws the device's shape with a battery bar beside it, in a colour that tracks the charge: along the
/// bottom for wide devices like controllers, up the right for tall ones like mice. Devices without a shape
/// of their own get a Bluetooth sign. A bolt over the bottom-right corner marks charging.
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
        Draw(g, battery, lightBackground ? Color.FromArgb(32, 32, 32) : Color.White, pixelArt: size == 16);
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

    // Layout on the 16x16 grid. Wide devices sit above a thin bar along the bottom, tall ones beside a thin
    // bar up the right. At 16 px the bar is 2 pixels, so it stays crisp; larger sizes draw it 1.5 units.
    private sealed record Layout(RectangleF Device, RectangleF Bar, RectangleF PixelBar, bool Upright);

    private static readonly Layout Wide = new(
        Device: new(0f, 1f, 16f, 12f), Bar: new(1f, 14.5f, 14f, 1.5f), PixelBar: new(1f, 14f, 14f, 2f), Upright: false);

    private static readonly Layout Tall = new(
        Device: new(0f, 0f, 13f, 16f), Bar: new(14.5f, 1f, 1.5f, 14f), PixelBar: new(14f, 1f, 2f, 14f), Upright: true);

    /// <summary>The bolt or "?" sits over the bottom-right corner, across the bar and the device.</summary>
    private static readonly RectangleF Badge = new(11f, 9f, 5f, 7f);

    private static Layout LayoutFor(DeviceKind kind) => kind is DeviceKind.Mouse or DeviceKind.Other ? Tall : Wide;

    /// <param name="pixelArt">At 16 px, draw from hand-placed pixels, which stay crisp where a smoothed shape
    /// would blur.</param>
    private static void Draw(Graphics g, ControllerBattery battery, Color foreground, bool pixelArt)
    {
        int? percent = battery.EffectivePercent;
        bool wired = battery.Power == ChargeState.Wired;
        bool charging = battery.Power == ChargeState.Charging;
        string? badge = wired || charging ? "bolt" : percent is null ? "?" : null;
        var layout = LayoutFor(battery.Kind);

        if (pixelArt && Pixels.TryGetValue(battery.Kind, out var pixels))
            DrawPixels(g, pixels, layout.Device, foreground);
        else
            DrawGlyph(g, battery.Kind, layout.Device, foreground);

        var bar = pixelArt ? layout.PixelBar : layout.Bar;
        using (var track = new SolidBrush(Color.FromArgb(70, foreground)))
            g.FillRectangle(track, bar);
        if (wired)
        {
            // Powered by the cable, with no battery: a full bar in the device's own colour.
            using var full = new SolidBrush(foreground);
            g.FillRectangle(full, bar);
        }
        else if (percent is int p)
        {
            // Whole pixels at 16 px, so the end of the fill is sharp; a sliver stays near 0% so "empty"
            // still reads as red.
            float length = (layout.Upright ? bar.Height : bar.Width) * Math.Clamp(p, 0, 100) / 100f;
            length = Math.Max(1f, pixelArt ? MathF.Round(length) : length);
            var fill = layout.Upright
                ? new RectangleF(bar.X, bar.Bottom - length, bar.Width, length)
                : new RectangleF(bar.X, bar.Y, length, bar.Height);
            using var brush = new SolidBrush(FillColor(p));
            g.FillRectangle(brush, fill);
        }

        if (badge == "bolt")
        {
            if (pixelArt)
                DrawPixels(g, BoltPixels, Badge, Color.White, Halo);
            else
                DrawBolt(g, Badge);
        }
        else if (badge == "?")
        {
            var outline = foreground.GetBrightness() > 0.5f ? Halo : Color.FromArgb(220, 255, 255, 255);
            if (pixelArt)
                DrawPixels(g, QuestionPixels, Badge, foreground, outline);
            else
                DrawBadge(g, "?", Badge, foreground, outline);
        }
    }

    /// <summary>The smooth shape, enlarged to fill <paramref name="area"/>.</summary>
    private static void DrawGlyph(Graphics g, DeviceKind kind, RectangleF area, Color foreground)
    {
        using var glyph = Glyph(kind);
        // The strokes aren't in the path: the Bluetooth sign is all strokes, and the headset's band arches
        // above its ear cups.
        var bounds = kind switch
        {
            DeviceKind.Other => new RectangleF(5.6f, 4.1f, 4.4f, 7.8f),
            DeviceKind.Headset => new RectangleF(3.6f, 3.9f, 8.8f, 7.7f),
            _ => glyph.GetBounds(),
        };
        float scale = Math.Min((area.Width - 1f) / bounds.Width, (area.Height - 1f) / bounds.Height);
        var state = g.Save();
        g.TranslateTransform(area.X + area.Width / 2, area.Y + area.Height / 2);
        g.ScaleTransform(scale, scale);
        g.TranslateTransform(-(bounds.X + bounds.Width / 2), -(bounds.Y + bounds.Height / 2));
        using (var brush = new SolidBrush(foreground))
            g.FillPath(brush, glyph);
        if (kind is DeviceKind.Headset or DeviceKind.Other)
            DrawStrokes(g, kind, foreground);
        g.Restore(state);
    }

    // Each device drawn pixel by pixel for 16 px: X is filled. They're centred in their layout's space.
    private static readonly Dictionary<DeviceKind, string[]> Pixels = new()
    {
        [DeviceKind.Gamepad] =
        [
            "...XXXXXXXX...",
            ".XXXXXXXXXXXX.",
            ".XX.XXXXXX.XX.",
            "XX...XXXX.X.XX",
            "XXX.XXXXXX.XXX",
            "XXXXXXXXXXXXXX",
            "XXXXXXXXXXXXXX",
            "XXXXX....XXXXX",
            "XXXX......XXXX",
            ".XX........XX.",
        ],
        [DeviceKind.Mouse] =
        [
            "..XX.XX..",
            ".XXX.XXX.",
            "XXXX.XXXX",
            "XXXX.XXXX",
            "XXXX.XXXX",
            "XXXXXXXXX",
            "XXXXXXXXX",
            "XXXXXXXXX",
            "XXXXXXXXX",
            "XXXXXXXXX",
            "XXXXXXXXX",
            ".XXXXXXX.",
            "..XXXXX..",
        ],
        [DeviceKind.Keyboard] =
        [
            ".XXXXXXXXXXX.",
            "XX.X.X.X.X.XX",
            "XXXXXXXXXXXXX",
            "XX.X.X.X.X.XX",
            "XXXXXXXXXXXXX",
            "XXX.......XXX",
            "XXXXXXXXXXXXX",
            ".XXXXXXXXXXX.",
        ],
        [DeviceKind.Headset] =
        [
            "....XXXXXX....",
            "..XX......XX..",
            ".X..........X.",
            ".X..........X.",
            "X............X",
            "X............X",
            "XXX........XXX",
            "XXX........XXX",
            "XXX........XXX",
            "XXX........XXX",
            "XXX........XXX",
            ".XX........XX.",
        ],
        [DeviceKind.Other] =
        [
            "....X....",
            "....XX...",
            "....X.X..",
            ".X..X..X.",
            "..X.X.X..",
            "...XXX...",
            "....X....",
            "...XXX...",
            "..X.X.X..",
            ".X..X..X.",
            "....X.X..",
            "....XX...",
            "....X....",
        ],
    };

    private static readonly string[] BoltPixels =
    [
        "...XX",
        "..XX.",
        ".XX..",
        "XXXXX",
        "..XX.",
        ".XX..",
        "XX...",
    ];

    private static readonly string[] QuestionPixels =
    [
        ".XXX.",
        "X...X",
        "....X",
        "..XX.",
        "..X..",
        ".....",
        "..X..",
    ];

    /// <summary>
    /// Draws <paramref name="rows"/> centred in <paramref name="area"/>, on whole pixels. With an
    /// <paramref name="outline"/>, the pixels around the shape are filled with it first, so it stands
    /// apart from whatever is under it.
    /// </summary>
    private static void DrawPixels(Graphics g, string[] rows, RectangleF area, Color color, Color? outline = null)
    {
        int width = rows[0].Length, height = rows.Length;
        int left = (int)(area.X + (area.Width - width) / 2), top = (int)(area.Y + (area.Height - height) / 2);
        bool Filled(int x, int y) => y >= 0 && y < height && x >= 0 && x < width && rows[y][x] == 'X';

        if (outline is Color around)
        {
            using var halo = new SolidBrush(around);
            for (int y = -1; y <= height; y++)
                for (int x = -1; x <= width; x++)
                    if (!Filled(x, y) && (Filled(x - 1, y) || Filled(x + 1, y) || Filled(x, y - 1) || Filled(x, y + 1)))
                        g.FillRectangle(halo, left + x, top + y, 1, 1);
        }
        using var brush = new SolidBrush(color);
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                if (Filled(x, y))
                    g.FillRectangle(brush, left + x, top + y, 1, 1);
    }

    /// <summary>
    /// The device's smooth silhouette, for sizes above 16 px. Inner shapes are separate figures, so with the default
    /// alternate fill they're cut out of it.
    /// </summary>
    private static GraphicsPath Glyph(DeviceKind kind)
    {
        var path = new GraphicsPath(FillMode.Alternate);
        switch (kind)
        {
            case DeviceKind.Gamepad:
            {
                const float top = 5.0f, bottom = 11.1f, waist = 8.8f;
                path.AddLine(6.0f, top, 10.0f, top);
                path.AddBezier(10.0f, top, 11.8f, top, 12.6f, 6.1f, 12.9f, 7.8f);
                path.AddBezier(12.9f, 7.8f, 13.3f, 9.7f, 13.0f, bottom, 11.8f, bottom);
                path.AddBezier(11.8f, bottom, 10.9f, bottom, 10.4f, 9.9f, 9.7f, waist);
                path.AddLine(9.7f, waist, 6.3f, waist);
                path.AddBezier(6.3f, waist, 5.6f, 9.9f, 5.1f, bottom, 4.2f, bottom);
                path.AddBezier(4.2f, bottom, 3.0f, bottom, 2.7f, 9.7f, 3.1f, 7.8f);
                path.AddBezier(3.1f, 7.8f, 3.4f, 6.1f, 4.2f, top, 6.0f, top);
                path.CloseFigure();
                // The d-pad and a button.
                path.AddRectangle(new RectangleF(4.3f, 6.5f, 2.4f, 0.9f));
                path.AddRectangle(new RectangleF(5.05f, 5.75f, 0.9f, 0.75f));
                path.AddRectangle(new RectangleF(5.05f, 7.4f, 0.9f, 0.75f));
                path.AddEllipse(9.4f, 6.2f, 1.6f, 1.6f);
                break;
            }
            case DeviceKind.Mouse:
                path.AddPath(RoundedRect(new RectangleF(5.4f, 3.9f, 5.2f, 8.2f), 2.6f), false);
                // The split between the buttons.
                path.AddRectangle(new RectangleF(7.6f, 4.7f, 0.8f, 2.3f));
                break;
            case DeviceKind.Keyboard:
                path.AddPath(RoundedRect(new RectangleF(3.4f, 5.3f, 9.2f, 5.4f), 1f), false);
                foreach (float x in new[] { 4.4f, 6.3f, 8.2f, 10.1f })
                    path.AddRectangle(new RectangleF(x, 6.3f, 1.5f, 1.2f));
                path.AddRectangle(new RectangleF(5.4f, 8.5f, 5.2f, 1.1f));
                break;
            case DeviceKind.Headset:
                // The ear cups; the band is drawn as a stroke.
                path.AddPath(RoundedRect(new RectangleF(3.6f, 8.0f, 2.4f, 3.6f), 0.9f), false);
                path.AddPath(RoundedRect(new RectangleF(10.0f, 8.0f, 2.4f, 3.6f), 0.9f), false);
                break;
        }
        return path;
    }

    /// <summary>The parts of a shape that are lines rather than solid: the headset's band, the Bluetooth sign.</summary>
    private static void DrawStrokes(Graphics g, DeviceKind kind, Color foreground)
    {
        using var pen = new Pen(foreground, 1.1f) { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round };
        if (kind == DeviceKind.Headset)
            g.DrawArc(pen, 4.3f, 4.4f, 7.4f, 7.4f, 180, 180);
        else
            g.DrawLines(pen, new PointF[] { new(5.6f, 6.2f), new(10f, 9.9f), new(8f, 11.9f), new(8f, 4.1f), new(10f, 6.1f), new(5.6f, 9.8f) });
    }

    /// <summary>A bolt filling <paramref name="area"/>, haloed so it stands out over the device and the bar.</summary>
    private static void DrawBolt(Graphics g, RectangleF area)
    {
        float x = area.X, y = area.Y, w = area.Width, h = area.Height;
        PointF[] bolt =
        [
            new(x + w * 0.75f, y), new(x, y + h * 0.58f), new(x + w * 0.45f, y + h * 0.58f),
            new(x + w * 0.25f, y + h), new(x + w, y + h * 0.42f), new(x + w * 0.55f, y + h * 0.42f),
        ];
        using (var halo = new Pen(Halo, 1.3f) { LineJoin = LineJoin.Round })
            g.DrawPolygon(halo, bolt);
        g.FillPolygon(Brushes.White, bolt);
    }

    /// <summary>A small bold mark filling <paramref name="area"/>, such as "?" while the level isn't known yet.</summary>
    private static void DrawBadge(Graphics g, string text, RectangleF area, Color color, Color outline)
    {
        using var family = new FontFamily("Segoe UI");
        using var path = new GraphicsPath();
        path.AddString(text, family, (int)FontStyle.Bold, 10f, PointF.Empty, StringFormat.GenericTypographic);

        var bounds = path.GetBounds();
        float scale = Math.Min(area.Width / bounds.Width, area.Height / bounds.Height);
        using var matrix = new Matrix();
        matrix.Translate(area.X + area.Width / 2, area.Y + area.Height / 2);
        matrix.Scale(scale, scale);
        matrix.Translate(-(bounds.X + bounds.Width / 2), -(bounds.Y + bounds.Height / 2));
        path.Transform(matrix);

        using (var halo = new Pen(outline, 1.4f) { LineJoin = LineJoin.Round })
            g.DrawPath(halo, path);
        using var brush = new SolidBrush(color);
        g.FillPath(brush, path);
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
