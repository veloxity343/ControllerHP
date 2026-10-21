using ControllerHP.Battery;

namespace ControllerHP.Tray;

/// <summary>
/// Turns successive readings into toasts: controller connected/disconnected and battery crossing the
/// low thresholds. Each threshold fires once and re-arms after the battery recovers or starts charging.
/// </summary>
internal sealed class ControllerNotifier : IDisposable
{
    /// <summary>
    /// How long a newly seen controller is left alone before toasting. Right after a Bluetooth reconnect,
    /// Windows.Gaming.Input can report its bogus 10% before the real Bluetooth reading arrives.
    /// </summary>
    public static readonly TimeSpan SettleTime = TimeSpan.FromSeconds(2);

    /// <summary>Controllers already on when the app starts don't get a "connected" toast.</summary>
    private static readonly TimeSpan StartupQuietTime = TimeSpan.FromSeconds(5);

    private static readonly int[] LowThresholds = [20, 10];
    private const int RearmMargin = 5;
    private const int NoneNotified = int.MaxValue;

    private static readonly Color ConnectedAccent = Color.FromArgb(52, 199, 89);
    private static readonly Color DisconnectedAccent = Color.FromArgb(120, 120, 120);

    private sealed class Tracked(ControllerBattery reading, DateTime firstSeen, bool announced)
    {
        public ControllerBattery Reading = reading;
        public readonly DateTime FirstSeen = firstSeen;
        public bool Announced = announced;
        /// <summary>The most severe threshold already toasted, or <see cref="NoneNotified"/>.</summary>
        public int Notified = NoneNotified;
    }

    private readonly ToastManager _toasts = new();
    private readonly Dictionary<string, Tracked> _tracked = [];
    private readonly DateTime _startedAt = DateTime.UtcNow;

    /// <summary>Whether connecting and disconnecting show a toast; battery warnings always do.</summary>
    public bool ConnectionToasts { get; set; } = true;

    public ToastCorner Corner
    {
        get => _toasts.Corner;
        set => _toasts.Corner = value;
    }

    /// <summary>Shows where toasts will appear, after the position changes.</summary>
    public void ShowSample() =>
        _toasts.Show(new ToastContent("ControllerHP", "Pop-ups appear here", null, ConnectedAccent));

    /// <returns>True if a controller is still settling and <see cref="Update"/> should be called again shortly.</returns>
    public bool Update(IReadOnlyList<ControllerBattery> readings)
    {
        var now = DateTime.UtcNow;
        bool settling = false;

        foreach (var gone in _tracked.Keys.Except(readings.Select(r => r.Id)).ToList())
        {
            var tracked = _tracked[gone];
            _tracked.Remove(gone);
            // A controller that vanished before it was announced was a blip; stay quiet.
            if (tracked.Announced && ConnectionToasts)
                _toasts.Show(new ToastContent(tracked.Reading.Name, "Disconnected", null, DisconnectedAccent));
        }

        foreach (var reading in readings)
        {
            if (!_tracked.TryGetValue(reading.Id, out var tracked))
            {
                bool quiet = now - _startedAt < StartupQuietTime;
                _tracked[reading.Id] = tracked = new Tracked(reading, now, announced: quiet);
            }
            tracked.Reading = reading;

            if (now - tracked.FirstSeen < SettleTime)
            {
                settling = true;
                continue;
            }

            int? crossed = CheckThresholds(tracked);
            bool announcing = !tracked.Announced;
            tracked.Announced = true;
            // With connection toasts off, a controller that connects low still gets its battery warning.
            if (announcing && ConnectionToasts)
            {
                var accent = crossed is not null && reading.EffectivePercent is int p
                    ? BatteryIconRenderer.FillColor(p)
                    : ConnectedAccent;
                _toasts.Show(new ToastContent(reading.Name, $"Connected · {Status(reading)}", reading, accent));
            }
            else if (crossed is int threshold && reading.EffectivePercent is int percent)
            {
                string title = threshold <= LowThresholds[^1] ? "Battery critical" : "Battery low";
                _toasts.Show(new ToastContent(reading.Name, $"{title} · {percent}%", reading,
                    BatteryIconRenderer.FillColor(percent)));
            }
        }

        return settling;
    }

    /// <returns>The threshold newly crossed downwards, if any.</returns>
    private static int? CheckThresholds(Tracked tracked)
    {
        var reading = tracked.Reading;
        if (reading.Power is ChargeState.Charging or ChargeState.Wired || reading.EffectivePercent is not int percent)
        {
            if (reading.Power == ChargeState.Charging)
                tracked.Notified = NoneNotified;
            return null;
        }

        int severest = LowThresholds.Where(t => percent <= t).DefaultIfEmpty(NoneNotified).Min();

        // Recovered well above the last warning (e.g. batteries swapped): allow it to warn again.
        if (tracked.Notified != NoneNotified && percent >= tracked.Notified + RearmMargin)
            tracked.Notified = severest;

        if (severest < tracked.Notified)
        {
            tracked.Notified = severest;
            return severest;
        }
        return null;
    }

    private static string Status(ControllerBattery reading) => reading.Power switch
    {
        ChargeState.Wired => "wired",
        ChargeState.Charging when reading.EffectivePercent is int p => $"{p}%, charging",
        ChargeState.Charging => "charging",
        _ when reading.EffectivePercent is int p => $"{p}%",
        _ => "battery unknown",
    };

    public void Dispose() => _toasts.Dispose();
}
