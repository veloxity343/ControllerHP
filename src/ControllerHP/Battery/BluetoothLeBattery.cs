using System.Text.RegularExpressions;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Enumeration;

namespace ControllerHP.Battery;

/// <summary>A connected Bluetooth LE device's battery level as Windows records it (what Settings shows).</summary>
internal sealed record BleBattery(string Address, string Name, ushort VendorId, ushort ProductId, int Percent);

/// <summary>
/// Reads the battery percentage Windows caches from a Bluetooth LE device's GATT Battery Service.
/// Xbox controllers on Bluetooth report a bogus fixed value through Windows.Gaming.Input
/// (e.g. 100/1000 mWh at full charge), but the GATT value here is accurate.
/// </summary>
internal static partial class BluetoothLeBattery
{
    // Undocumented devnode properties, the same ones the Settings app reads.
    private const string BatteryKey = "{104EA319-6EE2-4701-BD47-8DDBF425BBE5} 2";
    private const string ConnectedKey = "{83DA6326-97A6-4088-9453-A1923F573B29} 15";
    private const string InstanceIdKey = "System.Devices.DeviceInstanceId";

    /// <summary>The device node, e.g. BTHLE\DEV_70F8AE8F7583\..., which carries battery and connection state.</summary>
    [GeneratedRegex(@"^BTHLE\\DEV_([0-9A-F]{12})\\", RegexOptions.IgnoreCase)]
    private static partial Regex DeviceNodeId();

    /// <summary>Its GATT service children, e.g. BTHLEDEVICE\{...}_DEV_VID&amp;02045E_PID&amp;0B13_REV&amp;0509_70F8AE8F7583\..., which carry VID/PID.</summary>
    [GeneratedRegex(@"_DEV_VID&02([0-9A-F]{4})_PID&([0-9A-F]{4})_REV&[0-9A-F]{4}_([0-9A-F]{12})\\", RegexOptions.IgnoreCase)]
    private static partial Regex ServiceNodeId();

    public static List<BleBattery> QueryConnected()
    {
        var hardwareByAddress = new Dictionary<string, (ushort Vid, ushort Pid)>(StringComparer.OrdinalIgnoreCase);
        foreach (var service in FindDevices(@"BTHLEDEVICE\", [InstanceIdKey]))
        {
            var match = ServiceNodeId().Match(InstanceId(service));
            if (match.Success)
                hardwareByAddress[match.Groups[3].Value] = (ParseHex(match.Groups[1].Value), ParseHex(match.Groups[2].Value));
        }

        var batteries = new List<BleBattery>();
        foreach (var device in FindDevices(@"BTHLE\DEV_", [InstanceIdKey, BatteryKey, ConnectedKey]))
        {
            var match = DeviceNodeId().Match(InstanceId(device));
            if (!match.Success || !hardwareByAddress.TryGetValue(match.Groups[1].Value, out var hw))
                continue;
            if (device.Properties.GetValueOrDefault(ConnectedKey) is not true)
                continue;
            if (device.Properties.GetValueOrDefault(BatteryKey) is not IConvertible battery)
                continue;

            batteries.Add(new BleBattery(match.Groups[1].Value, device.Name, hw.Vid, hw.Pid,
                Math.Clamp(battery.ToInt32(null), 0, 100)));
        }
        return batteries;
    }

    /// <summary>
    /// Watches Bluetooth LE device nodes so battery and connection changes are seen as soon as Windows
    /// records them, rather than on the next poll. <paramref name="onChange"/> runs on a thread-pool thread.
    /// </summary>
    public static DeviceWatcher StartWatcher(Action onChange)
    {
        var watcher = DeviceInformation.CreateWatcher(
            $"{InstanceIdKey}:~<\"BTHLE\\DEV_\"", [BatteryKey, ConnectedKey], DeviceInformationKind.Device);
        // Added fires once per existing device during the initial enumeration; that's harmless.
        watcher.Added += (_, _) => onChange();
        watcher.Updated += (_, _) => onChange();
        watcher.Removed += (_, _) => onChange();
        watcher.Start();
        return watcher;
    }

    /// <summary>Every property in the Bluetooth battery property set, for the --list diagnostic.</summary>
    public static IEnumerable<string> DescribeRawProperties()
    {
        string[] keys = [.. Enumerable.Range(2, 10).Select(i => $"{{104EA319-6EE2-4701-BD47-8DDBF425BBE5}} {i}"), ConnectedKey];
        foreach (var device in FindDevices(@"BTHLE\DEV_", [InstanceIdKey, .. keys]))
        {
            var values = keys.Select(k => (k, device.Properties.GetValueOrDefault(k)))
                .Where(kv => kv.Item2 is not null)
                .Select(kv => $"{kv.k[(kv.k.LastIndexOf(' ') + 1)..]}={kv.Item2}");
            yield return $"{device.Name}: {string.Join("  ", values)}";
        }
    }

    /// <summary>
    /// Reads the GATT Battery Level characteristic straight from the device, bypassing the value Windows
    /// caches. Diagnostic only: it's slow and wakes the radio.
    /// </summary>
    public static string ReadLive(string address)
    {
        using var device = BluetoothLEDevice.FromBluetoothAddressAsync(Convert.ToUInt64(address, 16)).AsTask().GetAwaiter().GetResult();
        if (device is null)
            return "device not found";

        var services = device.GetGattServicesForUuidAsync(GattServiceUuids.Battery, BluetoothCacheMode.Uncached).AsTask().GetAwaiter().GetResult();
        if (services.Status != GattCommunicationStatus.Success || services.Services.Count == 0)
            return $"battery service unavailable ({services.Status})";

        using var service = services.Services[0];
        var characteristics = service.GetCharacteristicsForUuidAsync(GattCharacteristicUuids.BatteryLevel, BluetoothCacheMode.Uncached).AsTask().GetAwaiter().GetResult();
        if (characteristics.Status != GattCommunicationStatus.Success || characteristics.Characteristics.Count == 0)
            return $"battery characteristic unavailable ({characteristics.Status})";

        var result = characteristics.Characteristics[0].ReadValueAsync(BluetoothCacheMode.Uncached).AsTask().GetAwaiter().GetResult();
        if (result.Status != GattCommunicationStatus.Success)
            return $"read failed ({result.Status})";

        using var reader = Windows.Storage.Streams.DataReader.FromBuffer(result.Value);
        return $"{reader.ReadByte()}%";
    }

    /// <summary>Lists every GATT service and characteristic, reading the readable ones. Diagnostic only.</summary>
    public static IEnumerable<string> DescribeGatt(string address)
    {
        using var device = BluetoothLEDevice.FromBluetoothAddressAsync(Convert.ToUInt64(address, 16)).AsTask().GetAwaiter().GetResult();
        if (device is null)
        {
            yield return "device not found";
            yield break;
        }
        var services = device.GetGattServicesAsync(BluetoothCacheMode.Uncached).AsTask().GetAwaiter().GetResult();
        yield return $"services: {services.Status}";
        foreach (var service in services.Services)
        {
            yield return $"service {service.Uuid}";
            var characteristics = service.GetCharacteristicsAsync(BluetoothCacheMode.Uncached).AsTask().GetAwaiter().GetResult();
            if (characteristics.Status != GattCommunicationStatus.Success)
            {
                yield return $"  characteristics: {characteristics.Status}";
                continue;
            }
            foreach (var c in characteristics.Characteristics)
            {
                string value = "";
                if (c.CharacteristicProperties.HasFlag(GattCharacteristicProperties.Read))
                {
                    var read = c.ReadValueAsync(BluetoothCacheMode.Uncached).AsTask().GetAwaiter().GetResult();
                    if (read.Status == GattCommunicationStatus.Success)
                    {
                        var bytes = new byte[read.Value.Length];
                        using (var reader = Windows.Storage.Streams.DataReader.FromBuffer(read.Value))
                            reader.ReadBytes(bytes);
                        value = $" = {Convert.ToHexString(bytes)}";
                    }
                    else
                    {
                        value = $" (read {read.Status})";
                    }
                }
                yield return $"  char {c.Uuid} [{c.CharacteristicProperties}]{value}";
            }
            service.Dispose();
        }
    }

    private static DeviceInformationCollection FindDevices(string instanceIdPrefix, string[] properties) =>
        DeviceInformation.FindAllAsync(
                $"{InstanceIdKey}:~<\"{instanceIdPrefix}\"", properties, DeviceInformationKind.Device)
            .AsTask().GetAwaiter().GetResult();

    private static string InstanceId(DeviceInformation device) =>
        device.Properties.GetValueOrDefault(InstanceIdKey) as string ?? device.Id;

    private static ushort ParseHex(string hex) => Convert.ToUInt16(hex, 16);
}
