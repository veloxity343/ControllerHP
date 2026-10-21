using Windows.Gaming.Input;
using Windows.System.Power;

namespace ControllerHP.Battery;

/// <summary>
/// Reads Xbox-compatible gamepads through Windows.Gaming.Input, which sees them over USB, the Xbox
/// Wireless Adapter and Bluetooth. For Bluetooth pads the percentage comes from
/// <see cref="BluetoothLeBattery"/> instead, since Windows.Gaming.Input's report is wrong there.
/// </summary>
public sealed class GamingInputSource : IBatterySource, IDisposable
{
    public string Name => "Windows.Gaming.Input";

    public event EventHandler? ControllersChanged;

    private readonly Windows.Devices.Enumeration.DeviceWatcher? _bluetoothWatcher;
    private readonly Dictionary<string, ChargeTrend> _trends = [];

    public GamingInputSource()
    {
        Gamepad.GamepadAdded += OnGamepadChanged;
        Gamepad.GamepadRemoved += OnGamepadChanged;
        // Touching the collection starts enumeration; already-connected pads arrive via GamepadAdded.
        _ = Gamepad.Gamepads.Count;

        try
        {
            _bluetoothWatcher = BluetoothLeBattery.StartWatcher(() => ControllersChanged?.Invoke(this, EventArgs.Empty));
        }
        catch (Exception ex)
        {
            // Polling still picks up battery changes, just less promptly.
            System.Diagnostics.Debug.WriteLine($"Bluetooth LE watcher failed to start: {ex}");
        }
    }

    public IReadOnlyList<ControllerBattery> Read()
    {
        var pads = Gamepad.Gamepads;
        var bluetooth = QueryBluetoothBatteries().Where(IsXboxController).ToList();
        if (pads.Count == 0 && bluetooth.Count == 0)
            return [];

        // Forget trends for controllers that went away, so a reconnect starts fresh.
        foreach (var address in _trends.Keys.Except(bluetooth.Select(b => b.Address)).ToList())
            _trends.Remove(address);

        var readings = new List<ControllerBattery>();
        foreach (var pad in pads)
        {
            // A pad that has just gone away (seen with a DualSense disconnecting) can linger in the list
            // and fail every call; skip it rather than losing every other pad with it.
            try
            {
                if (ReadPad(pad, bluetooth) is { } reading)
                    readings.Add(reading);
            }
            catch (Exception ex)
            {
                Program.LogOnce($"Windows.Gaming.Input pad failed: {ex.GetType().Name}: {ex.Message}");
            }
        }

        // Windows.Gaming.Input's list can go stale in a long-running process: after a restart, a
        // DualSense disconnecting has taken a Bluetooth Xbox pad out of it until that pad reconnected.
        // Windows still knows the pad is connected, so show it from its Bluetooth reading alone.
        foreach (var ble in bluetooth)
        {
            readings.Add(new ControllerBattery(BluetoothId(ble), ble.Name, "Bluetooth LE", ble.Percent,
                ControllerBattery.LevelFromPercent(ble.Percent),
                InferCharging(ble) ? ChargeState.Charging : ChargeState.Discharging,
                $"{ble.VendorId:X4}:{ble.ProductId:X4}", Connection.Bluetooth));
        }
        return readings;
    }

    /// <summary>Keyed by Bluetooth address, so a pad keeps its identity whether or not Windows.Gaming.Input lists it.</summary>
    private static string BluetoothId(BleBattery ble) => $"ble:{ble.Address}";

    /// <summary>The Bluetooth batteries include mice, headsets and the like; keep Xbox controllers.</summary>
    private static bool IsXboxController(BleBattery ble) =>
        ble.VendorId == MicrosoftVendorId && ble.Name.Contains("Xbox", StringComparison.OrdinalIgnoreCase);

    private ControllerBattery? ReadPad(Gamepad pad, List<BleBattery> bluetooth)
    {
        var raw = RawGameController.FromGameController(pad);
        if (raw is null)
        {
            // Also seen while a DualSense disconnects; a real Xbox pad always has one.
            Program.LogOnce("Windows.Gaming.Input pad without a RawGameController skipped");
            return null;
        }
        // PlayStationSource reads these from their HID reports, which carry the real battery.
        if (raw.HardwareVendorId == PlayStationSource.SonyVendorId)
            return null;

        string id = raw.NonRoamableId is { Length: > 0 } nid ? nid : $"wgi:{pad.GetHashCode()}";
        string hardware = $"{raw.HardwareVendorId:X4}:{raw.HardwareProductId:X4}";
        var reading = ToReading(id, DisplayName(raw), pad.TryGetBatteryReport()) with
        {
            Hardware = hardware,
            // Bluetooth pads are matched below; any other wireless pad is on the Xbox Wireless Adapter.
            Connection = raw.IsWireless ? Connection.WirelessAdapter : Connection.Usb,
        };

        // Windows.Gaming.Input doesn't say which device a pad is, so pair by VID/PID. Two identical
        // controllers, one on Bluetooth and one not, could be swapped; accepted for now.
        var ble = bluetooth.FirstOrDefault(b => b.VendorId == raw.HardwareVendorId && b.ProductId == raw.HardwareProductId);
        if (ble is null)
            return reading;

        bluetooth.Remove(ble);
        return reading with
        {
            Id = BluetoothId(ble),
            Name = ble.Name,
            Connection = Connection.Bluetooth,
            Percent = ble.Percent,
            Level = ControllerBattery.LevelFromPercent(ble.Percent),
            Power = reading.Power == ChargeState.Charging || InferCharging(ble)
                ? ChargeState.Charging
                : ChargeState.Discharging,
        };
    }

    /// <summary>
    /// Over Bluetooth, Windows exposes no charging flag (the controller's own battery report is filtered
    /// out by the xinputhid driver), so charging is inferred from the percentage rising.
    /// </summary>
    private sealed class ChargeTrend(int percent)
    {
        public int Percent = percent;
        public bool Rising;
        public DateTime LastRise;
    }

    /// <summary>
    /// No rise for this long is taken to mean it was unplugged. A guess until the real charge rate is
    /// known; history.log records each step, so tune this from there.
    /// </summary>
    private static readonly TimeSpan RiseTimeout = TimeSpan.FromMinutes(5);

    private bool InferCharging(BleBattery ble)
    {
        var now = DateTime.UtcNow;
        if (!_trends.TryGetValue(ble.Address, out var trend))
        {
            _trends[ble.Address] = new ChargeTrend(ble.Percent);
            return false;
        }

        if (ble.Percent > trend.Percent)
        {
            trend.Rising = true;
            trend.LastRise = now;
        }
        else if (ble.Percent < trend.Percent)
        {
            trend.Rising = false;
        }
        trend.Percent = ble.Percent;

        // Full means done charging, and a long stall means it was unplugged before the next drop.
        if (trend.Rising && (ble.Percent >= 100 || now - trend.LastRise > RiseTimeout))
            trend.Rising = false;
        return trend.Rising;
    }

    private static List<BleBattery> QueryBluetoothBatteries()
    {
        try
        {
            return BluetoothLeBattery.QueryConnected();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Bluetooth LE battery query failed: {ex}");
            return [];
        }
    }

    /// <summary>Raw battery report fields for every pad, for the --list diagnostic.</summary>
    public static IEnumerable<string> DescribeRawReports()
    {
        foreach (var pad in Gamepad.Gamepads)
        {
            var r = pad.TryGetBatteryReport();
            yield return r is null
                ? "raw: (no report)"
                : $"raw: status={r.Status} remaining={r.RemainingCapacityInMilliwattHours?.ToString() ?? "null"}mWh " +
                  $"full={r.FullChargeCapacityInMilliwattHours?.ToString() ?? "null"}mWh " +
                  $"design={r.DesignCapacityInMilliwattHours?.ToString() ?? "null"}mWh " +
                  $"rate={r.ChargeRateInMilliwatts?.ToString() ?? "null"}mW";
        }
    }

    private const ushort MicrosoftVendorId = 0x045E;

    /// <summary>Bluetooth pads often report the generic HID name, so fall back to the vendor.</summary>
    private static string DisplayName(RawGameController? raw)
    {
        string? name = raw?.DisplayName;
        bool generic = string.IsNullOrWhiteSpace(name) || name.Contains("HID-compliant", StringComparison.OrdinalIgnoreCase);
        if (!generic)
            return name!;
        return raw?.HardwareVendorId == MicrosoftVendorId ? "Xbox Controller" : name ?? "Game Controller";
    }

    private ControllerBattery ToReading(string id, string name, Windows.Devices.Power.BatteryReport? report)
    {
        if (report is null)
            return new(id, name, Name, null, BatteryLevel.Unknown, ChargeState.Unknown);

        if (report.Status == BatteryStatus.NotPresent)
            return new(id, name, Name, null, BatteryLevel.Unknown, ChargeState.Wired);

        int? percent = null;
        if (report.RemainingCapacityInMilliwattHours is int remaining &&
            report.FullChargeCapacityInMilliwattHours is int full && full > 0)
        {
            percent = Math.Clamp((int)Math.Round(remaining * 100.0 / full), 0, 100);
        }

        var level = percent is int p ? ControllerBattery.LevelFromPercent(p) : BatteryLevel.Unknown;
        var power = report.Status == BatteryStatus.Charging ? ChargeState.Charging : ChargeState.Discharging;
        return new(id, name, Name, percent, level, power);
    }

    private void OnGamepadChanged(object? sender, Gamepad e) => ControllersChanged?.Invoke(this, EventArgs.Empty);

    public void Dispose()
    {
        Gamepad.GamepadAdded -= OnGamepadChanged;
        Gamepad.GamepadRemoved -= OnGamepadChanged;
        if (_bluetoothWatcher?.Status is Windows.Devices.Enumeration.DeviceWatcherStatus.Started
            or Windows.Devices.Enumeration.DeviceWatcherStatus.EnumerationCompleted)
        {
            _bluetoothWatcher.Stop();
        }
    }
}
