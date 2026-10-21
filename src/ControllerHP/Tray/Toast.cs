using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using ControllerHP.Battery;

namespace ControllerHP.Tray;

/// <summary>What a toast shows: the controller's battery glyph, its name, and one line of status.</summary>
/// <param name="Battery">Drawn as the glyph; null draws the dimmed outline used for "disconnected".</param>
/// <param name="Accent">Colour of the thin bar on the left edge.</param>
internal sealed record ToastContent(string Title, string Message, ControllerBattery? Battery, Color Accent);

internal enum ToastCorner { TopRight, TopLeft, BottomRight, BottomLeft }

/// <summary>Stacks toasts in a corner of the primary screen, newest nearest the corner.</summary>
internal sealed class ToastManager : IDisposable
{
    private const int MaxVisible = 4;
    private readonly List<ToastWindow> _open = [];
    private ToastCorner _corner = ToastCorner.TopRight;

    public ToastCorner Corner
    {
        get => _corner;
        set
        {
            _corner = value;
            Layout();
        }
    }

    public void Show(ToastContent content)
    {
        if (_open.Count >= MaxVisible)
            _open[0].Dismiss();

        var toast = new ToastWindow(content);
        toast.FormClosed += (_, _) =>
        {
            _open.Remove(toast);
            toast.Dispose();
            Layout();
        };
        _open.Add(toast);
        Layout();
        toast.Show();
    }

    private void Layout()
    {
        var area = (Screen.PrimaryScreen ?? Screen.AllScreens[0]).WorkingArea;
        bool top = _corner is ToastCorner.TopRight or ToastCorner.TopLeft;
        bool right = _corner is ToastCorner.TopRight or ToastCorner.BottomRight;
        int edge = top ? area.Top : area.Bottom;
        for (int i = _open.Count - 1; i >= 0; i--)
        {
            var toast = _open[i];
            int margin = toast.Scale(12), gap = toast.Scale(8);
            int x = right ? area.Right - toast.Width - margin : area.Left + margin;
            int y = top ? edge + margin : edge - toast.Height - margin;
            toast.Location = new Point(x, y);
            edge = top ? toast.Bottom - margin + gap : toast.Top + margin - gap;
        }
    }

    public void Dispose()
    {
        foreach (var toast in _open.ToList())
            toast.Dispose();
        _open.Clear();
    }
}

/// <summary>
/// A small borderless, topmost, non-activating window: it never takes focus from the game or app
/// underneath. Fades in, waits, fades out; clicking dismisses it early.
/// </summary>
internal sealed partial class ToastWindow : Form
{
    /// <summary>Size at 96 DPI: a single line, half the height of a Steam notification.</summary>
    public const int LogicalWidth = 380, LogicalHeight = 30;

    private static readonly TimeSpan HoldTime = TimeSpan.FromSeconds(4);
    private const double MaxOpacity = 0.96;
    private const double FadeStep = 0.12;

    private const int WsExTopmost = 0x00000008;
    private const int WsExToolWindow = 0x00000080;
    private const int WsExNoActivate = 0x08000000;

    private static readonly Color Background = Color.FromArgb(32, 32, 32);
    private static readonly Color TitleColor = Color.White;
    private static readonly Color MessageColor = Color.FromArgb(190, 190, 190);

    private readonly ToastContent _content;
    private readonly System.Windows.Forms.Timer _animation = new() { Interval = 15 };
    private DateTime _holdUntil;
    private bool _fadingOut;

    public ToastWindow(ToastContent content)
    {
        _content = content;
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = false;
        BackColor = Background;
        DoubleBuffered = true;
        Opacity = 0;
        Cursor = Cursors.Hand;

        // Creating the handle first gives the real monitor DPI for sizing.
        _ = Handle;
        Size = new Size(Scale(LogicalWidth), Scale(LogicalHeight));

        _animation.Tick += (_, _) => Animate();
        Click += (_, _) => Dismiss();
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= WsExTopmost | WsExToolWindow | WsExNoActivate;
            return cp;
        }
    }

    public int Scale(int logicalPixels) => (int)Math.Round(logicalPixels * DeviceDpi / 96f);

    public void Dismiss() => _fadingOut = true;

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        _holdUntil = DateTime.UtcNow + HoldTime;
        _animation.Start();
    }

    private void Animate()
    {
        if (_fadingOut)
        {
            Opacity = Math.Max(0, Opacity - FadeStep / 2);
            if (Opacity <= 0)
            {
                _animation.Stop();
                Close();
            }
        }
        else if (Opacity < MaxOpacity)
        {
            Opacity = Math.Min(MaxOpacity, Opacity + FadeStep);
        }
        else if (DateTime.UtcNow >= _holdUntil)
        {
            _fadingOut = true;
        }
    }

    [LibraryImport("dwmapi.dll")]
    private static partial int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        // Rounded corners on Windows 11 (DWMWA_WINDOW_CORNER_PREFERENCE = DWMWCP_ROUND); ignored on Windows 10.
        int round = 2;
        _ = DwmSetWindowAttribute(Handle, 33, ref round, sizeof(int));
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        PaintContent(e.Graphics, ClientRectangle, _content, DeviceDpi / 96f);
    }

    /// <summary>Draws a toast's contents; shared with the --preview sheet.</summary>
    public static void PaintContent(Graphics g, Rectangle bounds, ToastContent content, float scale)
    {
        int S(float v) => (int)Math.Round(v * scale);

        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
        using (var background = new SolidBrush(Background))
            g.FillRectangle(background, bounds);
        using (var accent = new SolidBrush(content.Accent))
            g.FillRectangle(accent, bounds.X, bounds.Y, S(3), bounds.Height);

        int glyphSize = S(22);
        using (var glyph = BatteryIconRenderer.RenderBitmap(content.Battery, lightBackground: false, glyphSize))
            g.DrawImage(glyph, bounds.X + S(10), bounds.Y + (bounds.Height - glyphSize) / 2);

        // One line: the title, then the message in grey. The title gives way first if space runs out.
        int textX = bounds.X + S(10) + glyphSize + S(9);
        int textRight = bounds.Right - S(12);
        int gap = S(8);
        // Pixel units so the size is the same whether drawing to the window or to a 96-DPI preview bitmap.
        using var titleFont = new Font("Segoe UI Semibold", 12.5f * scale, GraphicsUnit.Pixel);
        using var messageFont = new Font("Segoe UI", 12f * scale, GraphicsUnit.Pixel);
        var flags = TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis |
            TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding;
        // Measured without EndEllipsis, which would measure the text already cut down to fit.
        var measureFlags = TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding;
        int messageWidth = Math.Min(TextRenderer.MeasureText(g, content.Message, messageFont, Size.Empty, measureFlags).Width,
            textRight - textX);
        int titleWidth = Math.Min(TextRenderer.MeasureText(g, content.Title, titleFont, Size.Empty, measureFlags).Width,
            Math.Max(0, textRight - textX - messageWidth - gap));
        TextRenderer.DrawText(g, content.Title, titleFont,
            new Rectangle(textX, bounds.Y, titleWidth, bounds.Height), TitleColor, flags);
        TextRenderer.DrawText(g, content.Message, messageFont,
            new Rectangle(textX + titleWidth + gap, bounds.Y, messageWidth, bounds.Height), MessageColor, flags);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _animation.Dispose();
        base.Dispose(disposing);
    }
}
