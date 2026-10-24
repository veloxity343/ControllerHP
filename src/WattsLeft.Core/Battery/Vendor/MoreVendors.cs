namespace WattsLeft.Battery.Vendor;

/// <summary>The NZXT Lift Elite Wireless mouse, on its dongle or its cable.</summary>
/// <remarks>
/// On the FFCA:0001 collection: output report 4E 02 81 00 B0 (4E framing, 02 the mouse behind the dongle,
/// 81 read a property, B0 its telemetry). The dongle first acknowledges with 4E .. E5, then answers
/// 4E .. 97: the percentage little-endian in bytes 17-18, and bit 0 of byte 22 set while it's on its
/// charging cable. While NZXT CAM is open the dongle stops answering, so the last level stays.
/// </remarks>
internal sealed class Nzxt : VendorProtocol
{
    public override string Name => "NZXT";

    public override IReadOnlyCollection<ushort> Vendors { get; } = [0x1E71];

    public override VendorModel? Identify(ushort vendor, ushort product) => product switch
    {
        0x2101 => new VendorModel("NZXT Lift Elite", "lift-elite", DeviceKind.Mouse),
        0x2129 => new VendorModel("NZXT Lift Elite", "lift-elite", DeviceKind.Mouse, Cable: true),
        _ => null,
    };

    public override async Task<VendorReading?> ReadAsync(VendorDevice device, CancellationToken token)
    {
        VendorReading? best = null;
        foreach (var (collection, _) in device.ByPreference)
        {
            if (collection.UsagePage != 0xFFCA || collection.Usage != 0x0001)
                continue;
            using var link = HidLink.Open(collection);
            if (link is null)
                continue;
            while (await link.ReadAsync(_ => true, TimeSpan.FromMilliseconds(5), token) is not null) { }
            var r = await link.AskAsync(link.OutputReport(0x4E, [0x02, 0x81, 0x00, 0xB0]),
                r => r.Length >= 27 && r[0] == 0x4E && r[2] == 0x97, TimeSpan.FromMilliseconds(1500), token);
            if (r is null || (r[17] | r[18] << 8) is not (int percent and <= 100))
                continue;
            var reading = new VendorReading(percent, (r[22] & 1) != 0 ? ChargeState.Charging : ChargeState.Discharging);
            if (best is null || reading.Power == ChargeState.Charging)
                best = reading;
        }
        return best;
    }
}

/// <summary>The Angry Miao AM Infinity 8K mouse on its receiver (and the AJAZZ mice with the same receiver).</summary>
/// <remarks>
/// The receiver only mirrors the mouse's charge after the vendor's status poll: feature report 00 F7 on the
/// FFFF:0002 control collection, then, a 2.4 GHz round trip later, feature report 05 reads 05 00 00
/// &lt;charge&gt;. A non-zero byte before the charge is junk from a reconnect; a charge of 0 means the link isn't
/// up yet, not empty. No charging flag.
/// </remarks>
internal sealed class AmInfinity : VendorProtocol
{
    public override string Name => "Angry Miao";

    public override IReadOnlyCollection<ushort> Vendors { get; } = [0x3151];

    public override VendorModel? Identify(ushort vendor, ushort product) =>
        product == 0x5007 ? new VendorModel("AM Infinity 8K Mouse", "infinity-8k", DeviceKind.Mouse) : null;

    public override async Task<VendorReading?> ReadAsync(VendorDevice device, CancellationToken token)
    {
        var collection = device.Collections.Values.Select(c => c.Collection).FirstOrDefault(c => c.UsagePage == 0xFFFF && c.Usage == 0x0002);
        if (collection is null || HidLink.Open(collection) is not { } link)
            return null;
        using (link)
        {
            if (!link.SetFeature(link.FeatureReport(0x00, [0xF7])))
                return null;
            await Task.Delay(TimeSpan.FromMilliseconds(30), token);
            if (link.GetFeature(0x05) is not { Length: >= 4 } r || r[0] != 0x05 || r[2] != 0x00 || r[3] is 0 or > 100)
                return null;
            return new VendorReading(r[3], ChargeState.Discharging);
        }
    }
}

/// <summary>The SteelSeries Arctis Nova Elite base station.</summary>
/// <remarks>
/// Output report 01 B0 to the first of interface 3's vendor collections that takes it (FFC0 first). The
/// answer comes on any interface 3 collection: either the direct 01 B0 reply, with the headset level in
/// byte 6, its power in byte 14 (01 offline, 02 charging on the cable) and charging in byte 15 (02), or
/// 07 B7 &lt;level&gt; &lt;spare battery&gt; &lt;charging: 02&gt; and 07 B5 .. .. &lt;power&gt; frames with the same codes.
/// </remarks>
internal sealed class SteelSeriesElite : VendorProtocol
{
    public override string Name => "SteelSeries Elite";

    public override IReadOnlyCollection<ushort> Vendors { get; } = [0x1038];

    public override VendorModel? Identify(ushort vendor, ushort product) =>
        product == 0x2244 ? new VendorModel("Arctis Nova Elite", "nova-elite", DeviceKind.Headset) : null;

    public override async Task<VendorReading?> ReadAsync(VendorDevice device, CancellationToken token)
    {
        var collections = device.Collections.Values.Select(c => c.Collection).Where(c => c.Interface == 3).ToList();
        var links = collections.Select(HidLink.Open).OfType<HidLink>().ToList();
        try
        {
            static bool Answer(byte[] r) => r.Length >= 16 && ((r[0] == 0x01 && r[1] == 0xB0) || (r[0] == 0x07 && r[1] is 0xB7 or 0xB5));
            var listening = links.Select(l => l.ReadAsync(Answer, TimeSpan.FromSeconds(1), token)).ToList();
            bool sent = false;
            foreach (var link in links.Where(l => l.Collection.UsagePage >= 0xFF00).OrderBy(l => l.Collection.UsagePage == 0xFFC0 ? 0 : 1))
            {
                if (await link.WriteAsync(link.OutputReport(0x01, [0xB0]), token))
                {
                    sent = true;
                    break;
                }
            }
            if (!sent)
                return null;
            foreach (var r in await Task.WhenAll(listening))
            {
                if (r is null)
                    continue;
                if (r[0] == 0x01)
                {
                    if (r[14] == 0x01 || r[6] > 100)
                        return r[14] == 0x01 ? VendorReading.SwitchedOff : null;
                    return new VendorReading(r[6], r[15] == 0x02 || r[14] == 0x02 ? ChargeState.Charging : ChargeState.Discharging);
                }
                if (r[1] == 0xB5 && r[4] == 0x01)
                    return VendorReading.SwitchedOff;
                if (r[1] == 0xB7 && r[2] <= 100)
                    return new VendorReading(r[2], r[4] == 0x02 ? ChargeState.Charging : ChargeState.Discharging);
            }
            return null;
        }
        finally
        {
            foreach (var link in links)
                link.Dispose();
        }
    }
}

/// <summary>The Logitech G PRO X 2 LIGHTSPEED headset on its receiver, which speaks Logitech's "Centurion"
/// protocol rather than HID++.</summary>
/// <remarks>
/// On the FFA0:0001 collection, 64-byte frames with report id 51. The headset is reached through the
/// receiver's bridge with the fixed request 51 08 00 03 1A 00 03 00 04 0A (the bridge, then the headset's
/// battery feature). Up to four frames come back: 51 03 is an acknowledgement, 51 05 with byte 6 at 00 says
/// the headset is off, and 51 0B with byte 8 at 04 is the battery: byte 10 the percentage, byte 12 at 02
/// charging.
/// </remarks>
internal sealed class LogitechCenturion : VendorProtocol
{
    public override string Name => "Logitech headset";

    public override IReadOnlyCollection<ushort> Vendors { get; } = [0x046D];

    public override VendorModel? Identify(ushort vendor, ushort product) =>
        product == 0x0AF7 ? new VendorModel("Logitech G PRO X 2 LIGHTSPEED", "pro-x-2", DeviceKind.Headset) : null;

    public override async Task<VendorReading?> ReadAsync(VendorDevice device, CancellationToken token)
    {
        var collection = device.Collections.Values.Select(c => c.Collection).FirstOrDefault(c => c.UsagePage == 0xFFA0 && c.Usage == 0x0001);
        if (collection is null || HidLink.Open(collection) is not { } link)
            return null;
        using (link)
        {
            if (!await link.WriteAsync(link.OutputReport(0x51, [0x08, 0x00, 0x03, 0x1A, 0x00, 0x03, 0x00, 0x04, 0x0A]), token))
                return null;
            for (int i = 0; i < 4; i++)
            {
                if (await link.ReadAsync(r => r.Length > 12 && r[0] == 0x51, TimeSpan.FromMilliseconds(1500), token) is not { } r)
                    return null;
                if (r[1] == 0x05 && r[6] == 0x00)
                    return VendorReading.SwitchedOff;
                if (r[1] == 0x0B && r[8] == 0x04 && r[10] <= 100)
                    return new VendorReading(r[10], r[12] == 0x02 ? ChargeState.Charging : ChargeState.Discharging);
            }
            return null;
        }
    }
}
