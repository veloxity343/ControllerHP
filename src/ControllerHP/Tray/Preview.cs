using System.Drawing.Drawing2D;
using ControllerHP.Battery;

namespace ControllerHP.Tray;

/// <summary>Design aids: a PNG sheet of every icon state, and a live run of sample toasts.</summary>
internal static partial class Preview
{
    private static ControllerBattery Sample(int? percent, ChargeState power = ChargeState.Discharging) =>
        new("preview", "Xbox Wireless Controller", "preview", percent,
            percent is int p ? ControllerBattery.LevelFromPercent(p) : BatteryLevel.Unknown, power);

    private static readonly (string Label, ControllerBattery? Battery)[] States =
    [
        ("100%", Sample(100)), ("90%", Sample(90)), ("75%", Sample(75)), ("62%", Sample(62)),
        ("50%", Sample(50)), ("35%", Sample(35)), ("20%", Sample(20)), ("10%", Sample(10)),
        ("4%", Sample(4)), ("charging 60%", Sample(60, ChargeState.Charging)),
        ("wired", Sample(null, ChargeState.Wired)), ("unknown", Sample(null, ChargeState.Unknown)),
        ("none", null),
        ("mouse 62%", Sample(62) with { Kind = DeviceKind.Mouse }),
        ("keyboard 35%", Sample(35) with { Kind = DeviceKind.Keyboard }),
        ("headset 80%", Sample(80, ChargeState.Charging) with { Kind = DeviceKind.Headset }),
        ("other 15%", Sample(15) with { Kind = DeviceKind.Other }),
    ];

    /// <summary>A two-player evening: one pad running low on Bluetooth, one charging on USB.</summary>
    private static readonly ControllerBattery[] SampleFlyout =
    [
        Sample(20) with { Name = "Xbox Wireless Controller", Connection = Connection.Bluetooth },
        new("preview2", "DualSense Wireless Controller", "preview", 85, BatteryLevel.Full, ChargeState.Charging,
            Connection: Connection.Usb),
    ];

    /// <summary>The charging DualSense's estimate; the Xbox pad is already at its low level, so it has none.</summary>
    private static readonly Dictionary<string, string> SampleTimeLeft = new() { ["preview2"] = "1 h 30 min to full" };

    private static readonly ToastContent[] SampleToasts =
    [
        new("Xbox Wireless Controller", "Connected · 87%", Sample(87), Color.FromArgb(52, 199, 89)),
        new("Xbox Wireless Controller", "Battery low · 20%", Sample(20), BatteryIconRenderer.FillColor(20)),
        new("Xbox Wireless Controller", "Battery critical · 10%", Sample(10), BatteryIconRenderer.FillColor(10)),
        new("Xbox Wireless Controller", "Disconnected", null, Color.FromArgb(120, 120, 120)),
    ];

    public static void WriteSheet(string path)
    {
        int[] sizes = [16, 20, 24, 32];
        const int zoom = 3, labelHeight = 18, rowHeader = 70;
        int cell = sizes.Max() * zoom + 12;
        int RowHeight(int size) => size + size * zoom + 18;
        int iconsHeight = labelHeight + sizes.Sum(size => 2 * RowHeight(size));
        int toastWidth = ToastWindow.LogicalWidth, toastHeight = ToastWindow.LogicalHeight;
        int width = rowHeader + States.Length * cell;
        var flyoutSize = Flyout.LogicalSize(SampleFlyout.Length);
        int height = iconsHeight + SampleToasts.Length * (toastHeight + 10) + 30 + flyoutSize.Height + 20;

        // No alpha channel: GDI text doesn't write alpha, so on a 32-bit sheet the text would be see-through.
        using var sheet = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
        using var g = Graphics.FromImage(sheet);
        g.Clear(Color.FromArgb(245, 245, 245));
        g.InterpolationMode = InterpolationMode.NearestNeighbor;
        g.PixelOffsetMode = PixelOffsetMode.Half;
        using var font = new Font("Segoe UI", 11f, GraphicsUnit.Pixel);

        for (int i = 0; i < States.Length; i++)
            g.DrawString(States[i].Label, font, Brushes.Black, rowHeader + i * cell + 4, 2);

        int y = labelHeight;
        foreach (int size in sizes)
        {
            foreach (bool light in new[] { false, true })
            {
                int rowHeight = RowHeight(size);
                using (var bg = new SolidBrush(light ? Color.FromArgb(238, 238, 238) : Color.FromArgb(28, 28, 28)))
                    g.FillRectangle(bg, 0, y, width, rowHeight);
                g.DrawString($"{size}px\n{(light ? "light" : "dark")}", font, light ? Brushes.Black : Brushes.White, 4, y + 4);

                for (int i = 0; i < States.Length; i++)
                {
                    using var icon = BatteryIconRenderer.RenderBitmap(States[i].Battery, light, size);
                    int x = rowHeader + i * cell + 4;
                    g.DrawImage(icon, x, y + 6);                                                // actual size
                    g.DrawImage(icon, new Rectangle(x, y + size + 12, size * zoom, size * zoom)); // magnified
                }
                y += rowHeight;
            }
        }

        y += 10;
        foreach (var toast in SampleToasts)
        {
            ToastWindow.PaintContent(g, new Rectangle(rowHeader, y, toastWidth, toastHeight), toast, 1f);
            y += toastHeight + 10;
        }

        // The flyout in both modes, side by side.
        y += 10;
        Flyout.PaintContent(g, new Rectangle(rowHeader, y, flyoutSize.Width, flyoutSize.Height), SampleFlyout,
            Palette.Dark, 1f, timeLeft: SampleTimeLeft);
        Flyout.PaintContent(g, new Rectangle(rowHeader + flyoutSize.Width + 20, y, flyoutSize.Width, flyoutSize.Height),
            SampleFlyout, Palette.LightMode, 1f, timeLeft: SampleTimeLeft);

        sheet.Save(path, System.Drawing.Imaging.ImageFormat.Png);
    }

    private static readonly (string Label, DeviceKind Kind)[] GridDevices =
    [
        ("Controller", DeviceKind.Gamepad), ("Mouse", DeviceKind.Mouse), ("Keyboard", DeviceKind.Keyboard),
        ("Headset", DeviceKind.Headset), ("Other Bluetooth", DeviceKind.Other),
    ];

    private static readonly (string Label, int? Percent, ChargeState Power)[] GridStates =
    [
        ("100%", 100, ChargeState.Discharging), ("75%", 75, ChargeState.Discharging),
        ("50%", 50, ChargeState.Discharging), ("30%", 30, ChargeState.Discharging),
        ("15%", 15, ChargeState.Discharging), ("Charging", 60, ChargeState.Charging),
        ("Wired", null, ChargeState.Wired), ("Unknown", null, ChargeState.Unknown),
    ];

    /// <summary>
    /// Every device in every state, on a dark and a light taskbar: each icon large, with its real 16 px
    /// tray size beneath it.
    /// </summary>
    public static void WriteIconGrid(string path)
    {
        const int large = 64, small = 16, cellWidth = 96, labelWidth = 130, top = 36, headerHeight = 28;
        const int rowHeight = large + small + 26, sectionGap = 12;
        int sectionHeight = top + headerHeight + GridDevices.Length * rowHeight + 8;
        int width = labelWidth + GridStates.Length * cellWidth + 12;

        using var sheet = new Bitmap(width, 2 * sectionHeight + sectionGap, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
        using var g = Graphics.FromImage(sheet);
        g.Clear(Color.White);
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
        using var title = new Font("Segoe UI Semibold", 15f, GraphicsUnit.Pixel);
        using var label = new Font("Segoe UI", 14f, GraphicsUnit.Pixel);
        using var header = new Font("Segoe UI", 12f, GraphicsUnit.Pixel);
        var centred = new StringFormat { Alignment = StringAlignment.Center };

        int y = 0;
        foreach (bool light in new[] { false, true })
        {
            var background = light ? Color.FromArgb(243, 243, 243) : Color.FromArgb(32, 32, 32);
            var text = light ? Color.FromArgb(26, 26, 26) : Color.White;
            var dim = light ? Color.FromArgb(110, 110, 110) : Color.FromArgb(160, 160, 160);
            using (var fill = new SolidBrush(background))
                g.FillRectangle(fill, 0, y, width, sectionHeight);
            using var textBrush = new SolidBrush(text);
            using var dimBrush = new SolidBrush(dim);

            g.DrawString(light ? "Light taskbar" : "Dark taskbar", title, textBrush, 14, y + 10);
            for (int i = 0; i < GridStates.Length; i++)
                g.DrawString(GridStates[i].Label, header, dimBrush,
                    new RectangleF(labelWidth + i * cellWidth, y + top, cellWidth, headerHeight), centred);

            int rowY = y + top + headerHeight;
            foreach (var (name, kind) in GridDevices)
            {
                g.DrawString(name, label, textBrush, 14, rowY + large / 2 - 9);
                for (int i = 0; i < GridStates.Length; i++)
                {
                    var (_, percent, power) = GridStates[i];
                    var battery = new ControllerBattery("preview", name, "preview", percent,
                        percent is int p ? ControllerBattery.LevelFromPercent(p) : BatteryLevel.Unknown, power, Kind: kind);
                    int x = labelWidth + i * cellWidth;
                    using (var icon = BatteryIconRenderer.RenderBitmap(battery, light, large))
                        g.DrawImage(icon, x + (cellWidth - large) / 2, rowY);
                    using (var icon = BatteryIconRenderer.RenderBitmap(battery, light, small))
                        g.DrawImage(icon, x + (cellWidth - small) / 2, rowY + large + 8);
                }
                rowY += rowHeight;
            }
            y += sectionHeight + sectionGap;
        }

        sheet.Save(path, System.Drawing.Imaging.ImageFormat.Png);
    }

    /// <summary>Renders each page of the settings window, one row per page, in dark and light mode side by side.</summary>
    public static void WriteSettingsSheet(string path)
    {
        var shots = new List<Bitmap>();
        int pages;
        using (var probe = new SettingsWindow(Palette.Dark))
            pages = probe.PageCount;
        foreach (var (page, palette) in Enumerable.Range(0, pages).SelectMany(p => new[] { (p, Palette.Dark), (p, Palette.LightMode) }))
        {
            // Shown off-screen so its child controls exist; PrintWindow captures it there, title bar included.
            using var window = new SettingsWindow(palette, SampleHistory) { StartPosition = FormStartPosition.Manual, Location = new Point(-20000, 0) };
            window.ShowPage(page);
            window.Show();
            // Tall enough to show the whole page without scrolling.
            window.Height = window.LogicalToDeviceUnits(1300);
            // Paint it and its children now, then let the compositor pick that up: PrintWindow copies the
            // composed window, and capturing a brand-new window too early comes out black.
            window.Refresh();
            Application.DoEvents();
            DwmFlush();
            DwmFlush();
            var shot = new Bitmap(window.Width, window.Height);
            using (var g = Graphics.FromImage(shot))
            {
                IntPtr hdc = g.GetHdc();
                PrintWindow(window.Handle, hdc, PwRenderFullContent);
                g.ReleaseHdc(hdc);
            }
            shots.Add(shot);
            window.Close();
        }

        int width = shots[0].Width, height = shots[0].Height;
        using var sheet = new Bitmap(width * 2 + 20, (height + 20) * pages - 20,
            System.Drawing.Imaging.PixelFormat.Format24bppRgb);
        using (var g = Graphics.FromImage(sheet))
        {
            g.Clear(Color.FromArgb(128, 128, 128));
            for (int i = 0; i < shots.Count; i++)
                g.DrawImage(shots[i], i % 2 * (width + 20), i / 2 * (height + 20));
        }
        sheet.Save(path, System.Drawing.Imaging.ImageFormat.Png);
        foreach (var shot in shots)
            shot.Dispose();
    }

    /// <summary>
    /// A day of made-up history: an Xbox pad draining in 1% steps with a disconnected stretch, and a
    /// DualSense dropping in 10% steps, then charging.
    /// </summary>
    private static List<HistorySeries> SampleHistory(DateTime since)
    {
        var now = DateTime.Now;
        var xbox = new List<HistoryPoint>();
        for (int i = 0; i <= 60; i++)
            xbox.Add(new(now.AddHours(-22 + i * 0.25), 100 - i, false));
        xbox.Add(new(now.AddHours(-6.75), null, false));
        xbox.Add(new(now.AddHours(-4), 40, false));
        for (int i = 1; i <= 16; i++)
            xbox.Add(new(now.AddHours(-4 + i * 0.25), 40 - i, false));

        var dualSense = new List<HistoryPoint>();
        for (int i = 0; i <= 8; i++)
            dualSense.Add(new(now.AddHours(-18 + i * 1.5), 85 - i * 10, false));
        for (int i = 1; i <= 7; i++)
            dualSense.Add(new(now.AddHours(-6 + i * 0.6), 5 + i * 10, true));

        return
        [
            new("xbox", "Xbox Wireless Controller", 0, [.. xbox.Where(p => p.Time >= since)]),
            new("dualsense", "DualSense Wireless Controller", 1, [.. dualSense.Where(p => p.Time >= since)]),
        ];
    }

    private const uint PwRenderFullContent = 2;

    /// <summary>Waits for the compositor's next frame.</summary>
    [System.Runtime.InteropServices.LibraryImport("dwmapi.dll")]
    private static partial int DwmFlush();

    [System.Runtime.InteropServices.LibraryImport("user32.dll")]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static partial bool PrintWindow(IntPtr hwnd, IntPtr hdc, uint flags);

    /// <summary>Shows each sample toast for real, one after another, then exits.</summary>
    public static void RunToastDemo()
    {
        var toasts = new ToastManager();
        var queue = new Queue<ToastContent>(SampleToasts);
        var timer = new System.Windows.Forms.Timer { Interval = 1200 };
        int ticksAfterLast = 0;
        timer.Tick += (_, _) =>
        {
            if (queue.TryDequeue(out var next))
                toasts.Show(next);
            else if (++ticksAfterLast > 5)
                Application.ExitThread();
        };
        toasts.Show(queue.Dequeue());
        timer.Start();
        Application.Run();
        timer.Dispose();
        toasts.Dispose();
    }
}
