using System.Runtime.InteropServices;
using ControllerHP.Battery;
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
        IBatterySource[] sources = [xbox, playStation];

        if (args.Contains("--list"))
            return ListControllers([.. xbox.Sources, playStation]);

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

        if (args.Contains("--demo"))
        {
            ApplicationConfiguration.Initialize();
            Preview.RunToastDemo();
            return 0;
        }

        using var singleInstance = new Mutex(initiallyOwned: true, @"Local\ControllerHP.SingleInstance", out bool isFirst);
        if (!isFirst)
            return 0;

        Application.ThreadException += (_, e) => Log($"Unhandled UI exception: {e.Exception}");
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Log($"Unhandled exception: {e.ExceptionObject}");

        ApplicationConfiguration.Initialize();
        Application.Run(new TrayApp(sources));
        return 0;
    }

    internal const string ErrorLogFile = "error.log";
    internal const string HistoryLogFile = "history.log";

    private static readonly string LogDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ControllerHP");

    /// <summary>Appends to a log in %LOCALAPPDATA%\ControllerHP, since a tray app has nowhere else to report.</summary>
    internal static void Log(string message, string fileName = ErrorLogFile)
    {
        try
        {
            Directory.CreateDirectory(LogDirectory);
            File.AppendAllText(Path.Combine(LogDirectory, fileName), $"{DateTime.Now:O} {message}{Environment.NewLine}");
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
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

        foreach (var source in sources)
        {
            Console.WriteLine($"[{source.Name}]");
            try
            {
                var readings = source.Read();
                if (readings.Count == 0)
                    Console.WriteLine("  (no controllers)");
                foreach (var r in readings)
                    Console.WriteLine($"  {r.Describe()}  [hw={r.Hardware ?? "?"}, level={r.Level}, power={r.Power}, id={r.Id}]");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  error: {ex.Message}");
            }
        }
        Console.WriteLine("[Raw Windows.Gaming.Input reports]");
        foreach (var line in GamingInputSource.DescribeRawReports())
            Console.WriteLine($"  {line}");
        Console.WriteLine("[Raw Bluetooth LE battery properties]");
        foreach (var line in BluetoothLeBattery.DescribeRawProperties())
            Console.WriteLine($"  {line}");
        Console.WriteLine("[Connected Bluetooth LE batteries]");
        foreach (var b in BluetoothLeBattery.QueryConnected())
        {
            Console.WriteLine($"  {b.Name}: {b.Percent}%  [hw={b.VendorId:X4}:{b.ProductId:X4}, address={b.Address}]");
            try
            {
                Console.WriteLine($"    live GATT read: {BluetoothLeBattery.ReadLive(b.Address)}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"    live GATT read error: {ex.Message}");
            }
        }
        return 0;
    }
}
