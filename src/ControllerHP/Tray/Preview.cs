using System.Drawing.Drawing2D;
using ControllerHP.Battery;

namespace ControllerHP.Tray;

/// <summary>Design aids: a PNG sheet of every icon state, and a live run of sample toasts.</summary>
internal static class Preview
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
    ];

    /// <summary>A two-player evening: one pad running low on Bluetooth, one charging on USB.</summary>
    private static readonly ControllerBattery[] SampleFlyout =
    [
        Sample(20) with { Name = "Xbox Wireless Controller", Connection = Connection.Bluetooth },
        new("preview2", "DualSense Wireless Controller", "preview", 85, BatteryLevel.Full, ChargeState.Charging,
            Connection: Connection.Usb),
    ];

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
            Palette.Dark, 1f);
        Flyout.PaintContent(g, new Rectangle(rowHeader + flyoutSize.Width + 20, y, flyoutSize.Width, flyoutSize.Height),
            SampleFlyout, Palette.LightMode, 1f);

        sheet.Save(path, System.Drawing.Imaging.ImageFormat.Png);
    }

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
