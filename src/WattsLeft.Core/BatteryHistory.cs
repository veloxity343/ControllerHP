using System.Globalization;
using System.Text;
using WattsLeft.Battery;

namespace WattsLeft;

/// <summary>One reading in a controller's history; a null percent (disconnected, or level unknown) is a gap.</summary>
internal readonly record struct HistoryPoint(DateTime Time, int? Percent, bool Charging);

/// <summary>A controller's readings over time, oldest first.</summary>
/// <param name="Slot">
/// Its colour slot: its order of first appearance in the whole file, so a controller keeps its colour
/// whichever range is shown.
/// </param>
internal sealed record HistorySeries(string Id, string Name, int Slot, IReadOnlyList<HistoryPoint> Points)
{
    private readonly string? _deviceName;

    /// <summary>What the device calls itself; <see cref="Name"/> is the user's name for it, if they gave one.</summary>
    public string DeviceName
    {
        get => _deviceName ?? Name;
        init => _deviceName = value;
    }

    /// <summary>Still connected at the latest reading.</summary>
    public bool Connected => Points.Count > 0 && Points[^1].Percent is not null;
}

/// <summary>
/// Battery history for the settings window's chart, kept in battery.csv beside the logs. Unlike
/// history.log, rows carry each controller's id, so two controllers with the same name stay apart.
/// A row is written only when a controller's reading changes, appears or disappears.
/// </summary>
internal sealed class BatteryHistory
{
    public const string FileName = "battery.csv";
    /// <summary>When the app was last known to be running, for closing sessions a crash left open.</summary>
    private const string AliveFileName = "battery.alive";
    private const string Header = "time,id,name,percent,power";
    private const string Disconnected = "Disconnected";

    /// <summary>Long enough for a battery health baseline; older rows are dropped at startup.</summary>
    public static readonly TimeSpan Kept = TimeSpan.FromDays(90);

    private static string FilePath => Path.Combine(Log.Folder, FileName);
    private static string AlivePath => Path.Combine(Log.Folder, AliveFileName);

    /// <summary>Notes that the app is running; called on every poll, so it's at most a poll interval stale.</summary>
    public static void Heartbeat()
    {
        try
        {
            Directory.CreateDirectory(Log.Folder);
            File.WriteAllText(AlivePath, DateTime.Now.ToString("O", CultureInfo.InvariantCulture));
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private readonly Dictionary<string, (int? Percent, ChargeState Power)> _written = [];

    /// <summary>Appends a row for each controller whose reading changed since the last call.</summary>
    public void Record(IReadOnlyList<ControllerBattery> readings)
    {
        var now = DateTime.Now;
        var rows = new StringBuilder();
        foreach (var reading in readings)
        {
            var state = (reading.EffectivePercent, reading.Power);
            if (_written.TryGetValue(reading.Id, out var last) && last == state)
                continue;
            _written[reading.Id] = state;
            // The device's own name, so the history doesn't change with a rename (names are applied on loading).
            rows.AppendLine(Row(now, reading.Id, reading.DeviceName, reading.EffectivePercent, reading.Power.ToString()));
        }
        foreach (var id in _written.Keys.Except(readings.Select(r => r.Id)).ToList())
        {
            _written.Remove(id);
            rows.AppendLine(Row(now, id, "", null, Disconnected));
        }
        if (rows.Length > 0)
            Append(rows.ToString());
    }

    private static string Row(DateTime time, string id, string name, int? percent, string power) =>
        string.Join(',', time.ToString("O", CultureInfo.InvariantCulture), Quote(id), Quote(name),
            percent?.ToString(CultureInfo.InvariantCulture) ?? "", power);

    private static string Quote(string value) => $"\"{value.Replace("\"", "\"\"")}\"";

    private static void Append(string rows)
    {
        try
        {
            Directory.CreateDirectory(Log.Folder);
            if (!File.Exists(FilePath))
                File.WriteAllText(FilePath, Header + Environment.NewLine);
            File.AppendAllText(FilePath, rows);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    /// <summary>
    /// Marks every controller still open in the file as disconnected. At startup that's controllers the
    /// last run never closed (a crash, a forced close or a shutdown), dated at the last heartbeat: within a
    /// poll of when the app stopped, so neither the time it wasn't running nor a steady stretch with no
    /// readings is lost. Without a heartbeat, the controller's own last reading is the fallback.
    /// </summary>
    public static void CloseOpenSessions()
    {
        try
        {
            if (!File.Exists(FilePath))
                return;
            var open = new Dictionary<string, DateTime>();
            foreach (var line in File.ReadLines(FilePath).Skip(1))
            {
                var fields = SplitCsv(line);
                if (fields.Count < 5 || ParseTime(line) is not DateTime time)
                    continue;
                if (fields[4] == Disconnected)
                    open.Remove(fields[1]);
                else
                    open[fields[1]] = time;
            }
            if (open.Count == 0)
                return;
            DateTime? alive = File.Exists(AlivePath) &&
                DateTime.TryParse(File.ReadAllText(AlivePath), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var t)
                ? t
                : null;
            Append(string.Concat(open.Select(kv =>
                Row(alive is DateTime a && a > kv.Value ? a : kv.Value, kv.Key, "", null, Disconnected) + Environment.NewLine)));
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    /// <summary>Drops rows older than <see cref="Kept"/>, so the file doesn't grow forever.</summary>
    public static void Trim()
    {
        try
        {
            if (!File.Exists(FilePath))
                return;
            var cutoff = DateTime.Now - Kept;
            var lines = File.ReadAllLines(FilePath);
            var kept = lines.Skip(1).Where(line => ParseTime(line) is DateTime t && t >= cutoff).ToList();
            if (kept.Count < lines.Length - 1)
                File.WriteAllLines(FilePath, [Header, .. kept]);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    /// <summary>
    /// Each controller's readings since <paramref name="since"/>, plus the reading in force at that moment,
    /// so a line starts at the left edge rather than at the first change inside the range.
    /// </summary>
    public static List<HistorySeries> Load(DateTime since)
    {
        var points = new Dictionary<string, List<HistoryPoint>>();
        var names = new Dictionary<string, string>();
        foreach (var row in ReadRows())
        {
            if (row.Name.Length > 0)
                names[row.Id] = row.Name;
            if (!points.TryGetValue(row.Id, out var list))
                points[row.Id] = list = [];

            var point = new HistoryPoint(row.Time, row.Percent, row.Charging);
            // Readings before the range only matter as the starting value: keep the latest one, moved to the edge.
            if (row.Time < since)
            {
                list.Clear();
                list.Add(point with { Time = since });
            }
            else
            {
                list.Add(point);
            }
        }

        var custom = Settings.ControllerNames;
        var series = points
            .Select((kv, slot) =>
            {
                string deviceName = names.GetValueOrDefault(kv.Key, "Controller");
                return new HistorySeries(kv.Key, custom.GetValueOrDefault(kv.Key, deviceName), slot, kv.Value) { DeviceName = deviceName };
            })
            // A controller that was already disconnected for the whole range has nothing to show.
            .Where(s => s.Points.Any(pt => pt.Percent is not null))
            .ToList();

        // Two controllers with the same name get numbered, in order of first appearance.
        foreach (var group in series.GroupBy(s => s.Name).Where(g => g.Count() > 1))
        {
            int n = 1;
            foreach (var s in group.ToList())
                series[series.IndexOf(s)] = s with { Name = $"{s.Name} ({n++})" };
        }
        return series;
    }

    private sealed record CachedRow(DateTime Time, string Id, string Name, int? Percent, bool Charging);

    // Parsed rows, kept between loads. The file only grows between startups, so each load reads just the
    // rows appended since the last one; the settings window reloads every 30 s, and re-parsing a full
    // 90 days each time cost about a quarter of a second.
    private static readonly object CacheLock = new();
    private static List<CachedRow> s_rows = [];
    private static string? s_cachedPath;
    private static long s_cachedLength;

    /// <summary>Every row in time order, reading only what's new since the last call.</summary>
    private static List<CachedRow> ReadRows()
    {
        lock (CacheLock)
        {
            try
            {
                var info = new FileInfo(FilePath);
                // Replaced or trimmed (shorter than what's been read): start again.
                if (!info.Exists || s_cachedPath != FilePath || info.Length < s_cachedLength)
                {
                    s_rows = [];
                    s_cachedLength = 0;
                    s_cachedPath = FilePath;
                }
                if (!info.Exists || info.Length == s_cachedLength)
                    return s_rows;

                string text;
                using (var stream = new FileStream(FilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    stream.Seek(s_cachedLength, SeekOrigin.Begin);
                    using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: false);
                    text = reader.ReadToEnd();
                }
                // Whole lines only: a row being appended right now may not have its line break yet.
                int end = text.LastIndexOf('\n') + 1;
                if (end == 0)
                    return s_rows;
                s_cachedLength += Encoding.UTF8.GetByteCount(text.AsSpan(0, end));

                var added = new List<CachedRow>();
                foreach (var line in text[..end].Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    if (ParseTime(line) is not DateTime time)
                        continue; // the header, or a damaged line
                    var fields = SplitCsv(line);
                    if (fields.Count < 5)
                        continue;
                    string power = fields[4];
                    int? percent = power == Disconnected || !int.TryParse(fields[3], CultureInfo.InvariantCulture, out int p) ? null : p;
                    added.Add(new CachedRow(time, fields[1], fields[2], percent, power == nameof(ChargeState.Charging)));
                }

                // Rows closing a crashed run's sessions are appended later but dated earlier; keep time order,
                // which Load relies on. OrderBy is stable for equal times.
                var combined = s_rows.Concat(added);
                bool inOrder = added.Count == 0 ||
                    (s_rows.Count == 0 || added[0].Time >= s_rows[^1].Time) && added.Zip(added.Skip(1)).All(pair => pair.First.Time <= pair.Second.Time);
                s_rows = inOrder ? [.. combined] : [.. combined.OrderBy(r => r.Time)];
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            return s_rows;
        }
    }

    private static DateTime? ParseTime(string line)
    {
        int comma = line.IndexOf(',');
        return comma > 0 && DateTime.TryParse(line[..comma], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var t)
            ? t
            : null;
    }

    private static List<string> SplitCsv(string line)
    {
        var fields = new List<string>();
        var field = new StringBuilder();
        bool quoted = false;
        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (quoted)
            {
                if (c == '"' && i + 1 < line.Length && line[i + 1] == '"')
                    field.Append(line[++i]);
                else if (c == '"')
                    quoted = false;
                else
                    field.Append(c);
            }
            else if (c == '"')
                quoted = true;
            else if (c == ',')
            {
                fields.Add(field.ToString());
                field.Clear();
            }
            else
                field.Append(c);
        }
        fields.Add(field.ToString());
        return fields;
    }
}
