using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Threading;
using ControllerHP.Battery;
using ControllerHP.Battery.Vendor;

namespace ControllerHP;

internal static partial class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Contains("--list"))
            return ListDevices(CreateSources());

        // Design aids, rendered off screen: they need Avalonia running, but no tray or sources.
        if (args.Length == 2 && args[0] is "--preview-tray" or "--dump-icons")
        {
            App.Startup = () => (args[0] == "--preview-tray" ? (Action<string>)Preview.WriteTraySheet : Preview.DumpIcons)(args[1]);
            return Run(args);
        }

        if (args.Contains("--demo"))
        {
            App.Startup = Preview.RunToastDemo;
            return Run(args);
        }

        // One copy at a time, shared with the WinForms app it replaces, so the two never read devices at once.
        using var singleInstance = new Mutex(initiallyOwned: true, @"Local\ControllerHP.SingleInstance", out bool isFirst);
        if (!isFirst)
            return 0;

        Log.Trim();
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Log.Write($"Unhandled exception: {e.ExceptionObject}");
        var sources = CreateSources();
        App.Startup = () =>
        {
            Dispatcher.UIThread.UnhandledException += (_, e) =>
            {
                Log.Write($"Unhandled UI exception: {e.Exception}");
                e.Handled = true;
            };
            var tray = new TrayController(sources, App.Exit);
            App.Exiting += tray.Dispose;
        };
        return Run(args);
    }

    private static int Run(string[] args)
    {
        AppBuilder.Configure<App>().UsePlatformDetect().StartWithClassicDesktopLifetime(args, Avalonia.Controls.ShutdownMode.OnExplicitShutdown);
        return 0;
    }

    private static IBatterySource[] CreateSources()
    {
        // The Xbox sources see the same controllers, so only the best one that finds any is used.
        var xbox = new FirstAvailableSource("Xbox", new GamingInputSource(), new XInputSource());
        var playStation = new PlayStationSource();
        // Every other Bluetooth device with a battery Windows records: mice, keyboards, headsets.
        var bluetooth = new BluetoothSource();
        // Logitech mice, keyboards and headsets, on their USB receivers or cables.
        var logitech = new LogitechSource();
        // Other makers' devices on their own receivers, each through its maker's protocol.
        var vendors = new VendorHidSource(VendorProtocols.All);
        return [xbox, playStation, bluetooth, logitech, vendors];
    }

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool AttachConsole(int processId);

    /// <summary>Prints what every source sees, for diagnosing which APIs work with a given device.</summary>
    private static int ListDevices(IEnumerable<IBatterySource> sources)
    {
        AttachConsole(-1); // ATTACH_PARENT_PROCESS, since this is a WinExe.
        // Windows.Gaming.Input reports already-connected pads asynchronously after startup.
        Thread.Sleep(TimeSpan.FromSeconds(1.5));
        Diagnostics.WriteSources(Console.Out, sources);
        return 0;
    }
}
