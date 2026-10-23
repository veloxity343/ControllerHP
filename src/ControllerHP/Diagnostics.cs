using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using ControllerHP.Battery;
using ControllerHP.Tray;

namespace ControllerHP;

/// <summary>
/// What every battery source sees, for --list, and the fuller report the tray menu's Diagnostics writes
/// for bug reports. Bluetooth addresses in the report are partly masked so it can be posted as it is.
/// </summary>
internal static partial class Diagnostics
{
    public const string ReportFile = "diagnostics.txt";
    private const int LogTailLines = 100;

    /// <summary>Each source on its own, so a grouped one (the Xbox sources) shows what every member sees.</summary>
    public static IEnumerable<IBatterySource> Expand(IEnumerable<IBatterySource> sources) =>
        sources.SelectMany(s => s is FirstAvailableSource group ? group.Sources : [s]);

    /// <summary>What every source reports, raw values included.</summary>
    public static void WriteSources(TextWriter w, IEnumerable<IBatterySource> sources)
    {
        foreach (var source in Expand(sources))
        {
            w.WriteLine($"[{source.Name}]");
            try
            {
                var readings = source.Read();
                if (readings.Count == 0)
                    w.WriteLine("  (no devices)");
                foreach (var r in readings)
                    w.WriteLine($"  {r.Describe()}  [type={r.Kind}, hw={r.Hardware ?? "?"}, level={r.Level}, power={r.Power}, connection={r.Connection}, id={r.Id}]");
            }
            catch (Exception ex)
            {
                w.WriteLine($"  error: {ex.Message}");
            }
        }
        w.WriteLine("[Raw Windows.Gaming.Input reports]");
        foreach (var line in GamingInputSource.DescribeRawReports())
            w.WriteLine($"  {line}");
        w.WriteLine("[Raw Bluetooth LE battery properties]");
        foreach (var line in BluetoothLeBattery.DescribeRawProperties())
            w.WriteLine($"  {line}");
        w.WriteLine("[Raw classic Bluetooth battery properties]");
        foreach (var line in BluetoothLeBattery.DescribeRawProperties(@"BTHENUM\DEV_"))
            w.WriteLine($"  {line}");
        w.WriteLine("[Connected Bluetooth LE batteries]");
        foreach (var b in BluetoothLeBattery.QueryConnected())
        {
            w.WriteLine($"  {b.Name}: {b.Percent}%  [hw={b.VendorId:X4}:{b.ProductId:X4}, address={b.Address}]");
            try
            {
                w.WriteLine($"    live GATT read: {BluetoothLeBattery.ReadLive(b.Address)}");
            }
            catch (Exception ex)
            {
                w.WriteLine($"    live GATT read error: {ex.Message}");
            }
        }
    }

    /// <summary>Writes the full report to <see cref="ReportFile"/> in the log folder and returns its path.</summary>
    public static string WriteReport(IEnumerable<IBatterySource> sources)
    {
        var w = new StringWriter();
        string version = System.Windows.Forms.Application.ProductVersion.Split('+')[0];
        w.WriteLine("ControllerHP diagnostics report");
        w.WriteLine($"Created {DateTime.Now:yyyy-MM-dd HH:mm:ss zzz}.");
        w.WriteLine("Bluetooth addresses are partly masked (for example ********7583); the last four characters");
        w.WriteLine("still tell devices apart. Have a look through before posting it anyway.");
        w.WriteLine();
        Section(w, "System");
        w.WriteLine($"ControllerHP {version}");
        w.WriteLine($"{RuntimeInformation.OSDescription} ({RuntimeInformation.OSArchitecture})");
        w.WriteLine(RuntimeInformation.FrameworkDescription);

        Section(w, "Settings");
        w.WriteLine($"Hidden device types: {(Settings.HiddenKinds.Count == 0 ? "none" : string.Join(", ", Settings.HiddenKinds))}");
        w.WriteLine($"Tray icons: {(Settings.TrayIconLimit == 0 ? "one per device" : Settings.TrayIconLimit)}");
        w.WriteLine($"Pop-up position: {Settings.ToastCorner}");
        w.WriteLine($"Connection pop-ups: {OnOff(Settings.ConnectionToasts)}");
        w.WriteLine($"Fully charged pop-up: {OnOff(Settings.FullyChargedToasts)}");
        w.WriteLine($"Hold while a game is fullscreen: {OnOff(Settings.HoldInFullscreen)}");
        w.WriteLine($"Low battery: {OnOff(Settings.LowWarningEnabled)}, {Settings.LowWarning}%");
        w.WriteLine($"Critical battery: {OnOff(Settings.CriticalWarningEnabled)}, {Settings.CriticalWarning}%");
        w.WriteLine($"Warning sounds: {OnOff(Settings.WarningSounds)}");
        w.WriteLine($"Share battery levels: {OnOff(Settings.ShareStatus)}");
        w.WriteLine($"Start with Windows: {OnOff(Settings.StartWithWindows)}");
        w.WriteLine($"Removed devices: {Settings.HiddenControllers.Count}");
        w.WriteLine($"Renamed devices: {Settings.ControllerNames.Count}");
        w.WriteLine($"Left out of the chart: {Settings.LeftOutOfChart.Count}");
        foreach (var (id, levels) in Settings.ControllerWarnings)
            w.WriteLine($"Own warning levels for {id}: low {levels.Low?.ToString() ?? "off"}, critical {levels.Critical?.ToString() ?? "off"}");

        Section(w, "Battery sources");
        WriteSources(w, sources);

        Section(w, $"Battery history ({BatteryHistory.FileName})");
        var history = BatteryHistory.Load(DateTime.Now - BatteryHistory.Kept);
        if (history.Count == 0)
            w.WriteLine("(none)");
        foreach (var s in history)
            w.WriteLine($"{s.Name}: {s.Points.Count} readings, {s.Points[0].Time:yyyy-MM-dd HH:mm} to {s.Points[^1].Time:yyyy-MM-dd HH:mm}, " +
                $"last {(s.Points[^1].Percent is int p ? $"{p}%" : "disconnected")}  [id={s.Id}]");

        foreach (var (file, title) in new[] { (Program.HistoryLogFile, "readings"), (Program.ErrorLogFile, "errors") })
        {
            Section(w, $"{file}, last {LogTailLines} lines ({title})");
            var lines = ReadTail(Path.Combine(Program.LogDirectory, file));
            if (lines.Count == 0)
                w.WriteLine("(empty)");
            foreach (var line in lines)
                w.WriteLine(line);
        }

        string path = Path.Combine(Program.LogDirectory, ReportFile);
        Directory.CreateDirectory(Program.LogDirectory);
        File.WriteAllText(path, MaskAddresses(w.ToString()), Encoding.UTF8);
        return path;
    }

    private static void Section(TextWriter w, string title)
    {
        w.WriteLine();
        w.WriteLine($"== {title} ==");
    }

    private static string OnOff(bool on) => on ? "on" : "off";

    private static List<string> ReadTail(string path)
    {
        try
        {
            if (!File.Exists(path))
                return [];
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(stream);
            var lines = new Queue<string>();
            while (reader.ReadLine() is { } line)
            {
                lines.Enqueue(line);
                if (lines.Count > LogTailLines)
                    lines.Dequeue();
            }
            return [.. lines];
        }
        catch (IOException ex)
        {
            return [$"(couldn't read it: {ex.Message})"];
        }
    }

    /// <summary>
    /// A Bluetooth address is 12 hex digits on its own (in device paths and ids). The last group of a GUID is
    /// also 12 hex digits but sits after a dash, so it's left alone.
    /// </summary>
    [GeneratedRegex(@"(?<![0-9A-Fa-f-])[0-9A-Fa-f]{8}([0-9A-Fa-f]{4})(?![0-9A-Fa-f-])")]
    private static partial Regex BluetoothAddress();

    public static string MaskAddresses(string text) => BluetoothAddress().Replace(text, "********$1");
}
