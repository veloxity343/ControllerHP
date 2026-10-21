using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using ControllerHP.Battery;

namespace ControllerHP.Tray;

/// <summary>
/// The panel left-clicking the tray icon opens: every connected controller with its battery, charge
/// state and connection, including ones the tray icon limit hides. Sits above the taskbar like
/// Windows' own flyouts and closes when it loses focus.
/// </summary>
internal sealed partial class Flyout : Form
{
    private const int LogicalWidth = 320, HeaderHeight = 44, RowHeight = 56, BottomPadding = 8, ScreenMargin = 12;
    private const int WsExToolWindow = 0x00000080;

    // Opens by fading in while sliding out from the taskbar, like Windows 11's flyouts; closes with a quick fade.
    private static readonly TimeSpan OpenDuration = TimeSpan.FromMilliseconds(180);
    private static readonly TimeSpan CloseDuration = TimeSpan.FromMilliseconds(100);
    private const int SlideDistance = 16;

    private readonly Palette _palette;
    private readonly System.Windows.Forms.Timer _animation = new() { Interval = 10 };
    private readonly System.Diagnostics.Stopwatch _clock = new();
    private IReadOnlyList<ControllerBattery> _readings = [];
    private Point _anchor;
    /// <summary>Where it rests once open, and the direction it slides from (towards the taskbar).</summary>
    private Point _restingLocation;
    private Size _slideFrom;
    private bool _closing;

    public Flyout(IReadOnlyList<ControllerBattery> readings, Palette palette)
    {
        _palette = palette;
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = false;
        TopMost = true;
        KeyPreview = true;
        DoubleBuffered = true;
        BackColor = palette.Background;

        // Creating the handle first gives the real monitor DPI for sizing.
        _ = Handle;
        Theme.StyleWindow(Handle, Theme.Corners.Round, palette.Border);
        SetReadings(readings);
        _animation.Tick += (_, _) => Animate();
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= WsExToolWindow; // keeps it out of Alt+Tab
            return cp;
        }
    }

    private int Scale(int logicalPixels) => (int)Math.Round(logicalPixels * DeviceDpi / 96f);

    /// <summary>Size at 96 DPI for this many controllers.</summary>
    public static Size LogicalSize(int controllers) =>
        new(LogicalWidth, HeaderHeight + Math.Max(1, controllers) * RowHeight + BottomPadding);

    public void SetReadings(IReadOnlyList<ControllerBattery> readings)
    {
        _readings = readings;
        var size = LogicalSize(readings.Count);
        Size = new Size(Scale(size.Width), Scale(size.Height));
        if (Visible)
            Reposition();
        Invalidate();
    }

    /// <summary>Opens beside the taskbar on the screen with <paramref name="anchor"/> (the click point).</summary>
    public void ShowAt(Point anchor)
    {
        _anchor = anchor;
        Reposition();
        if (AnimationsEnabled())
        {
            Opacity = 0;
            Location = _restingLocation + _slideFrom;
            _clock.Restart();
            _animation.Start();
        }
        Show();
        Activate();
        SetForegroundWindow(Handle);
        InstallClickHook();
    }

    // Closing on Deactivate isn't enough: when the tray icon is clicked rapidly, Windows can refuse the
    // flyout focus, and a window that never had focus is never deactivated. So while open, watch for
    // any mouse click outside it.
    private const int WhMouseLowLevel = 14;
    private const int WmLButtonDown = 0x0201, WmRButtonDown = 0x0204, WmMButtonDown = 0x0207, WmXButtonDown = 0x020B;

    /// <summary>The open flyout the hook reports to; only one is open at a time.</summary>
    private static Flyout? s_hooked;
    private IntPtr _hook;

    [LibraryImport("user32.dll", EntryPoint = "SetWindowsHookExW", SetLastError = true)]
    private static unsafe partial IntPtr SetWindowsHookEx(int hookId,
        delegate* unmanaged<int, IntPtr, IntPtr, IntPtr> callback, IntPtr module, uint threadId);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool UnhookWindowsHookEx(IntPtr hook);

    [LibraryImport("user32.dll")]
    private static partial IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr wParam, IntPtr lParam);

    [LibraryImport("kernel32.dll", EntryPoint = "GetModuleHandleW", StringMarshalling = StringMarshalling.Utf16)]
    private static partial IntPtr GetModuleHandle(string? moduleName);

    private unsafe void InstallClickHook()
    {
        s_hooked = this;
        _hook = SetWindowsHookEx(WhMouseLowLevel, &OnMouse, GetModuleHandle(null), 0);
    }

    private void RemoveClickHook()
    {
        if (_hook != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_hook);
            _hook = IntPtr.Zero;
        }
        if (s_hooked == this)
            s_hooked = null;
    }

    /// <summary>Runs on the UI thread, which installed the hook; keep it quick and defer the work.</summary>
    [UnmanagedCallersOnly]
    private static IntPtr OnMouse(int code, IntPtr wParam, IntPtr lParam)
    {
        if (code >= 0 && s_hooked is { } flyout &&
            (int)wParam is WmLButtonDown or WmRButtonDown or WmMButtonDown or WmXButtonDown)
        {
            // MSLLHOOKSTRUCT starts with the cursor position in physical pixels, as Bounds is here.
            var point = new Point(Marshal.ReadInt32(lParam), Marshal.ReadInt32(lParam, 4));
            if (!flyout.Bounds.Contains(point))
                flyout.BeginInvoke(flyout.Dismiss);
        }
        return CallNextHookEx(IntPtr.Zero, code, wParam, lParam);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        RemoveClickHook();
        base.OnFormClosed(e);
    }

    /// <summary>Fades out, then closes. Safe to call more than once.</summary>
    public void Dismiss()
    {
        if (_closing)
            return;
        _closing = true;
        if (!AnimationsEnabled())
        {
            Close();
            return;
        }
        _clock.Restart();
        _animation.Start();
    }

    private void Animate()
    {
        double elapsed = _clock.Elapsed.TotalMilliseconds;
        if (_closing)
        {
            double t = Math.Min(1, elapsed / CloseDuration.TotalMilliseconds);
            Opacity = 1 - t;
            if (t >= 1)
            {
                _animation.Stop();
                Close();
            }
            return;
        }

        double progress = Math.Min(1, elapsed / OpenDuration.TotalMilliseconds);
        double eased = 1 - Math.Pow(1 - progress, 3); // ease-out cubic
        Opacity = eased;
        Location = _restingLocation + new Size(
            (int)Math.Round(_slideFrom.Width * (1 - eased)), (int)Math.Round(_slideFrom.Height * (1 - eased)));
        if (progress >= 1)
            _animation.Stop();
    }

    private const uint SpiGetClientAreaAnimation = 0x1042;

    [LibraryImport("user32.dll", EntryPoint = "SystemParametersInfoW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SystemParametersInfo(uint action, uint param, out int value, uint winIni);

    /// <summary>Windows' "Animation effects" setting (Accessibility → Visual effects).</summary>
    private static bool AnimationsEnabled() =>
        !SystemParametersInfo(SpiGetClientAreaAnimation, 0, out int enabled, 0) || enabled != 0;

    /// <summary>Against the taskbar's edge, as close to the click as fits on screen.</summary>
    private void Reposition()
    {
        int slide = Scale(SlideDistance);
        var screen = Screen.FromPoint(_anchor);
        var area = screen.WorkingArea;
        int margin = Scale(ScreenMargin);
        int x = Math.Clamp(_anchor.X - Width / 2, area.Left + margin, area.Right - Width - margin);
        int y = Math.Clamp(_anchor.Y - Height / 2, area.Top + margin, area.Bottom - Height - margin);

        // The taskbar sits where the working area falls short of the screen.
        if (area.Bottom < screen.Bounds.Bottom)
            (y, _slideFrom) = (area.Bottom - Height - margin, new Size(0, slide));
        else if (area.Top > screen.Bounds.Top)
            (y, _slideFrom) = (area.Top + margin, new Size(0, -slide));
        else if (area.Left > screen.Bounds.Left)
            (x, _slideFrom) = (area.Left + margin, new Size(-slide, 0));
        else if (area.Right < screen.Bounds.Right)
            (x, _slideFrom) = (area.Right - Width - margin, new Size(slide, 0));
        else
            (y, _slideFrom) = (area.Bottom - Height - margin, new Size(0, slide)); // auto-hidden taskbar: assume the bottom

        _restingLocation = new Point(x, y);
        if (!_animation.Enabled)
            Location = _restingLocation;
    }

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetForegroundWindow(IntPtr hwnd);

    protected override void OnDeactivate(EventArgs e)
    {
        base.OnDeactivate(e);
        Dismiss();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyCode == Keys.Escape)
            Dismiss();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _animation.Dispose();
        RemoveClickHook();
        base.Dispose(disposing);
    }

    protected override void OnPaint(PaintEventArgs e) =>
        PaintContent(e.Graphics, ClientRectangle, _readings, _palette, DeviceDpi / 96f);

    /// <summary>Draws the flyout's contents; shared with the --preview sheet.</summary>
    public static void PaintContent(Graphics g, Rectangle bounds, IReadOnlyList<ControllerBattery> readings,
        Palette palette, float scale)
    {
        int S(float v) => (int)Math.Round(v * scale);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using (var background = new SolidBrush(palette.Background))
            g.FillRectangle(background, bounds);
        int width = bounds.Width;
        int left = bounds.X + S(16), right = bounds.Right - S(16);
        var flags = TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis |
            TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding;

        using var headerFont = new Font("Segoe UI Semibold", 14f * scale, GraphicsUnit.Pixel);
        using var nameFont = new Font("Segoe UI Semibold", 13f * scale, GraphicsUnit.Pixel);
        using var detailFont = new Font("Segoe UI", 12f * scale, GraphicsUnit.Pixel);

        TextRenderer.DrawText(g, "Controllers", headerFont,
            new Rectangle(left, bounds.Y, right - left, S(HeaderHeight)), palette.Text, flags);

        int y = bounds.Y + S(HeaderHeight);
        if (readings.Count == 0)
        {
            TextRenderer.DrawText(g, "No controllers connected", detailFont,
                new Rectangle(left, y, right - left, S(RowHeight)), palette.SubtleText, flags);
            return;
        }

        int glyphSize = S(32);
        foreach (var reading in readings)
        {
            // Each controller sits on a rounded card, like the tiles in Windows' quick settings.
            var card = new Rectangle(bounds.X + S(8), y + S(2), width - S(16), S(RowHeight) - S(4));
            using (var brush = new SolidBrush(palette.Surface))
            using (var path = RoundedRect(card, S(6)))
                g.FillPath(brush, path);

            using (var glyph = BatteryIconRenderer.RenderBitmap(reading, palette.Light, glyphSize))
                g.DrawImage(glyph, left, y + (S(RowHeight) - glyphSize) / 2);

            int textX = left + glyphSize + S(12);
            int half = S(RowHeight) / 2;
            TextRenderer.DrawText(g, reading.Name, nameFont,
                new Rectangle(textX, y + S(6), right - textX, half - S(6)), palette.Text, flags);
            TextRenderer.DrawText(g, Details(reading), detailFont,
                new Rectangle(textX, y + half, right - textX, half - S(8)), palette.SubtleText, flags);
            y += S(RowHeight);
        }
    }

    /// <summary>e.g. "85% · Charging · Bluetooth".</summary>
    private static string Details(ControllerBattery reading)
    {
        var parts = new List<string>();
        if (reading.Power == ChargeState.Wired)
            parts.Add("Wired, no battery");
        else if (reading.Percent is int p)
            parts.Add($"{p}%");
        else if (reading.Level != BatteryLevel.Unknown)
            parts.Add($"Battery {reading.Level.ToString().ToLowerInvariant()}");
        else
            parts.Add("Battery unknown");

        if (reading.Power == ChargeState.Charging)
            parts.Add("Charging");

        string? connection = reading.Connection switch
        {
            Connection.Usb => "USB",
            Connection.Bluetooth => "Bluetooth",
            Connection.WirelessAdapter => "Xbox Wireless Adapter",
            _ => null,
        };
        if (connection is not null)
            parts.Add(connection);
        return string.Join(" · ", parts);
    }

    private static GraphicsPath RoundedRect(Rectangle r, int radius)
    {
        int d = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}
