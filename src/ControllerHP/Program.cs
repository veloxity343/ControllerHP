using System.Runtime.InteropServices;
using ControllerHP.Battery;
using ControllerHP.Battery.Vendor;
using ControllerHP.Tray;

namespace ControllerHP;

internal static partial class Program
{
    [STAThread]
    private static int Main(string[] args)
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
        IBatterySource[] sources = [xbox, playStation, bluetooth, logitech, vendors];

        if (args.Contains("--list"))
            return ListControllers(sources);

        if (args.Contains("--hid-probe"))
        {
            AttachConsole(-1);
            HidProbe.Dump(0x045E, 0x0B13, TimeSpan.FromSeconds(5));
            foreach (ushort productId in PlayStationSource.ProductIds)
                HidProbe.Dump(PlayStationSource.SonyVendorId, productId, TimeSpan.FromSeconds(5));
            foreach (var b in BluetoothLeBattery.QueryConnected())
                foreach (var line in BluetoothLeBattery.DescribeGatt(b.Address))
                    Console.WriteLine($"  {line}");
            return 0;
        }

        if (args.Length == 2 && args[0] == "--preview")
        {
            Preview.WriteSheet(args[1]);
            return 0;
        }

        if (args.Length == 2 && args[0] == "--preview-icons")
        {
            Preview.WriteIconGrid(args[1]);
            return 0;
        }

        if (args.Length == 2 && args[0] == "--preview-settings")
        {
            ApplicationConfiguration.Initialize();
            // A failure ends it with an error code, for CI to catch, instead of an error dialog nobody clicks.
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
            try
            {
                Preview.WriteSettingsSheet(args[1]);
            }
            catch (Exception e)
            {
                AttachConsole(-1);
                Console.Error.WriteLine(e);
                return 1;
            }
            return 0;
        }

        if (args.Contains("--demo"))
        {
            ApplicationConfiguration.Initialize();
            Preview.RunToastDemo();
            return 0;
        }

        using var singleInstance = new Mutex(initiallyOwned: true, @"Local\ControllerHP.SingleInstance", out bool isFirst);
        if (!isFirst)
            return 0;

        Log.Trim();
        Application.ThreadException += (_, e) => Log.Write($"Unhandled UI exception: {e.Exception}");
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Log.Write($"Unhandled exception: {e.ExceptionObject}");

        ApplicationConfiguration.Initialize();
        Application.Run(new TrayApp(sources));
        return 0;
    }

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool AttachConsole(int processId);

    /// <summary>Prints what every source sees, for diagnosing which APIs work with a given controller.</summary>
    private static int ListControllers(IEnumerable<IBatterySource> sources)
    {
        AttachConsole(-1); // ATTACH_PARENT_PROCESS, since this is a WinExe.

        // Windows.Gaming.Input reports already-connected pads asynchronously after startup.
        Thread.Sleep(TimeSpan.FromSeconds(1.5));
        Diagnostics.WriteSources(Console.Out, sources);
        return 0;
    }
}
