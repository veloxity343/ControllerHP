using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using ControllerHP.Battery;
using ControllerHP.Ui;

namespace ControllerHP;

/// <summary>Design aids: the flyout and menu rendered off screen, and every icon state as separate PNGs.</summary>
internal static class Preview
{
    private static ControllerBattery Sample(int? percent, ChargeState power = ChargeState.Discharging) =>
        new("preview", "Xbox Wireless Controller", "preview", percent,
            percent is int p ? ControllerBattery.LevelFromPercent(p) : BatteryLevel.Unknown, power);

    /// <summary>A two-player evening: one pad running low on Bluetooth, one charging on USB.</summary>
    private static readonly ControllerBattery[] SampleFlyout =
    [
        Sample(20) with { Name = "Xbox Wireless Controller", Connection = Connection.Bluetooth },
        new("preview2", "DualSense Wireless Controller", "preview", 85, BatteryLevel.Full, ChargeState.Charging,
            Connection: Connection.Usb),
    ];

    private static readonly Dictionary<string, string> SampleTimeLeft = new() { ["preview2"] = "1 h 30 min to full" };

    private static readonly DeviceKind[] Kinds = [DeviceKind.Gamepad, DeviceKind.Mouse, DeviceKind.Keyboard, DeviceKind.Headset, DeviceKind.Other];

    private static readonly (string Label, int? Percent, ChargeState Power)[] States =
    [
        ("100", 100, ChargeState.Discharging), ("75", 75, ChargeState.Discharging), ("50", 50, ChargeState.Discharging),
        ("30", 30, ChargeState.Discharging), ("15", 15, ChargeState.Discharging), ("4", 4, ChargeState.Discharging),
        ("charging", 60, ChargeState.Charging), ("wired", null, ChargeState.Wired), ("unknown", null, ChargeState.Unknown),
    ];

    /// <summary>Every device in every state at 16, 32 and 64 px on dark and light, one PNG each, for comparing renderers.</summary>
    public static void DumpIcons(string folder)
    {
        Directory.CreateDirectory(folder);
        foreach (var kind in Kinds)
            foreach (var (label, percent, power) in States)
                foreach (int size in new[] { 16, 20, 24, 32, 64 })
                    foreach (bool light in new[] { false, true })
                    {
                        var battery = Sample(percent, power) with { Kind = kind };
                        using var icon = BatteryIcon.Render(battery, light, size);
                        icon.Save(Path.Combine(folder, $"{kind}-{label}-{size}-{(light ? "light" : "dark")}.png"), new PngBitmapEncoderOptions());
                    }
        App.Exit();
    }

    /// <summary>The flyout and menu, dark and light side by side.</summary>
    public static void WriteTraySheet(string path)
    {
        const int gap = 20;
        double flyoutHeight = FlyoutContent.HeightFor(SampleFlyout.Length);
        var menuEntries = new List<MenuEntry>
        {
            new("Xbox Wireless Controller: 20%", Enabled: false), new("DualSense Wireless Controller: 85%, charging", Enabled: false),
            MenuEntry.Separator, new("Refresh now"), new("Tray icons", Submenu: [new("1")]), new("Pop-up position", Submenu: [new("Top right")]),
            new("Connection pop-ups", Checked: true), new("Battery warnings", Checked: true), new("Start with Windows", Checked: false),
            MenuEntry.Separator, new("Settings", Enabled: false), new("Diagnostics"), new("Exit"),
        };
        var dark = new MenuPanel(menuEntries, Palette.Dark);
        double menuWidth = dark.MenuWidth, menuHeight = dark.MenuHeight;
        int width = (int)(2 * (320 + gap + menuWidth) + gap * 2), height = (int)(Math.Max(flyoutHeight, menuHeight) + 2 * gap);

        using var sheet = new RenderTargetBitmap(new PixelSize(width, height), new Vector(96, 96));
        using (var g = sheet.CreateDrawingContext())
            g.FillRectangle(Brushes.Gray, new Rect(0, 0, width, height));
        double x = gap;
        foreach (var palette in new[] { Palette.Dark, Palette.LightMode })
        {
            var flyout = new FlyoutContent(palette) { Readings = SampleFlyout, TimeLeft = SampleTimeLeft };
            x = Place(sheet, flyout, x, gap, 320, flyoutHeight) + gap;
            var menu = new MenuPanel(menuEntries, palette);
            menu.Highlight(3);
            x = Place(sheet, menu, x, gap, menuWidth, menuHeight) + gap;
        }
        sheet.Save(path, new PngBitmapEncoderOptions());
        App.Exit();
    }

    /// <summary>Lays out a control at its size and renders it into the sheet at (x, y); returns its right edge.</summary>
    private static double Place(RenderTargetBitmap sheet, Avalonia.Controls.Control control, double x, double y, double width, double height)
    {
        using var part = new RenderTargetBitmap(new PixelSize((int)width, (int)height), new Vector(96, 96));
        control.Measure(new Size(width, height));
        control.Arrange(new Rect(0, 0, width, height));
        part.Render(control);
        using (var g = sheet.CreateDrawingContext(false))
            g.DrawImage(part, new Rect(x, y, width, height));
        return x + width;
    }
}
