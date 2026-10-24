namespace WattsLeft.Battery.Vendor;

/// <summary>
/// HyperX wireless headsets on their dongles, beside NGENUITY: Cloud II Wireless, Cloud Alpha 2, Cloud III
/// Wireless, Cloud II Core Wireless and Cloud III S Wireless. Four families, each with its own report id
/// and layout; every reply must echo its request or carry its documented response id.
/// </summary>
internal sealed class HyperX : VendorProtocol
{
    public override string Name => "HyperX";

    private const ushort HyperXVendor = 0x03F0, KingstonVendor = 0x0951;
    public override IReadOnlyCollection<ushort> Vendors { get; } = [HyperXVendor, KingstonVendor];

    private enum Family { Cloud2, Alpha2, Cloud3, Cloud3S, AlphaWireless, Flight, KingstonCloud2 }

    private static readonly Dictionary<(ushort, ushort), (string Name, string Group, Family Family)> Kingston = new()
    {
        [(KingstonVendor, 0x16C4)] = ("HyperX Cloud Flight Wireless", "flight-16c4", Family.Flight),
        [(KingstonVendor, 0x1723)] = ("HyperX Cloud Flight Wireless", "flight-1723", Family.Flight),
        [(KingstonVendor, 0x1718)] = ("HyperX Cloud II Wireless", "cloud2-kingston", Family.KingstonCloud2),
    };

    private static readonly Dictionary<ushort, (string Name, string Group, Family Family)> Models = new()
    {
        [0x098D] = ("HyperX Cloud Alpha Wireless", "alpha-wireless", Family.AlphaWireless),
        [0x0696] = ("HyperX Cloud II Wireless", "cloud2-0696", Family.Cloud2),
        [0x018B] = ("HyperX Cloud II Wireless", "cloud2-018b", Family.Cloud2),
        [0x08BE] = ("HyperX Cloud Alpha 2", "alpha2", Family.Alpha2),
        [0x05B7] = ("HyperX Cloud III Wireless", "cloud3", Family.Cloud3),
        [0x0C9D] = ("HyperX Cloud III Wireless", "cloud3", Family.Cloud3),
        [0x0995] = ("HyperX Cloud II Core Wireless", "cloud2-core", Family.Cloud3),
        [0x0795] = ("HyperX Cloud II Core Wireless", "cloud2-core", Family.Cloud3),
        [0x02CC] = ("HyperX Cloud III S Wireless", "cloud3s", Family.Cloud3S),
        [0x06BE] = ("HyperX Cloud III S Wireless", "cloud3s", Family.Cloud3S),
    };

    public override VendorModel? Identify(ushort vendor, ushort product) =>
        (vendor == HyperXVendor ? Models.TryGetValue(product, out var m) : Kingston.TryGetValue((vendor, product), out m))
            ? new VendorModel(m.Name, m.Group, DeviceKind.Headset, Detail: m.Family)
            : null;

    private static readonly TimeSpan ReadWait = TimeSpan.FromSeconds(1), AlphaWait = TimeSpan.FromMilliseconds(1500);

    public override async Task<VendorReading?> ReadAsync(VendorDevice device, CancellationToken token)
    {
        var family = (Family)device.ByPreference.First().Model.Detail!;
        var collections = device.Collections.Values.Select(c => c.Collection).ToList();
        return family switch
        {
            Family.Cloud2 => await ReadCloud2Async(Find(collections, 0xFF90, 0x0303), token),
            Family.Alpha2 => await ReadAlpha2Async(Find(collections, 0xFF13, 0xFF00), token),
            Family.Cloud3 => await ReadCloud3Async(Find(collections, 0xFF13, 0x0001), token),
            Family.Cloud3S => await ReadCloud3SAsync(collections, token),
            _ => await ReadInterfaceZeroAsync(collections, family, token),
        };
    }

    /// <summary>
    /// The older families, on interface 0's collections (vendor pages first):
    /// Cloud Alpha Wireless, 21 BB &lt;cmd&gt; answered with that echo: 03 the link (byte 3 at 01 is off), 0C
    /// charging (byte 3 at 01), 0B the level in byte 3. Cloud Flight, 21 FF 05, answered with 15 or 20 bytes
    /// carrying millivolts in bytes 3-4; above 4107 mV it's charging. Kingston's Cloud II Wireless, report 06
    /// 00 02 00 9A 00 00 68 4A 8E 0A 00 00 00 BB &lt;cmd&gt;, answered 0B .. BB &lt;cmd&gt;: 02 the level in byte 7, 03
    /// charging in byte 4.
    /// </summary>
    private static async Task<VendorReading?> ReadInterfaceZeroAsync(List<HidCollection> collections, Family family, CancellationToken token)
    {
        foreach (var collection in collections.Where(c => c.Interface is 0 or -1).OrderBy(c => c.UsagePage >= 0xFF00 ? 0 : 1))
        {
            using var link = HidLink.Open(collection);
            if (link is null)
                continue;
            switch (family)
            {
                case Family.AlphaWireless:
                {
                    async Task<byte[]?> Ask(byte command) => await link.AskAsync(link.OutputReport(0x21, [0xBB, command]),
                        r => r.Length >= 4 && r[0] == 0x21 && r[1] == 0xBB && r[2] == command, TimeSpan.FromSeconds(2), token);
                    if (await Ask(0x03) is not { } connection)
                        continue;
                    if (connection[3] == 0x01)
                        return VendorReading.SwitchedOff;
                    if (await Ask(0x0C) is { } charging && charging[3] == 0x01)
                        return new VendorReading(null, ChargeState.Charging);
                    return await Ask(0x0B) is { } level && level[3] <= 100 ? new VendorReading(level[3], ChargeState.Discharging) : null;
                }
                case Family.Flight:
                {
                    var r = await link.AskAsync(link.OutputReport(0x21, [0xFF, 0x05]), r => r.Length is 15 or 20, ReadWait, token);
                    if (r is null)
                        continue;
                    int millivolts = r[3] << 8 | r[4];
                    return millivolts > 0x100B
                        ? new VendorReading(null, ChargeState.Charging)
                        : new VendorReading(FlightPercent(millivolts), ChargeState.Discharging);
                }
                default:
                {
                    async Task<byte[]?> Ask(byte command) => await link.AskAsync(
                        link.OutputReport(0x06, [0x00, 0x02, 0x00, 0x9A, 0x00, 0x00, 0x68, 0x4A, 0x8E, 0x0A, 0x00, 0x00, 0x00, 0xBB, command]),
                        r => r.Length >= 8 && r[0] == 0x0B && r[2] == 0xBB && r[3] == command, ReadWait, token);
                    if (await Ask(0x02) is not { } level || level[7] > 100)
                        continue;
                    var charging = await Ask(0x03);
                    return new VendorReading(level[7], Power(charging is not null && charging[4] == 1));
                }
            }
        }
        return null;
    }

    /// <summary>The Cloud Flight's charge from its voltage, along the curve fitted for it.</summary>
    private static int FlightPercent(int millivolts)
    {
        if (millivolts <= 3648)
            return (int)Math.Round(0.00125 * millivolts);
        if (millivolts > 3975)
            return 100;
        double v = millivolts;
        double percent = 0.00000002547505 * Math.Pow(v, 4) - 0.0003900299 * Math.Pow(v, 3) + 2.238321 * v * v - 5706.256 * v + 5452299.0;
        return (int)Math.Clamp(Math.Round(percent), 0, 100);
    }

    /// <summary>The collection with this usage: these dongles carry several on one interface.</summary>
    private static HidCollection? Find(List<HidCollection> collections, ushort page, ushort usage) =>
        collections.FirstOrDefault(c => c.UsagePage == page && c.Usage == usage);

    private static ChargeState Power(bool charging) => charging ? ChargeState.Charging : ChargeState.Discharging;

    /// <summary>06 FF BB &lt;cmd&gt; (52 bytes); the reply echoes those four bytes. 02: percent in byte 7. 03: byte 4 is 1 charging.</summary>
    private static async Task<VendorReading?> ReadCloud2Async(HidCollection? collection, CancellationToken token)
    {
        if (collection is null || HidLink.Open(collection) is not { } link)
            return null;
        using (link)
        {
            async Task<byte[]?> Ask(byte command) => await link.AskAsync(link.OutputReport(0x06, [0xFF, 0xBB, command]),
                r => r.Length >= 8 && r[0] == 0x06 && r[1] == 0xFF && r[2] == 0xBB && r[3] == command, ReadWait, token);

            if (await Ask(0x02) is not { } level || level[7] > 100)
                return null;
            var charging = await Ask(0x03);
            return new VendorReading(level[7], Power(charging is not null && charging[4] == 1));
        }
    }

    /// <summary>50 02 (64 bytes), answered by 51 02 &lt;percent&gt; .. .. .. &lt;flags, bit 7 charging&gt;.</summary>
    private static async Task<VendorReading?> ReadAlpha2Async(HidCollection? collection, CancellationToken token)
    {
        if (collection is null || HidLink.Open(collection) is not { } link)
            return null;
        using (link)
        {
            // NGENUITY keeps the channel busy: drop what's queued, then wait for the answer itself.
            while (await link.ReadAsync(_ => true, TimeSpan.FromMilliseconds(5), token) is not null) { }
            var r = await link.AskAsync(link.OutputReport(0x50, [0x02]), r => r.Length >= 7 && r[0] == 0x51 && r[1] == 0x02,
                AlphaWait, token);
            if (r is null || r[2] > 100)
                return null;
            return new VendorReading(r[2], Power((r[6] & 0x80) != 0));
        }
    }

    /// <summary>
    /// 66 &lt;cmd&gt; (62 bytes), as an output report or, on dongles that refuse that, a feature report. The reply
    /// starts 66, then the command or its response id. 89/0D battery: byte 4, if bytes 2 or 3 aren't both 0.
    /// 8A/0C charging: byte 2, 1 charging, 2 charged on the cable.
    /// </summary>
    private static async Task<VendorReading?> ReadCloud3Async(HidCollection? collection, CancellationToken token)
    {
        if (collection is null || HidLink.Open(collection) is not { } link)
            return null;
        using (link)
        {
            async Task<byte[]?> Ask(byte command, byte response)
            {
                bool Match(byte[] r) => r.Length >= 5 && r[0] == 0x66 && (r[1] == command || r[1] == response);
                var request = link.OutputReport(0x66, [command]);
                if (await link.WriteAsync(request, token))
                    return await link.ReadAsync(Match, ReadWait, token);
                return link.SetFeature(link.FeatureReport(0x66, [command])) ? await link.ReadAsync(Match, ReadWait, token) : null;
            }

            if (await Ask(0x89, 0x0D) is not { } level || (level[2] == 0 && level[3] == 0) || level[4] > 100)
                return null;
            var charging = await Ask(0x8A, 0x0C);
            return new VendorReading(level[4], Power(charging is not null && charging[2] == 1));
        }
    }

    /// <summary>
    /// 0C 02 03 01 00 &lt;cmd&gt; (64 bytes; 01 asks for a value), sent as an output report to the first of the
    /// dongle's collections that takes report 0C. The answer, 0C .. .. .. .. &lt;cmd&gt; &lt;value&gt;, can arrive on
    /// any of its collections, so all are read. 06 battery (FF no value); 48 charging, 1 charging, 2 charged.
    /// </summary>
    private static async Task<VendorReading?> ReadCloud3SAsync(List<HidCollection> collections, CancellationToken token)
    {
        var links = collections.OrderBy(c => c.UsagePage >= 0xFF00 ? 0 : 1).Select(HidLink.Open).OfType<HidLink>().ToList();
        try
        {
            async Task<byte?> Ask(byte command)
            {
                bool Match(byte[] r) => r.Length >= 7 && r[0] == 0x0C && r[5] == command && r[6] != 0xFF;
                var listening = links.Select(l => l.ReadAsync(Match, ReadWait, token)).ToList();
                bool sent = false;
                foreach (var link in links)
                {
                    if (await link.WriteAsync(link.OutputReport(0x0C, [0x02, 0x03, 0x01, 0x00, command]), token))
                    {
                        sent = true;
                        break;
                    }
                }
                if (!sent)
                    return null;
                foreach (var answer in await Task.WhenAll(listening))
                {
                    if (answer is not null)
                        return answer[6];
                }
                return null;
            }

            if (await Ask(0x06) is not (byte level and <= 100))
                return null;
            byte? charging = await Ask(0x48);
            return new VendorReading(level, Power(charging == 1));
        }
        finally
        {
            foreach (var link in links)
                link.Dispose();
        }
    }
}
