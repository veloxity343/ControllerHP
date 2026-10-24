namespace WattsLeft.Battery;

/// <summary>
/// Works out charging from a battery level rising, for devices whose source doesn't say whether they're
/// charging (Windows records only the level for Bluetooth devices). Keyed by device.
/// </summary>
internal sealed class ChargeTrends
{
    /// <summary>
    /// No rise for this long is taken to mean it was unplugged. A guess until the real charge rate is
    /// known; history.log records each step, so tune this from there.
    /// </summary>
    private static readonly TimeSpan RiseTimeout = TimeSpan.FromMinutes(5);

    /// <summary>
    /// How far the level has to climb from its low before it counts as charging. A battery that's just been
    /// resting can tick up a point (an Xbox controller over Bluetooth goes 94, 95, 94), so one step isn't enough;
    /// devices that report in 5 or 10% steps still show charging on their first step up.
    /// </summary>
    private const int MinimumRise = 2;

    private sealed class Trend(int percent)
    {
        public int Percent = percent;
        /// <summary>The level rises are measured from: the lowest since it last dropped or stopped charging.</summary>
        public int Low = percent;
        public bool Rising;
        public DateTime LastRise;
    }

    private readonly Dictionary<string, Trend> _trends = [];

    /// <summary>Whether the device looks to be charging, given its latest level.</summary>
    public bool Charging(string key, int percent)
    {
        var now = DateTime.UtcNow;
        if (!_trends.TryGetValue(key, out var trend))
        {
            _trends[key] = new Trend(percent);
            return false;
        }

        if (percent > trend.Percent)
        {
            trend.LastRise = now;
            if (percent - trend.Low >= MinimumRise)
                trend.Rising = true;
        }
        else if (percent < trend.Percent)
        {
            trend.Rising = false;
            trend.Low = percent;
        }
        trend.Percent = percent;

        // Full means done charging, and a long stall means it was unplugged before the next drop.
        if (trend.Rising && (percent >= 100 || now - trend.LastRise > RiseTimeout))
        {
            trend.Rising = false;
            trend.Low = percent;
        }
        return trend.Rising;
    }

    /// <summary>Forgets devices that went away, so a reconnect starts fresh.</summary>
    public void KeepOnly(IEnumerable<string> keys)
    {
        var keep = keys.ToHashSet();
        foreach (var key in _trends.Keys.Where(k => !keep.Contains(k)).ToList())
            _trends.Remove(key);
    }
}
