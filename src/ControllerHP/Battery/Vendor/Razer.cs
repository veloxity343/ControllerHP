namespace ControllerHP.Battery.Vendor;

/// <summary>
/// Razer wireless mice and keyboards, on their HyperSpeed receivers, docks or cables, beside Synapse.
/// </summary>
/// <remarks>
/// Razer's control messages are a 90-byte feature report with no report id: [0] status (00 in a request;
/// in a reply 01 busy, 02 done, 04 the device didn't answer, 05 not supported), [1] a transaction id that
/// depends on the model, [5] data size, [6] command class, [7] command id, [8..] arguments, [88] the XOR of
/// bytes 2 to 87. Class 07 command 80 reads the battery (argument 1, 0..255); 07 84 reads charging. Only
/// those two are sent. Synapse uses the same collection, so a reply to another command is skipped, and the
/// request is sent again a couple of times if replies keep belonging to someone else. Razer's headsets need
/// a mode switched before they'll answer, which is a change to the device, so they're left out.
/// </remarks>
internal sealed class Razer : VendorProtocol
{
    public override string Name => "Razer";

    private const ushort RazerVendor = 0x1532;
    public override IReadOnlyCollection<ushort> Vendors { get; } = [RazerVendor];

    /// <summary>The report id byte plus the 90-byte message.</summary>
    private const int FeatureLength = 91;

    private const byte Busy = 0x01, Done = 0x02, NoAnswer = 0x04; // and 05, not supported: the next transaction id is tried
    private static readonly byte[] TransactionIds = [0x1F, 0x3F, 0xFF, 0x9F, 0x08];

    private static readonly TimeSpan AnswerWait = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan FirstWait = TimeSpan.FromMilliseconds(60), NextWait = TimeSpan.FromMilliseconds(80);
    private const int Resends = 3;

    /// <summary>Product id: name, the transaction id it answers on, and (keyboards) the interface that takes commands.</summary>
    private static readonly Dictionary<ushort, (string Name, byte TransactionId, int? Interface)> Models = new()
    {
        [0x00A4] = ("Razer Mouse Dock Pro", 0x1F, null),
        [0x0290] = ("Razer DeathStalker V2 Pro", 0x9F, 2), [0x0292] = ("Razer DeathStalker V2 Pro", 0x1F, 3),
        [0x0296] = ("Razer DeathStalker V2 Pro TKL", 0x9F, 2), [0x0298] = ("Razer DeathStalker V2 Pro TKL", 0x1F, 3),
        [0x0271] = ("Razer BlackWidow V3 Mini HyperSpeed", 0x9F, 3), [0x0258] = ("Razer BlackWidow V3 Mini HyperSpeed", 0x1F, 3),
        [0x02BA] = ("Razer BlackWidow V4 Mini HyperSpeed", 0x9F, 3), [0x02B9] = ("Razer BlackWidow V4 Mini HyperSpeed", 0x1F, 3),
        [0x02D5] = ("Razer BlackWidow V4 Tenkeyless HyperSpeed", 0x9F, 2), [0x02D7] = ("Razer BlackWidow V4 Tenkeyless HyperSpeed", 0x1F, 3),
        [0x025A] = ("Razer BlackWidow V3 Pro", 0x3F, null), [0x025C] = ("Razer BlackWidow V3 Pro", 0x9F, null),
        [0x001F] = ("Razer Naga Epic", 0xFF, null),
        [0x0024] = ("Razer Mamba (2012)", 0xFF, null), [0x0025] = ("Razer Mamba (2012)", 0xFF, null),
        [0x0032] = ("Razer Ouroboros", 0xFF, null),
        [0x003E] = ("Razer Naga Epic Chroma", 0xFF, null), [0x003F] = ("Razer Naga Epic Chroma", 0xFF, null),
        [0x0044] = ("Razer Mamba", 0xFF, null), [0x0045] = ("Razer Mamba", 0xFF, null),
        [0x0059] = ("Razer Lancehead", 0x3F, null), [0x005A] = ("Razer Lancehead", 0x3F, null),
        [0x0062] = ("Razer Atheris", 0x1F, null),
        [0x006F] = ("Razer Lancehead Wireless", 0x1F, null), [0x0070] = ("Razer Lancehead Wireless", 0x1F, null),
        [0x0072] = ("Razer Mamba Wireless", 0x3F, null), [0x0073] = ("Razer Mamba Wireless", 0x3F, null),
        [0x0077] = ("Razer Pro Click", 0x1F, null), [0x0080] = ("Razer Pro Click", 0x1F, null),
        [0x007A] = ("Razer Viper Ultimate", 0xFF, null), [0x007B] = ("Razer Viper Ultimate", 0xFF, null),
        [0x007C] = ("Razer DeathAdder V2 Pro", 0x3F, null), [0x007D] = ("Razer DeathAdder V2 Pro", 0x3F, null),
        [0x0083] = ("Razer Basilisk X HyperSpeed", 0xFF, null),
        [0x0086] = ("Razer Basilisk Ultimate", 0x1F, null), [0x0088] = ("Razer Basilisk Ultimate", 0x1F, null),
        [0x008F] = ("Razer Naga Pro", 0x1F, null), [0x0090] = ("Razer Naga Pro", 0x1F, null),
        [0x0094] = ("Razer Orochi V2", 0x1F, null),
        [0x009A] = ("Razer Pro Click Mini", 0x1F, null),
        [0x009C] = ("Razer DeathAdder V2 X HyperSpeed", 0x1F, null),
        [0x009E] = ("Razer Viper Mini SE", 0x1F, null), [0x009F] = ("Razer Viper Mini SE", 0x1F, null),
        [0x00A5] = ("Razer Viper V2 Pro", 0x1F, null), [0x00A6] = ("Razer Viper V2 Pro", 0x1F, null),
        [0x00A7] = ("Razer Naga V2 Pro", 0x1F, null), [0x00A8] = ("Razer Naga V2 Pro", 0x1F, null),
        [0x00AA] = ("Razer Basilisk V3 Pro", 0x1F, null), [0x00AB] = ("Razer Basilisk V3 Pro", 0x1F, null),
        [0x00AF] = ("Razer Cobra Pro", 0x1F, null), [0x00B0] = ("Razer Cobra Pro", 0x1F, null),
        [0x00B3] = ("Razer HyperPolling Dongle", 0x1F, null),
        [0x00B4] = ("Razer Naga V2 HyperSpeed", 0x1F, null),
        [0x00B6] = ("Razer DeathAdder V3 Pro", 0x1F, null), [0x00B7] = ("Razer DeathAdder V3 Pro", 0x1F, null),
        [0x00B8] = ("Razer Viper V3 HyperSpeed", 0x1F, null),
        [0x00B9] = ("Razer Basilisk V3 X HyperSpeed", 0x1F, null),
        [0x00BE] = ("Razer DeathAdder V4 Pro", 0x1F, null), [0x00BF] = ("Razer DeathAdder V4 Pro", 0x1F, null),
        [0x00C0] = ("Razer Viper V3 Pro", 0x1F, null), [0x00C1] = ("Razer Viper V3 Pro", 0x1F, null),
        [0x00C2] = ("Razer DeathAdder V3 Pro", 0x1F, null), [0x00C3] = ("Razer DeathAdder V3 Pro", 0x1F, null),
        [0x00C4] = ("Razer DeathAdder V3 HyperSpeed", 0x1F, null), [0x00C5] = ("Razer DeathAdder V3 HyperSpeed", 0x1F, null),
        [0x00C7] = ("Razer Pro Click V2 Vertical Edition", 0x1F, null), [0x00C8] = ("Razer Pro Click V2 Vertical Edition", 0x1F, null),
        [0x00CC] = ("Razer Basilisk V3 Pro 35K", 0x1F, null), [0x00CD] = ("Razer Basilisk V3 Pro 35K", 0x1F, null),
        [0x00D0] = ("Razer Pro Click V2", 0x1F, null), [0x00D1] = ("Razer Pro Click V2", 0x1F, null),
        [0x00D3] = ("Razer Basilisk Mobile", 0x1F, null), [0x00D4] = ("Razer Basilisk Mobile", 0x1F, null),
        [0x00D6] = ("Razer Basilisk V3 Pro 35K Phantom Green", 0x1F, null), [0x00D7] = ("Razer Basilisk V3 Pro 35K Phantom Green", 0x1F, null),
    };

    public override VendorModel? Identify(ushort vendor, ushort product)
    {
        if (!Models.TryGetValue(product, out var m))
            return null;
        // A model's receiver and cable ids share its name, so the name makes them one device.
        var kind = product >= 0x0200 ? DeviceKind.Keyboard : product == 0x00A4 ? DeviceKind.Other : DeviceKind.Mouse;
        return new VendorModel(m.Name, m.Name.ToLowerInvariant().Replace(' ', '-'), kind, Detail: product);
    }

    private sealed record Answer(byte Status, byte? Value);

    public override async Task<VendorReading?> ReadAsync(VendorDevice device, CancellationToken token)
    {
        // Where it last answered, then the collections with Razer's message size: a keyboard's command
        // interface first, then the main and vendor pages.
        var candidates = device.Collections.Values
            .Where(c => c.Collection.FeatureLength == FeatureLength)
            .OrderBy(c => device.Memo.GetValueOrDefault("path") as string == c.Collection.Path ? 0 : 1)
            .ThenBy(c => Models[(ushort)c.Model.Detail!].Interface == c.Collection.Interface ? 0 : 1)
            .ThenBy(c => c.Collection.UsagePage is 0x0001 or 0xFF00 ? 0 : 1)
            .ThenBy(c => c.Collection.Interface)
            .ToList();

        bool asleep = false;
        foreach (var (collection, model) in candidates)
        {
            var known = Models[(ushort)model.Detail!];
            byte[] ids = device.Memo.GetValueOrDefault("tid") is byte remembered
                ? [remembered]
                : [known.TransactionId, .. TransactionIds.Where(t => t != known.TransactionId)];
            using var link = HidLink.Open(collection);
            if (link is null)
                continue;
            foreach (byte tid in ids)
            {
                var battery = await QueryAsync(link, tid, 0x07, 0x80, token);
                if (battery is null)
                    break; // the collection doesn't take feature reports
                if (battery.Status == NoAnswer)
                {
                    // The receiver is there but the device didn't answer: asleep or switched off.
                    device.Memo["path"] = collection.Path;
                    device.Memo["tid"] = tid;
                    asleep = true;
                    break;
                }
                if (battery.Status != Done || battery.Value is not byte raw)
                    continue;
                device.Memo["path"] = collection.Path;
                device.Memo["tid"] = tid;
                var charging = await QueryAsync(link, tid, 0x07, 0x84, token);
                bool isCharging = charging is { Status: Done, Value: > 0 };
                return new VendorReading((int)Math.Round(raw / 255.0 * 100), isCharging ? ChargeState.Charging : ChargeState.Discharging);
            }
            if (asleep)
                break;
        }
        return null;
    }

    /// <summary>
    /// One command and its answer. Null if the collection refuses feature reports; an answer with a null
    /// value if the replies were for another app's commands.
    /// </summary>
    private static async Task<Answer?> QueryAsync(HidLink link, byte transactionId, byte commandClass, byte commandId,
        CancellationToken token)
    {
        var message = new byte[90];
        message[1] = transactionId;
        message[5] = 0x02;
        message[6] = commandClass;
        message[7] = commandId;
        byte crc = 0;
        for (int i = 2; i < 88; i++)
            crc ^= message[i];
        message[88] = crc;
        var request = link.FeatureReport(0x00, message);
        if (!link.SetFeature(request))
            return null;

        int sends = 1, foreign = 0;
        var deadline = DateTime.UtcNow + AnswerWait;
        await Task.Delay(FirstWait, token);
        while (true)
        {
            if (link.GetFeature(0x00) is not { Length: >= FeatureLength } reply)
                return null;
            var data = reply[1..FeatureLength];
            byte status = data[0];
            bool ours = data[6] == commandClass && data[7] == commandId;
            bool someoneElses = status == Done && !ours;
            if (DateTime.UtcNow < deadline && (status == Busy || someoneElses))
            {
                if (someoneElses && ++foreign % 4 == 0 && sends < Resends)
                {
                    // Another app's request may have replaced ours before the device read it: ask again.
                    if (!link.SetFeature(request))
                        return null;
                    sends++;
                }
                await Task.Delay(NextWait, token);
                continue;
            }
            return new Answer(status, ours && status == Done ? data[9] : null);
        }
    }
}

/// <summary>The Razer BlackShark V2 Pro (2020) headset, on its receiver.</summary>
/// <remarks>
/// Not Razer's usual message: a 64-byte feature report FF 0A 00 FD 04 12 F1 02 05 on the FF00:0001
/// collection, read back as feature report FF. A reply starts FF 0F 05 FE 12 04 1F 08 05, then carries the
/// power state in byte 11, millivolts big-endian in bytes 12-13 (0 when the headset is off) and the level in
/// byte 14. A reply starting FF 01 00 means the headset is off. While charging (state 09) the level byte
/// jumps about, so only charging is shown then; states 01, 02, 03 and 06 (charged) are on battery.
/// </remarks>
internal sealed class RazerBlackShark2020 : VendorProtocol
{
    public override string Name => "Razer headset";

    public override IReadOnlyCollection<ushort> Vendors { get; } = [0x1532];

    public override VendorModel? Identify(ushort vendor, ushort product) =>
        product == 0x0528 ? new VendorModel("Razer BlackShark V2 Pro (2020)", "blackshark-v2-pro-2020", DeviceKind.Headset) : null;

    private static readonly byte[] Answered = [0xFF, 0x0F, 0x05, 0xFE, 0x12, 0x04, 0x1F, 0x08, 0x05];
    private static readonly byte[] NoHeadset = [0xFF, 0x01, 0x00, 0xFE, 0x12, 0x04, 0x1F, 0x08, 0x05];

    public override async Task<VendorReading?> ReadAsync(VendorDevice device, CancellationToken token)
    {
        var collection = device.Collections.Values.Select(c => c.Collection).FirstOrDefault(c => c.UsagePage == 0xFF00 && c.Usage == 0x0001);
        if (collection is null || HidLink.Open(collection) is not { } link)
            return null;
        using (link)
        {
            if (!link.SetFeature(link.FeatureReport(0xFF, [0x0A, 0x00, 0xFD, 0x04, 0x12, 0xF1, 0x02, 0x05])))
                return null;
            byte[]? last = null;
            for (int i = 0; i < 5; i++)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(100), token);
                last = link.GetFeature(0xFF);
                if (last is { Length: >= 15 } r && r.AsSpan(0, Answered.Length).SequenceEqual(Answered))
                {
                    if ((r[12] << 8 | r[13]) == 0)
                        return VendorReading.SwitchedOff;
                    if (r[11] == 0x09)
                        return new VendorReading(null, ChargeState.Charging);
                    return r[14] <= 100 ? new VendorReading(r[14], ChargeState.Discharging) : null;
                }
            }
            return last is { Length: >= 9 } l && l.AsSpan(0, NoHeadset.Length).SequenceEqual(NoHeadset) ? VendorReading.SwitchedOff : null;
        }
    }
}
