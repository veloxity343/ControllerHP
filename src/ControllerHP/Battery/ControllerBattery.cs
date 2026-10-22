namespace ControllerHP.Battery;

public enum BatteryLevel { Unknown, Empty, Low, Medium, Full }

public enum ChargeState
{
    Unknown,
    Discharging,
    Charging,
    /// <summary>Powered by cable with no battery reported (e.g. a USB-only controller).</summary>
    Wired,
}

public enum Connection { Unknown, Usb, Bluetooth, WirelessAdapter }

/// <summary>What sort of device it is, which picks the shape of its tray icon.</summary>
public enum DeviceKind { Gamepad, Mouse, Keyboard, Headset, Other }

/// <summary>A point-in-time battery reading for one controller.</summary>
/// <param name="Id">Stable identifier used to keep one tray icon per physical controller.</param>
/// <param name="Percent">Exact charge if the source reports it; null for coarse-level sources like XInput.</param>
/// <param name="Hardware">USB vendor/product ids ("VID:PID") when known, for identifying the controller family.</param>
/// <param name="Connection">How it's connected, when the source can tell.</param>
/// <param name="Kind">What sort of device it is.</param>
public sealed record ControllerBattery(
    string Id,
    string Name,
    string Source,
    int? Percent,
    BatteryLevel Level,
    ChargeState Power,
    string? Hardware = null,
    Connection Connection = Connection.Unknown,
    DeviceKind Kind = DeviceKind.Gamepad)
{
    private readonly string? _deviceName;

    /// <summary>
    /// What the device calls itself. <see cref="Name"/> is what's shown, which is the user's name for it
    /// when they've renamed it.
    /// </summary>
    public string DeviceName
    {
        get => _deviceName ?? Name;
        init => _deviceName = value;
    }

    /// <summary>
    /// The percentage to display: the exact value when known, otherwise a representative value for the
    /// coarse level, so icons and low-battery warnings work the same for every source.
    /// </summary>
    public int? EffectivePercent => Percent ?? Level switch
    {
        BatteryLevel.Empty => 5,
        BatteryLevel.Low => 20,
        BatteryLevel.Medium => 50,
        BatteryLevel.Full => 100,
        _ => null,
    };

    public string Describe()
    {
        string charge = Power switch
        {
            ChargeState.Wired => "wired (no battery)",
            _ when Percent is int p => $"{p}%",
            _ when Level != BatteryLevel.Unknown => Level.ToString(),
            _ => "battery unknown",
        };
        return Power == ChargeState.Charging ? $"{Name}: {charge}, charging" : $"{Name}: {charge}";
    }

    public static BatteryLevel LevelFromPercent(int percent) => percent switch
    {
        <= 10 => BatteryLevel.Empty,
        <= 25 => BatteryLevel.Low,
        <= 60 => BatteryLevel.Medium,
        _ => BatteryLevel.Full,
    };
}

/// <summary>
/// A way of discovering controllers and reading their batteries. Xbox controllers are covered by
/// Windows.Gaming.Input and XInput, PlayStation controllers by their raw HID reports.
/// </summary>
public interface IBatterySource
{
    string Name { get; }

    /// <summary>Raised (on any thread) when a controller is connected or disconnected, if the source can tell.</summary>
    event EventHandler? ControllersChanged;

    IReadOnlyList<ControllerBattery> Read();
}
