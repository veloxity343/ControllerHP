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
        // Every other Bluetooth device with a battery Windows records: mice, keyboards, headsets.
        var bluetooth = new BluetoothSource();
        // Logitech mice, keyboards and headsets, on their USB receivers or cables.
        var logitech = new LogitechSource();
        IBatterySource[] sources = [xbox, playStation, bluetooth, logitech];

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
            Preview.WriteSettingsSheet(args[1]);
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

        TrimLogs();
        Application.ThreadException += (_, e) => Log($"Unhandled UI exception: {e.Exception}");
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Log($"Unhandled exception: {e.ExceptionObject}");

        ApplicationConfiguration.Initialize();
        Application.Run(new TrayApp(sources));
        return 0;
    }

    internal const string ErrorLogFile = "error.log";
    internal const string HistoryLogFile = "history.log";

    internal static readonly string LogDirectory = Path.Combine(
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

    public static readonly TimeSpan HistoryLogKept = TimeSpan.FromDays(30);
    public const long ErrorLogLimit = 1024 * 1024;

    /// <summary>
    /// Keeps the logs from growing forever: history.log to its last 30 days (battery.csv has the long-term
    /// record), error.log to its newest megabyte. Run at startup.
    /// </summary>
    internal static void TrimLogs()
    {
        TrimByAge(Path.Combine(LogDirectory, HistoryLogFile), DateTime.Now - HistoryLogKept);
        TrimBySize(Path.Combine(LogDirectory, ErrorLogFile), ErrorLogLimit);
    }

    /// <summary>Drops lines dated before <paramref name="cutoff"/>; each line starts with its timestamp.</summary>
    internal static void TrimByAge(string path, DateTime cutoff)
    {
        try
        {
            if (!File.Exists(path))
                return;
            var lines = File.ReadAllLines(path);
            int first = Array.FindIndex(lines, line => LineTime(line) is DateTime t && t >= cutoff);
            if (first == 0)
                return;
            File.WriteAllLines(path, first < 0 ? [] : lines[first..]);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    /// <summary>
    /// Keeps roughly the newest <paramref name="limit"/> bytes, starting at a whole entry: error entries can
    /// run over several lines (stack traces), so the cut moves forward to the next line with a timestamp.
    /// </summary>
    internal static void TrimBySize(string path, long limit)
    {
        try
        {
            var info = new FileInfo(path);
            if (!info.Exists || info.Length <= limit)
                return;
            string tail;
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                stream.Seek(-limit, SeekOrigin.End);
                tail = new StreamReader(stream).ReadToEnd();
            }
            var lines = tail.Split('\n');
            int first = Array.FindIndex(lines, 1, line => LineTime(line) is not null); // line 0 is likely cut mid-way
            File.WriteAllText(path, first < 0 ? "" : string.Join('\n', lines[first..]));
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private static DateTime? LineTime(string line)
    {
        int space = line.IndexOf(' ');
        return space > 0 && DateTime.TryParse(line[..space], System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.RoundtripKind, out var t) ? t : null;
    }

    private static readonly HashSet<string> LoggedOnce = [];

    /// <summary>Logs a recurring problem the first time it appears, rather than on every 30-second poll.</summary>
    internal static void LogOnce(string message)
    {
        lock (LoggedOnce)
        {
            if (!LoggedOnce.Add(message))
                return;
        }
        Log(message);
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
