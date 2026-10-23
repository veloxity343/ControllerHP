using System.Text.RegularExpressions;
using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;

namespace ControllerHP.Battery;

/// <summary>
/// Every Bluetooth device whose battery level Windows itself records: the level shown in Settings →
/// Bluetooth &amp; devices, which Windows reads from the device's standard battery service. That covers most
/// Bluetooth mice, keyboards and headsets, over both Bluetooth Low Energy and classic Bluetooth. Xbox and
/// PlayStation controllers are left to their own sources, which know more about them.
/// </summary>
public sealed partial class BluetoothSource : IBatterySource, IDisposable
{
    public string Name => "Bluetooth";

    public event EventHandler? ControllersChanged;

    private readonly List<DeviceWatcher> _watchers = [];
    private readonly ChargeTrends _trends = new();
    /// <summary>A device's type doesn't change, so it's looked up once per address.</summary>
    private readonly Dictionary<string, DeviceKind> _kinds = [];

    // Bluetooth service ids (16-bit, in their full form) that mean a device plays audio: A2DP sink,
    // hands-free, headset and headset audio gateway.
    private static readonly string[] AudioServices = ["0000110b-", "0000111e-", "00001108-", "00001131-"];

    /// <summary>A device's own node, e.g. BTHENUM\DEV_A0FA9C2B7263\..., which carries the battery level.</summary>
    [GeneratedRegex(@"^BTH(LE|ENUM)\\DEV_([0-9A-F]{12})\\", RegexOptions.IgnoreCase)]
    private static partial Regex DeviceNodeId();

    /// <summary>
    /// A classic device's service node, e.g. BTHENUM\{0000110b-...}_VID&amp;0002054c_PID&amp;0ce6\9&amp;...&amp;A0FA9C2B7263_C00000000:
    /// the service, maybe the maker and model, then the address.
    /// </summary>
    [GeneratedRegex(@"^BTHENUM\\\{([0-9A-F-]{36})\}(?:_VID&[0-9A-F]*?([0-9A-F]{4})_PID&([0-9A-F]{4}))?[^\\]*\\.*?([0-9A-F]{12})_C[0-9A-F]+$", RegexOptions.IgnoreCase)]
    private static partial Regex ClassicServiceNodeId();

    /// <summary>A Low Energy device's service node, e.g. BTHLEDEVICE\{...}_DEV_VID&amp;02045E_PID&amp;0B13_REV&amp;0509_70F8AE8F7583\...</summary>
    [GeneratedRegex(@"^BTHLEDEVICE\\\{([0-9A-F-]{36})\}_(?:DEV_VID&[0-9A-F]*?([0-9A-F]{4})_PID&([0-9A-F]{4})_REV&[0-9A-F]{4}_)?([0-9A-F]{12})\\", RegexOptions.IgnoreCase)]
    private static partial Regex LowEnergyServiceNodeId();

    public BluetoothSource()
    {
        foreach (string prefix in new[] { @"BTHLE\DEV_", @"BTHENUM\DEV_" })
        {
            try
            {
                _watchers.Add(BluetoothLeBattery.StartWatcher(() => ControllersChanged?.Invoke(this, EventArgs.Empty), prefix));
            }
            catch (Exception ex)
            {
                // Polling still picks up changes, just less promptly.
                Program.LogOnce($"Bluetooth watcher for {prefix} failed to start: {ex.Message}");
            }
        }
    }

    private const string ContainerIdKey = "System.Devices.ContainerId";
    private const string ClassGuidKey = "System.Devices.ClassGuid";

    // Device setup classes Windows files a device's parts under.
    private static readonly Guid KeyboardClass = new("4d36e96b-e325-11ce-bfc1-08002be10318");
    private static readonly Guid MouseClass = new("4d36e96f-e325-11ce-bfc1-08002be10318");
    private static readonly Guid AudioEndpointClass = new("c166523c-fe0c-4a94-a586-f1a80cfbbf3e");
    private static readonly Guid MediaClass = new("4d36e96c-e325-11ce-bfc1-08002be10318");

    private sealed record Device(string Address, string Name, bool LowEnergy, int Percent, bool? Connected, Guid? Container)
    {
        public ushort? VendorId { get; set; }
        public ushort? ProductId { get; set; }
        public bool Audio { get; set; }
    }

    public IReadOnlyList<ControllerBattery> Read()
    {
        var devices = new Dictionary<string, Device>(StringComparer.OrdinalIgnoreCase);
        foreach (string prefix in new[] { @"BTHLE\DEV_", @"BTHENUM\DEV_" })
        {
            foreach (var node in BluetoothLeBattery.FindDevices(prefix,
                         [BluetoothLeBattery.InstanceIdKey, BluetoothLeBattery.BatteryKey, BluetoothLeBattery.ConnectedKey, ContainerIdKey]))
            {
                var match = DeviceNodeId().Match(BluetoothLeBattery.InstanceId(node));
                if (!match.Success || node.Properties.GetValueOrDefault(BluetoothLeBattery.BatteryKey) is not IConvertible battery)
                    continue;
                string address = match.Groups[2].Value.ToUpperInvariant();
                devices[address] = new Device(address, node.Name, match.Groups[1].Value.Equals("LE", StringComparison.OrdinalIgnoreCase),
                    Math.Clamp(battery.ToInt32(null), 0, 100), node.Properties.GetValueOrDefault(BluetoothLeBattery.ConnectedKey) as bool?,
                    node.Properties.GetValueOrDefault(ContainerIdKey) as Guid?);
            }
        }
        if (devices.Count == 0)
            return [];
        AddServiceDetails(devices);

        var readings = new List<ControllerBattery>();
        foreach (var device in devices.Values)
        {
            var (connected, kind) = Ask(device);
            if (connected != true)
                continue;
            // Xbox and PlayStation controllers have sources of their own, which see more than the level.
            if (kind == DeviceKind.Gamepad && device.VendorId is 0x045E or PlayStationSource.SonyVendorId)
                continue;
            if (device.LowEnergy && device.VendorId == 0x045E && device.Name.Contains("Xbox", StringComparison.OrdinalIgnoreCase))
                continue;

            string id = $"bt:{device.Address}";
            readings.Add(new ControllerBattery(id, device.Name, Name, device.Percent,
                ControllerBattery.LevelFromPercent(device.Percent),
                _trends.Charging(id, device.Percent) ? ChargeState.Charging : ChargeState.Discharging,
                device.VendorId is ushort vid ? $"{vid:X4}:{device.ProductId:X4}" : null,
                Connection.Bluetooth, kind));
        }
        _trends.KeepOnly(readings.Select(r => r.Id));
        return readings;
    }

    /// <summary>Its maker and model, and whether it plays audio, from the services Windows lists for it.</summary>
    private static void AddServiceDetails(Dictionary<string, Device> devices)
    {
        foreach (var (prefix, pattern) in new[] { (@"BTHENUM\{", ClassicServiceNodeId()), (@"BTHLEDEVICE\{", LowEnergyServiceNodeId()) })
        {
            foreach (var node in BluetoothLeBattery.FindDevices(prefix, [BluetoothLeBattery.InstanceIdKey]))
            {
                var match = pattern.Match(BluetoothLeBattery.InstanceId(node));
                if (!match.Success || !devices.TryGetValue(match.Groups[4].Value, out var device))
                    continue;
                if (AudioServices.Any(match.Groups[1].Value.ToLowerInvariant().StartsWith))
                    device.Audio = true;
                if (match.Groups[2].Success && device.VendorId is null)
                {
                    device.VendorId = Convert.ToUInt16(match.Groups[2].Value, 16);
                    device.ProductId = Convert.ToUInt16(match.Groups[3].Value, 16);
                }
            }
        }
    }

    /// <summary>
    /// Asks Windows' Bluetooth stack whether it's connected right now, and what sort of device it is.
    /// Looking a device up by address doesn't scan, so it's quick. The connected flag Windows records
    /// beside the battery level is the fallback: it can read as disconnected for a headset playing audio.
    /// </summary>
    private (bool? Connected, DeviceKind Kind) Ask(Device device)
    {
        bool? connected = device.Connected;
        _kinds.TryGetValue(device.Address, out var kind);
        bool knowKind = _kinds.ContainsKey(device.Address);
        try
        {
            ulong address = Convert.ToUInt64(device.Address, 16);
            var wait = TimeSpan.FromSeconds(3);
            if (device.LowEnergy)
            {
                var lookup = BluetoothLEDevice.FromBluetoothAddressAsync(address).AsTask();
                if (lookup.Wait(wait) && lookup.Result is { } found)
                {
                    using (found)
                    {
                        connected = found.ConnectionStatus == BluetoothConnectionStatus.Connected;
                        if (!knowKind)
                            kind = KindFromAppearance(found.Appearance.RawValue);
                    }
                }
            }
            else
            {
                var lookup = BluetoothDevice.FromBluetoothAddressAsync(address).AsTask();
                if (lookup.Wait(wait) && lookup.Result is { } found)
                {
                    using (found)
                    {
                        connected = found.ConnectionStatus == BluetoothConnectionStatus.Connected;
                        if (!knowKind)
                            kind = KindFromClass(found.ClassOfDevice.RawValue);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Program.LogOnce($"Bluetooth lookup failed: {ex.GetType().Name}: {ex.Message}");
        }

        if (!knowKind)
        {
            // Many devices don't say what they are over Bluetooth (a keyboard can advertise nothing), but
            // Windows files the parts it installs for them by class, as Settings shows.
            if (kind == DeviceKind.Other && device.Container is Guid container)
                kind = KindFromParts(container);
            if (kind == DeviceKind.Other && device.Audio)
                kind = DeviceKind.Headset;
            _kinds[device.Address] = kind;
        }
        return (connected, kind);
    }

    /// <summary>
    /// A device's type from the parts Windows installed for it: a keyboard part makes it a keyboard (keyboards
    /// often have a mouse part too, for media keys or a touchpad), else a mouse part a mouse, else an audio
    /// part a headset.
    /// </summary>
    private static DeviceKind KindFromParts(Guid container)
    {
        try
        {
            var classes = BluetoothLeBattery.FindByContainer(container, ClassGuidKey)
                .Select(part => part.Properties.GetValueOrDefault(ClassGuidKey) as Guid?)
                .OfType<Guid>()
                .ToHashSet();
            if (classes.Contains(KeyboardClass))
                return DeviceKind.Keyboard;
            if (classes.Contains(MouseClass))
                return DeviceKind.Mouse;
            if (classes.Contains(AudioEndpointClass) || classes.Contains(MediaClass))
                return DeviceKind.Headset;
        }
        catch (Exception ex)
        {
            Program.LogOnce($"Bluetooth device parts lookup failed: {ex.GetType().Name}: {ex.Message}");
        }
        return DeviceKind.Other;
    }

    /// <summary>A Bluetooth Low Energy device's type, from the GAP Appearance it advertises.</summary>
    internal static DeviceKind KindFromAppearance(uint appearance)
    {
        uint category = appearance >> 6, subcategory = appearance & 0x3F;
        return category switch
        {
            0x0F => subcategory switch // Human Interface Device
            {
                1 => DeviceKind.Keyboard,
                2 => DeviceKind.Mouse,
                3 or 4 => DeviceKind.Gamepad, // joystick, gamepad
                _ => DeviceKind.Other,
            },
            0x25 => DeviceKind.Headset, // wearable audio device: earbuds, headset, headphones, neckband
            _ => DeviceKind.Other,
        };
    }

    /// <summary>A classic Bluetooth device's type, from its Class of Device.</summary>
    internal static DeviceKind KindFromClass(uint classOfDevice)
    {
        uint major = (classOfDevice >> 8) & 0x1F, minor = (classOfDevice >> 2) & 0x3F;
        return major switch
        {
            4 when minor is 1 or 2 or 6 => DeviceKind.Headset, // audio: wearable headset, hands-free, headphones
            5 when (minor & 0x0F) is 1 or 2 => DeviceKind.Gamepad, // peripheral: joystick, gamepad
            5 => (minor >> 4) switch // peripheral: keyboard, pointing device, or both
            {
                1 or 3 => DeviceKind.Keyboard,
                2 => DeviceKind.Mouse,
                _ => DeviceKind.Other,
            },
            _ => DeviceKind.Other,
        };
    }

    public void Dispose()
    {
        foreach (var watcher in _watchers)
        {
            if (watcher.Status is DeviceWatcherStatus.Started or DeviceWatcherStatus.EnumerationCompleted)
                watcher.Stop();
        }
    }
}
