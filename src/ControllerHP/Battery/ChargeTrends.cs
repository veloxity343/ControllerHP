namespace ControllerHP.Battery;

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

    private sealed class Trend(int percent)
    {
        public int Percent = percent;
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
            trend.Rising = true;
            trend.LastRise = now;
        }
        else if (percent < trend.Percent)
        {
            trend.Rising = false;
        }
        trend.Percent = percent;

        // Full means done charging, and a long stall means it was unplugged before the next drop.
        if (trend.Rising && (percent >= 100 || now - trend.LastRise > RiseTimeout))
            trend.Rising = false;
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
