namespace WattsLeft;

/// <summary>
/// Figures for one controller, worked out from its battery history. A null figure means there isn't
/// enough history yet to give one honestly.
/// </summary>
internal sealed record ControllerStats
{
    /// <summary>Average drain while on battery over the shown range, in percent per hour.</summary>
    public double? DrainPerHour { get; init; }

    /// <summary>Time until the countdown level while discharging; null when charging or unknown.</summary>
    public TimeSpan? UntilLow { get; init; }
    /// <summary>The level <see cref="UntilLow"/> counts down to: the low warning, or 0 when that's off.</summary>
    public int CountdownLevel { get; init; }

    /// <summary>Time until full while charging; null when discharging or unknown.</summary>
    public TimeSpan? UntilFull { get; init; }
    public bool Charging { get; init; }

    /// <summary>Estimated runtime on a full charge, from the last week's drain.</summary>
    public TimeSpan? RuntimePerCharge { get; init; }
    /// <summary>The same estimate from the controller's first week, once its history is long enough.</summary>
    public TimeSpan? BaselineRuntime { get; init; }
    /// <summary>Runtime now as a share of the baseline, capped at 100%.</summary>
    public int? HealthPercent { get; init; }
    /// <summary>A commonly reported range for this model, for comparison before a baseline exists.</summary>
    public (int Low, int High)? TypicalHours { get; init; }

    public TimeSpan PlayTime { get; init; }
    public TimeSpan PlayTimeToday { get; init; }
    /// <summary>Total discharged over the range divided by 100. Not used for health: cycling isn't wear by itself.</summary>
    public double ChargeCycles { get; init; }
    public DateTime? LastCharged { get; init; }
    public int? LastChargedTo { get; init; }
    public int LowCount { get; init; }
    public int LowLevel { get; init; }
}

/// <summary>Works out <see cref="ControllerStats"/> from a controller's readings.</summary>
internal static class BatteryStats
{
    /// <summary>e.g. "45 min", "3 h 20 min", "1 d 4 h"; rounded to 5 minutes, since none of this is precise.</summary>
    public static string FormatDuration(TimeSpan span, string zero = "0 min")
    {
        if (span < TimeSpan.FromMinutes(2.5))
            return zero;
        var rounded = TimeSpan.FromMinutes(Math.Round(span.TotalMinutes / 5) * 5);
        if (rounded.TotalDays >= 1)
            return rounded.Hours > 0 ? $"{(int)rounded.TotalDays} d {rounded.Hours} h" : $"{(int)rounded.TotalDays} d";
        if (rounded.TotalHours >= 1)
            return rounded.Minutes > 0 ? $"{(int)rounded.TotalHours} h {rounded.Minutes} min" : $"{(int)rounded.TotalHours} h";
        return $"{rounded.Minutes} min";
    }

    /// <summary>
    /// Time left as the tray shows it, e.g. "3 h to 20%" or "1 h 30 min to full": the same figure as the
    /// Controllers page's Until card. Null when there isn't enough history to say.
    /// </summary>
    public static string? TimeLeft(ControllerStats s)
    {
        if (s.Charging)
            return s.UntilFull is TimeSpan full ? $"{FormatDuration(full)} to full" : null;
        return s.UntilLow is TimeSpan low ? $"{FormatDuration(low)} to {(s.CountdownLevel > 0 ? $"{s.CountdownLevel}%" : "empty")}" : null;
    }
    /// <summary>History kept for the baseline: long enough to compare a controller with its early self.</summary>
    public static readonly TimeSpan BaselineAfter = TimeSpan.FromDays(21);
    private static readonly TimeSpan Week = TimeSpan.FromDays(7);

    /// <summary>
    /// A rate needs at least this much measured time and change to mean anything: one 10% step of a
    /// PlayStation controller, or a few 1% steps of an Xbox one, over a meaningful stretch.
    /// </summary>
    private static readonly TimeSpan MinimumMeasured = TimeSpan.FromMinutes(45);
    private const int MinimumChange = 5;

    /// <param name="history">All of the controller's readings (the full retained history).</param>
    /// <param name="rangeStart">Start of the range shown in the chart, for the range-based figures.</param>
    /// <param name="lowWarning">The low warning level, or null when that warning is off.</param>
    public static ControllerStats Compute(HistorySeries history, DateTime rangeStart, DateTime now, int? lowWarning)
    {
        var points = history.Points;
        var latest = points.Count > 0 ? points[^1] : (HistoryPoint?)null;
        bool connected = latest?.Percent is not null;
        bool charging = connected && latest!.Value.Charging;
        int current = latest?.Percent ?? 0;

        var recentDrain = Rate(points, now - Week, now, charging: false);
        var rangeDrain = Rate(points, rangeStart, now, charging: false);
        var chargeRate = Rate(points, now - Week * 4, now, charging: true);

        int countdownTo = lowWarning ?? 0;
        TimeSpan? untilLow = connected && !charging && recentDrain is double drain && current > countdownTo
            ? TimeSpan.FromHours((current - countdownTo) / drain)
            : null;
        TimeSpan? untilFull = charging && chargeRate is double rise && current < 100
            ? TimeSpan.FromHours((100 - current) / rise)
            : null;

        // Health: how long a charge lasts on battery now, against this controller's own first week.
        TimeSpan? runtime = recentDrain is double r ? TimeSpan.FromHours(100 / r) : null;
        TimeSpan? baseline = null;
        int? health = null;
        var firstSeen = points.Count > 0 ? points[0].Time : now;
        if (now - firstSeen >= BaselineAfter && Rate(points, firstSeen, firstSeen + Week, charging: false) is double early)
        {
            baseline = TimeSpan.FromHours(100 / early);
            if (runtime is TimeSpan rt)
                health = (int)Math.Min(100, Math.Round(rt / baseline.Value * 100 / 5) * 5);
        }

        int lowLevel = lowWarning ?? 20;
        var (play, playToday, discharged, lows) = Usage(points, rangeStart, now, lowLevel);
        var (lastCharged, lastChargedTo) = LastCharge(points);

        return new ControllerStats
        {
            DrainPerHour = rangeDrain,
            UntilLow = untilLow,
            CountdownLevel = countdownTo,
            UntilFull = untilFull,
            Charging = charging,
            RuntimePerCharge = runtime,
            BaselineRuntime = baseline,
            HealthPercent = health,
            TypicalHours = Typical(history.DeviceName),
            PlayTime = play,
            PlayTimeToday = playToday,
            ChargeCycles = discharged / 100.0,
            LastCharged = lastCharged,
            LastChargedTo = lastChargedTo,
            LowCount = lows,
            LowLevel = lowLevel,
        };
    }

    /// <summary>
    /// Average change in percent per hour while discharging (or charging), measured between the moments
    /// the reading changed. Measuring from a connection or a plateau would skew it: a DualSense could be
    /// anywhere inside a 10% step when it connects, and one sitting full on a cable isn't draining.
    /// Spans break at disconnections, unknown readings, the other charge state, and any reversal.
    /// </summary>
    private static double? Rate(IReadOnlyList<HistoryPoint> points, DateTime from, DateTime to, bool charging)
    {
        double totalChange = 0, totalHours = 0;
        DateTime? spanStart = null;
        int spanStartPercent = 0;
        int? previous = null;
        foreach (var p in points)
        {
            bool inSpan = p.Percent is int && p.Charging == charging && p.Time >= from && p.Time <= to;
            bool continues = inSpan && previous is int prev &&
                (charging ? p.Percent >= prev : p.Percent <= prev);
            if (!continues)
            {
                spanStart = null;
                previous = inSpan ? p.Percent : null;
                continue;
            }

            if (p.Percent != previous)
            {
                // A change: the first one opens the measured span, each later one extends it.
                if (spanStart is null)
                {
                    spanStart = p.Time;
                    spanStartPercent = p.Percent!.Value;
                }
                else
                {
                    totalChange += Math.Abs(p.Percent!.Value - spanStartPercent);
                    totalHours += (p.Time - spanStart.Value).TotalHours;
                    spanStart = p.Time;
                    spanStartPercent = p.Percent.Value;
                }
            }
            previous = p.Percent;
        }
        return totalHours >= MinimumMeasured.TotalHours && totalChange >= MinimumChange ? totalChange / totalHours : null;
    }

    /// <summary>Time on battery, how much was discharged, and how often it dropped to the low level, over the range.</summary>
    private static (TimeSpan Play, TimeSpan Today, int Discharged, int Lows) Usage(
        IReadOnlyList<HistoryPoint> points, DateTime from, DateTime now, int lowLevel)
    {
        var play = TimeSpan.Zero;
        var today = TimeSpan.Zero;
        int discharged = 0, lows = 0;
        for (int i = 0; i < points.Count; i++)
        {
            var p = points[i];
            if (p.Percent is not int percent || p.Charging)
                continue;
            var start = p.Time < from ? from : p.Time;
            var end = i + 1 < points.Count ? points[i + 1].Time : now;
            if (end > start)
            {
                play += end - start;
                var todayStart = start < now.Date ? now.Date : start;
                if (end > todayStart)
                    today += end - todayStart;
            }
            if (i + 1 < points.Count && points[i + 1].Percent is int next && points[i + 1].Time >= from)
            {
                if (next < percent && !points[i + 1].Charging)
                    discharged += percent - next;
                if (percent > lowLevel && next <= lowLevel)
                    lows++;
            }
        }
        return (play, today, discharged, lows);
    }

    /// <summary>When charging last stopped (or now, if it's charging), and the level it reached.</summary>
    private static (DateTime?, int?) LastCharge(IReadOnlyList<HistoryPoint> points)
    {
        for (int i = points.Count - 1; i >= 0; i--)
        {
            if (points[i].Charging && points[i].Percent is int reached)
                return (i + 1 < points.Count ? points[i + 1].Time : null, reached);
        }
        return (null, null);
    }

    /// <summary>
    /// Commonly reported runtimes, not official figures: they vary with vibration, light bar, audio and,
    /// for Xbox, AA batteries versus a rechargeable pack. No figure where none is widely reported.
    /// </summary>
    private static (int, int)? Typical(string name) => name switch
    {
        _ when name.Contains("DualSense Edge", StringComparison.OrdinalIgnoreCase) => null,
        _ when name.Contains("DualSense", StringComparison.OrdinalIgnoreCase) => (6, 12),
        _ when name.Contains("DUALSHOCK 4", StringComparison.OrdinalIgnoreCase) => (4, 8),
        _ when name.Contains("Xbox", StringComparison.OrdinalIgnoreCase) => (30, 40),
        _ => null,
    };
}
