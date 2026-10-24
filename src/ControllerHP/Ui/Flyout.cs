using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using ControllerHP.Battery;

namespace ControllerHP.Ui;

/// <summary>
/// The panel left-clicking the tray icon opens: every connected device with its battery, charge state and
/// connection, including ones the tray icon limit hides. Sits above the taskbar like Windows' own flyouts,
/// sliding out from it as it fades in.
/// </summary>
internal sealed class Flyout : PopupWindow
{
    private const int Width_ = 320, ScreenMargin = 12, SlideDistance = 16;
    private static readonly TimeSpan OpenDuration = TimeSpan.FromMilliseconds(180);

    private readonly FlyoutContent _content;
    private PixelPoint _anchor, _resting;
    private PixelVector _slideFrom;
    private DispatcherTimer? _opening;

    /// <summary>The gear in the header was clicked; the flyout is already closing.</summary>
    public event Action? SettingsRequested;

    public Flyout(IReadOnlyList<ControllerBattery> readings, IReadOnlyDictionary<string, string> timeLeft, Palette palette)
        : base(palette, Appearance.Corners.Round)
    {
        _content = new FlyoutContent(palette);
        _content.GearClicked += () =>
        {
            Dismiss();
            SettingsRequested?.Invoke();
        };
        Content = _content;
        Width = Width_;
        SetReadings(readings, timeLeft);
    }

    public void SetReadings(IReadOnlyList<ControllerBattery> readings, IReadOnlyDictionary<string, string> timeLeft)
    {
        _content.Readings = readings;
        _content.TimeLeft = timeLeft;
        Height = FlyoutContent.HeightFor(readings.Count);
        if (IsVisible && _opening is null)
            Reposition();
        _content.InvalidateVisual();
    }

    /// <summary>Opens beside the taskbar on the screen with <paramref name="anchor"/> (the click point, physical pixels).</summary>
    public void ShowAt(PixelPoint anchor)
    {
        _anchor = anchor;
        Reposition();
        bool animate = Appearance.AnimationsEnabled();
        Position = animate ? _resting + _slideFrom : _resting;
        if (animate)
            Opacity = 0;
        Show();
        if (!animate)
            return;
        var clock = System.Diagnostics.Stopwatch.StartNew();
        _opening = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(10) };
        _opening.Tick += (_, _) =>
        {
            double progress = Math.Min(1, clock.Elapsed.TotalMilliseconds / OpenDuration.TotalMilliseconds);
            double eased = 1 - Math.Pow(1 - progress, 3); // ease-out cubic
            Opacity = eased;
            Position = _resting + new PixelVector((int)Math.Round(_slideFrom.X * (1 - eased)), (int)Math.Round(_slideFrom.Y * (1 - eased)));
            if (progress >= 1)
            {
                _opening.Stop();
                _opening = null;
            }
        };
        _opening.Start();
    }

    public override void Dismiss()
    {
        _opening?.Stop();
        _opening = null;
        base.Dismiss();
    }

    /// <summary>Against the taskbar's edge, as close to the click as fits on screen.</summary>
    private void Reposition()
    {
        var screen = Screens.ScreenFromPoint(_anchor) ?? Screens.Primary;
        if (screen is null)
            return;
        double scale = screen.Scaling;
        int width = (int)Math.Round(Width * scale), height = (int)Math.Round(Height * scale);
        int margin = (int)Math.Round(ScreenMargin * scale), slide = (int)Math.Round(SlideDistance * scale);
        var area = screen.WorkingArea;
        var bounds = screen.Bounds;
        int x = Math.Clamp(_anchor.X - width / 2, area.X + margin, area.Right - width - margin);
        int y = Math.Clamp(_anchor.Y - height / 2, area.Y + margin, area.Bottom - height - margin);

        // The taskbar sits where the working area falls short of the screen.
        if (area.Bottom < bounds.Bottom)
            (y, _slideFrom) = (area.Bottom - height - margin, new PixelVector(0, slide));
        else if (area.Y > bounds.Y)
            (y, _slideFrom) = (area.Y + margin, new PixelVector(0, -slide));
        else if (area.X > bounds.X)
            (x, _slideFrom) = (area.X + margin, new PixelVector(-slide, 0));
        else if (area.Right < bounds.Right)
            (x, _slideFrom) = (area.Right - width - margin, new PixelVector(slide, 0));
        else
            (y, _slideFrom) = (area.Bottom - height - margin, new PixelVector(0, slide)); // auto-hidden taskbar: assume the bottom

        _resting = new PixelPoint(x, y);
        if (_opening is null)
            Position = _resting;
    }
}

/// <summary>The flyout's contents: a header with the settings gear, then a card per device.</summary>
internal sealed class FlyoutContent(Palette palette) : Control
{
    private const int HeaderHeight = 44, RowHeight = 56, BottomPadding = 8;

    public IReadOnlyList<ControllerBattery> Readings { get; set; } = [];
    public IReadOnlyDictionary<string, string> TimeLeft { get; set; } = new Dictionary<string, string>();
    private bool _gearHover;
    /// <summary>Each device's icon, kept while its reading doesn't change.</summary>
    private readonly Dictionary<string, (ControllerBattery Reading, Bitmap Image)> _glyphs = [];

    public event Action? GearClicked;

    public static double HeightFor(int devices) => HeaderHeight + Math.Max(1, devices) * RowHeight + BottomPadding;

    private Rect Gear => new(Bounds.Width - 32 - 8, (HeaderHeight - 32) / 2.0, 32, 32);

    public override void Render(DrawingContext g)
    {
        var bounds = new Rect(Bounds.Size);
        g.FillRectangle(Draw.Brush(palette.Background), bounds);
        double left = 16, right = bounds.Width - 16;

        Draw.Text(g, "Devices", Draw.Font(semibold: true), 14, palette.Text, new Rect(left, 0, right - left, HeaderHeight));
        if (_gearHover)
            Draw.RoundedRect(g, palette.Hover, Gear, 4);
        // Segoe MDL2 Assets ships with Windows 10 and 11; E713 is its Settings gear.
        Draw.Text(g, "", new Typeface("Segoe MDL2 Assets"), 15, palette.Text, Gear, TextAlignment.Center);

        double y = HeaderHeight;
        if (Readings.Count == 0)
        {
            Draw.Text(g, "No devices connected", Draw.Font(), 12, palette.SubtleText, new Rect(left, y, right - left, RowHeight));
            return;
        }

        // Icons of devices no longer listed go.
        foreach (var gone in _glyphs.Keys.Where(id => !Readings.Any(r => r.Id == id)).ToList())
        {
            _glyphs[gone].Image.Dispose();
            _glyphs.Remove(gone);
        }

        const int glyphSize = 32;
        double pixelScale = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;
        foreach (var reading in Readings)
        {
            // Each device sits on a rounded card, like the tiles in Windows' quick settings.
            Draw.RoundedRect(g, palette.Surface, new Rect(8, y + 2, bounds.Width - 16, RowHeight - 4), 6);
            g.DrawImage(Glyph(reading, (int)Math.Round(glyphSize * pixelScale)), new Rect(left, y + (RowHeight - glyphSize) / 2.0, glyphSize, glyphSize));

            double textX = left + glyphSize + 12, half = RowHeight / 2.0;
            Draw.Text(g, reading.Name, Draw.Font(semibold: true), 13, palette.Text, new Rect(textX, y + 6, right - textX, half - 6));
            Draw.Text(g, Details(reading, TimeLeft.GetValueOrDefault(reading.Id)), Draw.Font(), 12, palette.SubtleText,
                new Rect(textX, y + half, right - textX, half - 8));
            y += RowHeight;
        }
    }

    /// <summary>The device's icon at its size in physical pixels, so it's as sharp as the screen allows.</summary>
    private IImage Glyph(ControllerBattery reading, int pixels)
    {
        if (_glyphs.TryGetValue(reading.Id, out var kept))
        {
            if (kept.Reading == reading && kept.Image.PixelSize.Width == pixels)
                return kept.Image;
            kept.Image.Dispose();
        }
        var image = BatteryIcon.Render(reading, palette.Light, pixels);
        _glyphs[reading.Id] = (reading, image);
        return image;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        foreach (var (_, image) in _glyphs.Values)
            image.Dispose();
        _glyphs.Clear();
    }

    /// <summary>e.g. "62% · 3 h to 20% · Bluetooth", or "85% · Charging · USB" while time left isn't known.</summary>
    internal static string Details(ControllerBattery reading, string? timeLeft)
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

        // "… to full" already says it's charging, and the line is short on room.
        if (timeLeft is not null)
            parts.Add(timeLeft);
        else if (reading.Power == ChargeState.Charging)
            parts.Add("Charging");

        string? connection = reading.Connection switch
        {
            Connection.Usb => "USB",
            Connection.Bluetooth => "Bluetooth",
            Connection.WirelessAdapter => "Xbox Wireless Adapter",
            Connection.Receiver => "USB receiver",
            _ => null,
        };
        if (connection is not null)
            parts.Add(connection);
        return string.Join(" · ", parts);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        bool hover = Gear.Contains(e.GetPosition(this));
        if (hover != _gearHover)
        {
            _gearHover = hover;
            Cursor = hover ? new Cursor(StandardCursorType.Hand) : Cursor.Default;
            InvalidateVisual();
        }
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _gearHover = false;
        InvalidateVisual();
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (e.InitialPressMouseButton == MouseButton.Left && Gear.Contains(e.GetPosition(this)))
            GearClicked?.Invoke();
    }
}
