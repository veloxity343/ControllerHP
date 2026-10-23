using System.Runtime.InteropServices;
using Windows.Devices.Enumeration;

namespace ControllerHP.Battery;

/// <summary>
/// Wireless gaming mice from several makers whose mice run the same firmware family, and so answer the same
/// battery request: WLmouse, LAMZU and G-Wolves. Each is asked on its receiver (or its cable) through a
/// vendor-defined HID collection. Only that one read request is ever sent.
/// </summary>
/// <remarks>
/// The request is a 64-byte feature report with no report id, the bytes 00 00 02 02 00 83 then zeros. The
/// mouse answers in the same feature report: a status byte (A1 awake), two bytes, 02 00 83, then the charging
/// flag and the battery percentage. Older G-Wolves models use a variant: 00 02 8F, then 01 over a receiver
/// or 00 on the cable; the answer is A1 02 8F, a byte, the charging flag and the percentage. The request
/// only goes to a collection on usage page FFFF whose feature report is exactly that 64 bytes, which is
/// where the makers' own web drivers send it; if Windows doesn't give the size, nothing is sent.
/// </remarks>
public sealed class MouseReceiverSource : IBatterySource, IDisposable
{
    public string Name => "Mouse receivers";

    public event EventHandler? ControllersChanged;

    internal enum Request { Shared, OlderGWolves }

    /// <summary>
    /// A known receiver or cable. Models sharing a <paramref name="Group"/> are one mouse, so its receiver
    /// and its cable give one icon.
    /// </summary>
    private sealed record Model(string Name, string Group, bool Cable, Request Request);

    private const ushort WLmouse = 0x36A7, Lamzu = 0x373E, GWolves = 0x33E4;

    private static readonly Dictionary<(ushort Vendor, ushort Product), Model> Models = BuildModels();

    private static Dictionary<(ushort, ushort), Model> BuildModels()
    {
        var models = new Dictionary<(ushort, ushort), Model>
        {
            // WLmouse: each id is a receiver, and all of them one mouse's icon.
            [(WLmouse, 0xA887)] = new("WLmouse Beast X", "wlmouse", false, Request.Shared),
            [(WLmouse, 0xA868)] = new("WLmouse Beast X Mini Pro", "wlmouse", false, Request.Shared),
            [(WLmouse, 0xA880)] = new("WLmouse Beast X Max", "wlmouse", false, Request.Shared),
            // LAMZU Maya X: its 8K receiver and its cable.
            [(Lamzu, 0x001E)] = new("LAMZU Maya X", "lamzu-maya-x", false, Request.Shared),
            [(Lamzu, 0x001C)] = new("LAMZU Maya X", "lamzu-maya-x", true, Request.Shared),
            // G-Wolves' shared 8K receiver, which doesn't say which mouse it has.
            [(GWolves, 0x3854)] = new("G-Wolves mouse", "gwolves-3854", false, Request.Shared),
        };

        // Mice that pair with the shared receiver, on their cables.
        (ushort Cable, string Name)[] sharedReceiverMice =
        [
            (0x5418, "G-Wolves HTS Plus"), (0x5419, "G-Wolves HTS Plus"), (0x5219, "G-Wolves HTS Plus Pro"),
            (0x4718, "G-Wolves Lycan"), (0x4719, "G-Wolves Lycan"), (0x5618, "G-Wolves HTXU"),
            (0x5619, "G-Wolves HTXU"), (0x3619, "G-Wolves Fenrir Pro"), (0x3519, "G-Wolves Fenrir Asym"),
            (0x2719, "G-Wolves HTX Mini"), (0x4219, "G-Wolves WARG"),
        ];
        foreach (var (cable, name) in sharedReceiverMice)
            models[(GWolves, cable)] = new(name, "gwolves-3854", true, Request.Shared);

        // G-Wolves models with a receiver of their own: receiver id, cable ids, and which request they answer.
        (ushort Receiver, ushort[] Cables, string Name, Request Request)[] ownReceiverMice =
        [
            (0x3817, [0x3808], "G-Wolves HTM Plus", Request.Shared),
            (0x6817, [0x6808], "G-Wolves HSK Pro 2.0", Request.Shared),
            (0x5617, [0x5608], "G-Wolves HTXU", Request.Shared),
            (0x3617, [0x3608], "G-Wolves Fenrir Pro", Request.Shared),
            (0x3917, [0x3908], "G-Wolves VUK", Request.OlderGWolves),
            (0x7913, [0x7904], "G-Wolves HT-S2", Request.OlderGWolves),
            (0x3717, [0x3708], "G-Wolves Fenrir Max", Request.OlderGWolves),
            (0x5317, [0x5308], "G-Wolves HTS Ultra", Request.OlderGWolves),
            (0x7713, [0x7704], "G-Wolves HTR", Request.OlderGWolves),
            (0x3517, [0x3508], "G-Wolves Fenrir", Request.OlderGWolves),
            (0x7917, [0x7908], "G-Wolves HT-S2 Pro", Request.OlderGWolves),
            (0x5817, [0x5808, 0x5807], "G-Wolves HSK Pro", Request.OlderGWolves),
            (0x2717, [0x2708], "G-Wolves HTX Mini", Request.OlderGWolves),
            (0x5417, [0x5408, 0x5407], "G-Wolves HTS Plus", Request.OlderGWolves),
            (0x5717, [0x5708, 0x5707], "G-Wolves HTX", Request.OlderGWolves),
            (0x5917, [0x5908, 0x5907], "G-Wolves HSK Plus", Request.OlderGWolves),
            (0x7203, [0x7204], "G-Wolves HSK Lite", Request.OlderGWolves),
            (0x7717, [0x7708], "G-Wolves HTR Pro", Request.OlderGWolves),
            (0x5803, [0x5804], "G-Wolves HSK Pro ACE", Request.OlderGWolves),
            (0x5403, [0x5404], "G-Wolves HTS Plus ACE", Request.OlderGWolves),
            (0x5703, [0x5704], "G-Wolves HTX ACE", Request.OlderGWolves),
            (0x5903, [0x5904], "G-Wolves HSK Plus ACE", Request.OlderGWolves),
        ];
        foreach (var (receiver, cables, name, request) in ownReceiverMice)
        {
            string group = $"gwolves-{receiver:X4}";
            models[(GWolves, receiver)] = new(name, group, false, request);
            foreach (ushort cable in cables)
                models[(GWolves, cable)] = new(name, group, true, request);
        }
        return models;
    }

    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(30);
    /// <summary>
    /// The receiver can't tell a sleeping mouse from one switched off: neither answers. A mouse keeps its last
    /// reading this long, so one that dozes between games doesn't come and go.
    /// </summary>
    private static readonly TimeSpan AsleepKeep = TimeSpan.FromMinutes(30);
    /// <summary>
    /// A receiver whose mouse doesn't answer is slow to give up on, so once nothing is shown it's only asked
    /// every so many polls, after a few tries at the full rate.
    /// </summary>
    private const int TriesAtFullRate = 3, SilentPollEvery = 5;
    private const int ReplyTries = 15;
    private static readonly TimeSpan ReplyWait = TimeSpan.FromMilliseconds(50);

    /// <summary>The report id byte plus the 64 bytes of the request.</summary>
    private const int FeatureLength = 65;

    private const string VendorIdKey = "System.DeviceInterface.Hid.VendorId";
    private const string ProductIdKey = "System.DeviceInterface.Hid.ProductId";
    private const string HidInterfaceClass = "{4D1E55B2-F16F-11CF-88CB-001111000030}";

    /// <summary>One mouse: its receiver and cable interfaces present now, and its latest reading.</summary>
    private sealed class Mouse(string group)
    {
        public readonly string Group = group;
        public readonly Dictionary<string, Model> Interfaces = [];
        public ControllerBattery? Reading;
        public DateTime LastAnswer;
        public int SilentPolls, Skipped;
    }

    private readonly Dictionary<string, Mouse> _mice = [];
    private readonly DeviceWatcher _watcher;
    private readonly CancellationTokenSource _stop = new();

    public MouseReceiverSource()
    {
        // Every HID collection on the vendor usage page FFFF; only known makers' are kept.
        _watcher = DeviceInformation.CreateWatcher(
            $"System.Devices.InterfaceClassGuid:=\"{HidInterfaceClass}\" AND System.DeviceInterface.Hid.UsagePage:=65535",
            [VendorIdKey, ProductIdKey], DeviceInformationKind.DeviceInterface);
        _watcher.Added += (_, device) => OnAdded(device);
        _watcher.Removed += (_, update) => OnRemoved(update.Id);
        // Unused, but without an Updated handler the watcher doesn't raise the other events reliably.
        _watcher.Updated += (_, _) => { };
        _watcher.Start();
        _ = Task.Run(() => PollLoopAsync(_stop.Token));
    }

    public IReadOnlyList<ControllerBattery> Read()
    {
        lock (_mice)
            return _mice.Values.Select(m => m.Reading).OfType<ControllerBattery>().ToList();
    }

    private void OnAdded(DeviceInformation device)
    {
        if (AsUShort(device.Properties.GetValueOrDefault(VendorIdKey)) is not ushort vendor ||
            AsUShort(device.Properties.GetValueOrDefault(ProductIdKey)) is not ushort product ||
            !Models.TryGetValue((vendor, product), out var model))
        {
            return;
        }
        lock (_mice)
        {
            if (!_mice.TryGetValue(model.Group, out var mouse))
                _mice[model.Group] = mouse = new Mouse(model.Group);
            mouse.Interfaces[device.Id] = model;
            // Its cable appearing (charging) is worth asking about straight away.
            mouse.Skipped = SilentPollEvery;
        }
    }

    private void OnRemoved(string id)
    {
        bool changed = false;
        lock (_mice)
        {
            foreach (var mouse in _mice.Values.Where(m => m.Interfaces.Remove(id)).ToList())
            {
                if (mouse.Interfaces.Count == 0)
                {
                    _mice.Remove(mouse.Group);
                    changed |= mouse.Reading is not null;
                }
            }
        }
        if (changed)
            ControllersChanged?.Invoke(this, EventArgs.Empty);
    }

    private static ushort? AsUShort(object? value) => value is IConvertible number ? number.ToUInt16(null) : null;

    private async Task PollLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            Mouse[] mice;
            lock (_mice)
                mice = [.. _mice.Values];
            bool changed = false;
            foreach (var mouse in mice)
            {
                try
                {
                    changed |= await PollAsync(mouse, token);
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    Program.LogOnce($"Mouse receiver {mouse.Group} failed: {ex.GetType().Name}: {ex.Message}");
                }
            }
            if (changed)
                ControllersChanged?.Invoke(this, EventArgs.Empty);

            try
            {
                await Task.Delay(PollInterval, token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    /// <summary>Asks the mouse, on its cable first if it's plugged in. True if its reading changed.</summary>
    private async Task<bool> PollAsync(Mouse mouse, CancellationToken token)
    {
        KeyValuePair<string, Model>[] interfaces;
        lock (_mice)
        {
            bool shown = mouse.Reading is not null && DateTime.UtcNow - mouse.LastAnswer < AsleepKeep;
            if (!shown && mouse.SilentPolls >= TriesAtFullRate && ++mouse.Skipped < SilentPollEvery)
                return false;
            mouse.Skipped = 0;
            interfaces = [.. mouse.Interfaces.OrderByDescending(kv => kv.Value.Cable)];
        }

        (int Percent, bool Charging, Model Model)? answer = null;
        foreach (var (path, model) in interfaces)
        {
            if (await AskAsync(path, model, token) is { } reading)
            {
                answer = (reading.Percent, reading.Charging, model);
                break;
            }
        }

        lock (_mice)
        {
            var before = mouse.Reading;
            if (answer is var (percent, charging, model))
            {
                mouse.SilentPolls = 0;
                mouse.LastAnswer = DateTime.UtcNow;
                mouse.Reading = new ControllerBattery($"mouse:{mouse.Group}", model.Name, Name, percent,
                    ControllerBattery.LevelFromPercent(percent), charging ? ChargeState.Charging : ChargeState.Discharging,
                    null, model.Cable ? Connection.Usb : Connection.Receiver, DeviceKind.Mouse);
            }
            else
            {
                mouse.SilentPolls++;
                if (mouse.Reading is not null && DateTime.UtcNow - mouse.LastAnswer >= AsleepKeep)
                    mouse.Reading = null;
            }
            return mouse.Reading != before;
        }
    }

    /// <summary>
    /// Sends the request and reads the answer back, on a collection whose feature report is exactly the
    /// request's size. Null if it's some other collection, or nothing answered.
    /// </summary>
    private static async Task<(int Percent, bool Charging)?> AskAsync(string path, Model model, CancellationToken token)
    {
        // Sizes first, through a handle that can neither read nor write.
        using (var probe = Hid.Open(path, 0))
        {
            if (probe.IsInvalid || Hid.GetCaps(probe) is not { } caps || caps.FeatureReportByteLength != FeatureLength)
                return null;
        }

        using var handle = Hid.Open(path, Hid.GenericRead | Hid.GenericWrite);
        if (handle.IsInvalid)
            return null;

        var request = new byte[FeatureLength];
        byte[] payload = model.Request == Request.Shared
            ? [0x00, 0x00, 0x02, 0x02, 0x00, 0x83]
            : [0x00, 0x02, 0x8F, (byte)(model.Cable ? 0x00 : 0x01)];
        payload.CopyTo(request, 1);
        if (!Hid.HidD_SetFeature(handle, request, request.Length))
            return null;

        var reply = new byte[FeatureLength];
        for (int i = 0; i < ReplyTries; i++)
        {
            await Task.Delay(ReplyWait, token);
            Array.Clear(reply);
            if (Hid.HidD_GetFeature(handle, reply, reply.Length) && Parse(reply, model.Request) is { } reading)
                return reading;
        }
        return null;
    }

    /// <summary>
    /// The percentage and charging flag in an answer. Its position can be a byte off, depending on whether
    /// the report id is counted, so the answer's fixed bytes are looked for rather than assumed.
    /// </summary>
    internal static (int Percent, bool Charging)? Parse(ReadOnlySpan<byte> reply, Request request)
    {
        if (request == Request.Shared)
        {
            // A1 (or A2) .. .. 02 00 83 <charging> <percent>: the two bytes after the status can differ by model.
            for (int i = 0; i + 7 < reply.Length; i++)
            {
                if (reply[i] is 0xA1 or 0xA2 && reply[i + 3] == 0x02 && reply[i + 4] == 0x00 && reply[i + 5] == 0x83 &&
                    reply[i + 7] <= 100)
                {
                    return (reply[i + 7], reply[i + 6] != 0);
                }
            }
            return null;
        }

        // A1 02 8F <byte> <charging> <percent>
        for (int i = 0; i <= 1 && i + 5 < reply.Length; i++)
        {
            if (reply[i] == 0xA1 && reply[i + 1] == 0x02 && reply[i + 2] == 0x8F && reply[i + 5] <= 100)
                return (reply[i + 5], reply[i + 4] != 0);
        }
        return null;
    }

    public void Dispose()
    {
        _stop.Cancel();
        if (_watcher.Status is DeviceWatcherStatus.Started or DeviceWatcherStatus.EnumerationCompleted)
            _watcher.Stop();
    }
}
