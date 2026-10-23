namespace ControllerHP.Battery.Vendor;

/// <summary>The Astro A50 Gen 5 base station.</summary>
/// <remarks>
/// On its FF32:0074 collection: 64-byte frames, report 02, then 0C, the length that follows (3 plus the
/// payload), 00, a command and a handle that comes back in the reply. Command 06 reads the battery: the
/// reply 02 0C .. 00 06 carries the level in byte 6 and, in byte 8, whether the headset is docked, which is
/// how the station says it's charging. The station also pushes frames of the same shape, so only a reply
/// to command 06 counts.
/// </remarks>
internal sealed class Astro : VendorProtocol
{
    public override string Name => "Astro";

    public override IReadOnlyCollection<ushort> Vendors { get; } = [0x046D];

    public override VendorModel? Identify(ushort vendor, ushort product) =>
        product == 0x0B1C ? new VendorModel("Astro A50 Gen 5", "a50-gen5", DeviceKind.Headset) : null;

    private static readonly TimeSpan ReadWait = TimeSpan.FromMilliseconds(300);

    public override async Task<VendorReading?> ReadAsync(VendorDevice device, CancellationToken token)
    {
        var collection = device.Collections.Values.Select(c => c.Collection).FirstOrDefault(c => c.UsagePage == 0xFF32 && c.Usage == 0x0074);
        if (collection is null || HidLink.Open(collection) is not { } link)
            return null;
        using (link)
        {
            while (await link.ReadAsync(_ => true, TimeSpan.FromMilliseconds(30), token) is not null) { }
            for (int attempt = 0; attempt < 3; attempt++)
            {
                var r = await link.AskAsync(link.OutputReport(0x02, [0x0C, 0x03, 0x00, 0x06, 0x0C]),
                    r => r.Length > 8 && r[0] == 0x02 && r[1] == 0x0C && r[4] == 0x06 && r[6] <= 100, ReadWait, token);
                if (r is not null)
                    return new VendorReading(r[6], r[8] != 0 ? ChargeState.Charging : ChargeState.Discharging);
            }
        }
        return null;
    }
}

/// <summary>Audeze Maxwell and Maxwell 2 headsets, on their dongles or USB-C cables.</summary>
/// <remarks>
/// On the FF13:0001 collection: output report 06 07 80 05 5A 03 00 D6 0C asks for the battery attribute
/// (0CD6). The answer is input report 07, a rolling buffer of the dongle's recent replies, so the newest
/// frame carrying the marker D6 0C 00 00 &lt;percent&gt; counts. With no headset linked, the dongle names itself
/// "Audeze Maxwell Dongle" and keeps repeating the last level, so that name means off. There's no charging
/// flag, but a Maxwell always charges on USB-C, so its cable endpoint answering means charging.
/// </remarks>
internal sealed class Audeze : VendorProtocol
{
    public override string Name => "Audeze";

    public override IReadOnlyCollection<ushort> Vendors { get; } = [0x3329];

    private static readonly Dictionary<ushort, (string Name, string Group, bool Cable)> Models = new()
    {
        [0x4B19] = ("Audeze Maxwell", "maxwell", false), [0x4B1A] = ("Audeze Maxwell", "maxwell", true),
        [0x4B18] = ("Audeze Maxwell (Xbox)", "maxwell-xbox", false), [0x4B1E] = ("Audeze Maxwell (Xbox)", "maxwell-xbox", true),
        [0x4B29] = ("Audeze Maxwell 2", "maxwell-2", false),
        [0x4B28] = ("Audeze Maxwell 2 (Xbox)", "maxwell-2-xbox", false),
    };

    public override VendorModel? Identify(ushort vendor, ushort product) =>
        Models.TryGetValue(product, out var m) ? new VendorModel(m.Name, m.Group, DeviceKind.Headset, m.Cable) : null;

    private static readonly TimeSpan ReadWait = TimeSpan.FromMilliseconds(400);

    public override async Task<VendorReading?> ReadAsync(VendorDevice device, CancellationToken token)
    {
        foreach (var (collection, model) in device.ByPreference)
        {
            if (collection.UsagePage != 0xFF13 || collection.Usage != 0x0001)
                continue;
            if (!model.Cable && collection.ProductName()?.Contains("Dongle", StringComparison.OrdinalIgnoreCase) == true)
                return VendorReading.SwitchedOff; // no headset linked
            using var link = HidLink.Open(collection);
            if (link is null)
                continue;
            if (!await link.WriteAsync(link.OutputReport(0x06, [0x07, 0x80, 0x05, 0x5A, 0x03, 0x00, 0xD6, 0x0C]), token))
                continue;
            int? level = null;
            // The answer can take a frame or two; the newest marker wins.
            for (int i = 0; i < 3; i++)
            {
                if (await link.ReadAsync(r => r.Length > 0 && r[0] == 0x07, ReadWait, token) is not { } frame)
                    break;
                level = FindLevel(frame) ?? level;
            }
            if (level is int percent)
                return new VendorReading(percent, model.Cable ? ChargeState.Charging : ChargeState.Discharging);
        }
        return null;
    }

    /// <summary>The percentage after the first D6 0C 00 00 marker that's followed by a real percentage.</summary>
    internal static int? FindLevel(byte[] frame)
    {
        for (int i = 0; i + 4 < frame.Length; i++)
        {
            if (frame[i] == 0xD6 && frame[i + 1] == 0x0C && frame[i + 2] == 0x00 && frame[i + 3] == 0x00 && frame[i + 4] <= 100)
                return frame[i + 4];
        }
        return null;
    }
}

/// <summary>The JBL Quantum 910 Wireless.</summary>
/// <remarks>
/// Nothing is ever sent to it. The headset pushes its battery on its FF13:0001 collection as report 08 with
/// the level in byte 1, but only on events (being plugged in to charge, say), and a report that arrives while
/// nobody's listening is lost. So a listener keeps the collection open, and a reading is kept for as long as
/// the receiver is plugged in. No charging flag.
/// </remarks>
internal sealed class Jbl : VendorProtocol
{
    public override string Name => "JBL";

    public override IReadOnlyCollection<ushort> Vendors { get; } = [0x0ECB];

    public override VendorModel? Identify(ushort vendor, ushort product) =>
        product == 0x2088 ? new VendorModel("JBL Quantum 910 Wireless", "quantum-910", DeviceKind.Headset) : null;

    public override TimeSpan SilentKeep(VendorModel model) => TimeSpan.MaxValue;

    public override Task<VendorReading?> ReadAsync(VendorDevice device, CancellationToken token)
    {
        var collection = device.Collections.Values.Select(c => c.Collection).FirstOrDefault(c => c.UsagePage == 0xFF13 && c.Usage == 0x0001);
        if (collection is null)
            return Task.FromResult<VendorReading?>(null);
        lock (device.Memo)
        {
            if (device.Memo.GetValueOrDefault("listener") is not Task { IsCompleted: false })
                device.Memo["listener"] = Task.Run(() => ListenAsync(device, collection, token), token);
            return Task.FromResult(device.Memo.GetValueOrDefault("heard") as VendorReading);
        }
    }

    private static async Task ListenAsync(VendorDevice device, HidCollection collection, CancellationToken token)
    {
        using var link = HidLink.Open(collection);
        if (link is null)
            return;
        while (!token.IsCancellationRequested)
        {
            var started = DateTime.UtcNow;
            var r = await link.ReadAsync(r => r.Length > 1 && r[0] == 0x08 && r[1] <= 100, TimeSpan.FromMinutes(5), token);
            if (r is not null)
            {
                lock (device.Memo)
                    device.Memo["heard"] = new VendorReading(r[1], ChargeState.Discharging);
            }
            else if (DateTime.UtcNow - started < TimeSpan.FromSeconds(1))
            {
                return; // the read failed at once: the receiver went away
            }
        }
    }
}
