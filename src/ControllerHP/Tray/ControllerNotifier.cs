using ControllerHP.Battery;

namespace ControllerHP.Tray;

/// <summary>
/// Turns successive readings into toasts: controller connected/disconnected and battery crossing the
/// low thresholds. Each threshold fires once and re-arms after the battery recovers or starts charging.
/// While a game is fullscreen, toasts can be held and shown once it closes.
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

    /// <summary>The "Battery low" level in percent, or null when that warning is off.</summary>
    public int? LowWarning { get; set; } = 20;

    /// <summary>The "Battery critical" level in percent, or null when that warning is off.</summary>
    public int? CriticalWarning { get; set; } = 10;

    private IEnumerable<int> Thresholds => new[] { LowWarning, CriticalWarning }.OfType<int>();
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

    private enum ToastKind { Connection, Warning }

    /// <summary>Toasts waiting for a fullscreen game to close: the latest per controller, in arrival order.</summary>
    private readonly Dictionary<string, (ToastContent Content, ToastKind Kind)> _held = [];
    private readonly System.Windows.Forms.Timer _heldTimer = new() { Interval = 2000 };

    /// <summary>
    /// Hold toasts while a fullscreen app (usually a borderless game) is running. Toasts are always held in
    /// exclusive fullscreen, where they couldn't be seen anyway.
    /// </summary>
    public bool HoldInFullscreen { get; set; }

    /// <summary>How the fullscreen state is checked; replaceable so holding can be tested without a game.</summary>
    public Func<FullscreenState> QueryFullscreen { get; set; } = Fullscreen.Query;

    public ControllerNotifier()
    {
        _heldTimer.Tick += (_, _) => ShowHeldIfClear();
    }

    private bool ShouldHold() => QueryFullscreen() switch
    {
        FullscreenState.ExclusiveGame => true,
        FullscreenState.App => HoldInFullscreen,
        _ => false,
    };

    private void Show(string id, ToastContent content, ToastKind kind)
    {
        if (!ShouldHold())
        {
            _toasts.Show(content);
            return;
        }
        // One per controller: a newer toast (say Disconnected after Battery low) replaces the older one.
        _held.Remove(id);
        _held[id] = (content, kind);
        _heldTimer.Start();
    }

    /// <summary>Once nothing is fullscreen, shows what was held, minus warnings for controllers charged since.</summary>
    private void ShowHeldIfClear()
    {
        if (_held.Count == 0 || ShouldHold())
        {
            if (_held.Count == 0)
                _heldTimer.Stop();
            return;
        }
        _heldTimer.Stop();
        foreach (var (id, (content, kind)) in _held.ToList())
        {
            bool chargedSince = _tracked.TryGetValue(id, out var tracked) &&
                (tracked.Reading.Power is ChargeState.Charging or ChargeState.Wired || tracked.Notified == NoneNotified);
            if (kind == ToastKind.Warning && chargedSince)
                continue;
            _toasts.Show(content);
        }
        _held.Clear();
    }


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
                Show(gone, new ToastContent(tracked.Reading.Name, "Disconnected", null, DisconnectedAccent), ToastKind.Connection);
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
                Show(reading.Id, new ToastContent(reading.Name, $"Connected · {Status(reading)}", reading, accent),
                    crossed is null ? ToastKind.Connection : ToastKind.Warning);
            }
            else if (crossed is int threshold && reading.EffectivePercent is int percent)
            {
                string title = threshold == CriticalWarning ? "Battery critical" : "Battery low";
                Show(reading.Id, new ToastContent(reading.Name, $"{title} · {percent}%", reading,
                    BatteryIconRenderer.FillColor(percent)), ToastKind.Warning);
            }
        }

        return settling;
    }

    /// <returns>The threshold newly crossed downwards, if any.</returns>
    private int? CheckThresholds(Tracked tracked)
    {
        var reading = tracked.Reading;
        if (reading.Power is ChargeState.Charging or ChargeState.Wired || reading.EffectivePercent is not int percent)
        {
            if (reading.Power == ChargeState.Charging)
                tracked.Notified = NoneNotified;
            return null;
        }

        int severest = Thresholds.Where(t => percent <= t).DefaultIfEmpty(NoneNotified).Min();

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

    public void Dispose()
    {
        _heldTimer.Dispose();
        _toasts.Dispose();
    }
}
