using System.Runtime.InteropServices;
using Windows.Devices.Enumeration;
using Windows.Devices.HumanInterfaceDevice;

namespace ControllerHP.Battery;

/// <summary>
/// Reads Sony controllers (DualSense, DualSense Edge, DualShock 4) straight from their HID input reports,
/// since Windows doesn't surface their battery anywhere. Each connected controller gets a background loop
/// that samples one report every few seconds and raises <see cref="ControllersChanged"/> when it changes.
/// </summary>
/// <remarks>
/// Over Bluetooth these controllers send a short report without battery data until the calibration
/// feature report is read, which switches them to the full report until they reconnect. Games that
/// read the short report through DirectInput lose input while that lasts; Steam Input and games with
/// native support read the full report. Report layouts follow Linux's hid-playstation driver.
/// </remarks>
public sealed class PlayStationSource : IBatterySource, IDisposable
{
    public const ushort SonyVendorId = 0x054C;

    private enum Family { DualSense, DualShock4 }

    private static readonly Dictionary<ushort, (string Name, Family Family)> Models = new()
    {
        [0x0CE6] = ("DualSense Wireless Controller", Family.DualSense),
        [0x0DF2] = ("DualSense Edge Wireless Controller", Family.DualSense),
        [0x05C4] = ("DUALSHOCK 4 Wireless Controller", Family.DualShock4),
        [0x09CC] = ("DUALSHOCK 4 Wireless Controller", Family.DualShock4),
    };

    public static IEnumerable<ushort> ProductIds => Models.Keys;

    private static readonly TimeSpan SampleInterval = TimeSpan.FromSeconds(5);
    /// <summary>Full reports stream continuously, so a read this slow means no battery data is coming.</summary>
    private static readonly TimeSpan ReadTimeout = TimeSpan.FromSeconds(2);
    /// <summary>Before reopening after a failure, e.g. while another app holds the controller exclusively.</summary>
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(10);

    private const int UsbReportLength = 64;

    /// <summary>Calibration feature report; reading it over Bluetooth turns on the full input report.</summary>
    private const byte CalibrationReportId = 0x05;

    private const string VendorIdKey = "System.DeviceInterface.Hid.VendorId";
    private const string ProductIdKey = "System.DeviceInterface.Hid.ProductId";

    private sealed record Status(int? Percent, ChargeState Power);

    private sealed class Controller(string id, ushort productId)
    {
        public readonly string Id = id;
        public readonly ushort ProductId = productId;
        public readonly CancellationTokenSource Stop = new();
        /// <summary>null until the first report arrives.</summary>
        public volatile Status? Latest;
        /// <summary>Whether the calibration read that turns on the full Bluetooth report has succeeded.</summary>
        public bool FullReportRequested;
    }

    public string Name => "PlayStation HID";

    public event EventHandler? ControllersChanged;

    private readonly Dictionary<string, Controller> _controllers = [];
    private readonly DeviceWatcher _watcher;

    public PlayStationSource()
    {
        // Game controllers are usage page 0x01 (Generic Desktop), usage 0x05 (Game Pad); keep Sony's.
        _watcher = DeviceInformation.CreateWatcher(HidDevice.GetDeviceSelector(0x01, 0x05), [VendorIdKey, ProductIdKey]);
        _watcher.Added += (_, device) => OnAdded(device);
        _watcher.Removed += (_, update) => OnRemoved(update.Id);
        // Unused, but without an Updated handler the watcher doesn't raise the other events reliably.
        _watcher.Updated += (_, _) => { };
        _watcher.Start();
    }

    public IReadOnlyList<ControllerBattery> Read()
    {
        lock (_controllers)
            return _controllers.Values.Select(ToReading).ToList();
    }

    private ControllerBattery ToReading(Controller controller)
    {
        var status = controller.Latest ?? new Status(null, ChargeState.Unknown);
        var level = status.Percent is int p ? ControllerBattery.LevelFromPercent(p) : BatteryLevel.Unknown;
        return new(controller.Id, Models[controller.ProductId].Name, Name, status.Percent, level, status.Power,
            $"{SonyVendorId:X4}:{controller.ProductId:X4}");
    }

    private void OnAdded(DeviceInformation device)
    {
        if (AsUShort(device.Properties.GetValueOrDefault(VendorIdKey)) != SonyVendorId ||
            AsUShort(device.Properties.GetValueOrDefault(ProductIdKey)) is not ushort productId ||
            !Models.ContainsKey(productId))
        {
            return;
        }

        var controller = new Controller(device.Id, productId);
        lock (_controllers)
        {
            if (!_controllers.TryAdd(device.Id, controller))
                return;
        }
        _ = Task.Run(() => WatchAsync(controller));
        ControllersChanged?.Invoke(this, EventArgs.Empty);
    }

    private static ushort? AsUShort(object? value) => value is IConvertible number ? number.ToUInt16(null) : null;

    private void OnRemoved(string id)
    {
        Controller? controller;
        lock (_controllers)
        {
            if (!_controllers.Remove(id, out controller))
                return;
        }
        controller.Stop.Cancel();
        ControllersChanged?.Invoke(this, EventArgs.Empty);
    }

    private async Task WatchAsync(Controller controller)
    {
        var token = controller.Stop.Token;
        while (!token.IsCancellationRequested)
        {
            var delay = SampleInterval;
            try
            {
                var status = await SampleAsync(controller, token);
                if (status != controller.Latest)
                {
                    controller.Latest = status;
                    ControllersChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"{Models[controller.ProductId].Name}: {ex.Message}");
                delay = RetryDelay;
            }

            try
            {
                await Task.Delay(delay, token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    /// <summary>
    /// Opens the controller just long enough to read one report, so tools that want it exclusively
    /// (DS4Windows, HidHide) aren't blocked by a handle held open.
    /// </summary>
    private static async Task<Status> SampleAsync(Controller controller, CancellationToken token)
    {
        using var handle = Hid.Open(controller.Id, Hid.GenericRead | Hid.GenericWrite);
        if (handle.IsInvalid)
            throw new IOException($"open failed: error {Marshal.GetLastWin32Error()}");
        var caps = Hid.GetCaps(handle) ?? throw new IOException("couldn't read HID capabilities");

        if (!controller.FullReportRequested)
        {
            // Over USB this is just a harmless read (or unsupported, on a DualShock 4).
            var feature = new byte[caps.FeatureReportByteLength];
            feature[0] = CalibrationReportId;
            controller.FullReportRequested = Hid.HidD_GetFeature(handle, feature, feature.Length);
        }

        using var stream = new FileStream(handle, FileAccess.Read, 0, isAsync: true);
        var buffer = new byte[caps.InputReportByteLength];
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(ReadTimeout);
        // Reads come back padded to the longest input report, which is 64 bytes only over USB.
        bool bluetooth = caps.InputReportByteLength > UsbReportLength;
        while (true)
        {
            int read = await stream.ReadAsync(buffer, timeout.Token);
            if (read == 0)
                throw new IOException("device closed");
            // Short reports can still arrive just after the switch; full ones follow within milliseconds.
            if (Parse(Models[controller.ProductId].Family, bluetooth, buffer.AsSpan(0, read)) is Status status)
                return status;
        }
    }

    /// <returns>null if the report carries no battery data (Bluetooth before the full report is on).</returns>
    private static Status? Parse(Family family, bool bluetooth, ReadOnlySpan<byte> report)
    {
        // Where the status byte sits: USB uses report 0x01, Bluetooth's full report is 0x31 or 0x11.
        // Bluetooth's short report is also 0x01 but carries no battery data.
        int offset = (family, bluetooth, report[0]) switch
        {
            (Family.DualSense, false, 0x01) => 53,
            (Family.DualSense, true, 0x31) => 54,
            (Family.DualShock4, false, 0x01) => 30,
            (Family.DualShock4, true, 0x11) => 32,
            _ => -1,
        };
        if (offset < 0 || offset >= report.Length)
            return null;

        byte status = report[offset];
        int level = status & 0x0F;
        int percent = Math.Min(level * 10 + 5, 100);

        if (family == Family.DualSense)
        {
            return (status >> 4) switch
            {
                0x0 => new(percent, ChargeState.Discharging),
                0x1 => new(percent, ChargeState.Charging),
                // Full but still on the cable; no bolt, like the Xbox inference at 100%.
                0x2 => new(100, ChargeState.Discharging),
                _ => new(null, ChargeState.Unknown), // voltage, temperature or charging error
            };
        }

        bool cable = (status & 0x10) != 0;
        return (cable, level) switch
        {
            (false, _) => new(percent, ChargeState.Discharging),
            (true, < 10) => new(percent, ChargeState.Charging),
            (true, 10 or 11) => new(100, ChargeState.Discharging),
            _ => new(null, ChargeState.Unknown),
        };
    }

    public void Dispose()
    {
        if (_watcher.Status is DeviceWatcherStatus.Started or DeviceWatcherStatus.EnumerationCompleted)
            _watcher.Stop();
        lock (_controllers)
        {
            foreach (var controller in _controllers.Values)
                controller.Stop.Cancel();
            _controllers.Clear();
        }
    }
}
