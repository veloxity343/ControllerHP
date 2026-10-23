namespace ControllerHP.Battery.Vendor;

/// <summary>
/// SteelSeries wireless headsets (Arctis Nova, Arctis 7+, GameBuds, older Arctis) and mice (Rival 3, Aerox),
/// on their dongles, beside SteelSeries GG.
/// </summary>
/// <remarks>
/// Most go through the dongle's configuration collection, usage FFC0:0001 (on interface 3 when there's a
/// choice). Headsets: output report 00 B0, answered by an input report starting B0, laid out by family.
/// Rival 3 mice: 00 AA 01, answered with an AA echo then the level; Aerox mice: 00 D2, answered with a D2
/// echo and a level byte whose bit 7 is charging. Older Arctis headsets have requests of their own on a
/// given interface. Every reply has to carry its echo or a documented state, so other traffic on the
/// collection is never read as a level.
/// </remarks>
internal sealed class SteelSeries : VendorProtocol
{
    public override string Name => "SteelSeries";

    private const ushort SteelSeriesVendor = 0x1038;
    public override IReadOnlyCollection<ushort> Vendors { get; } = [SteelSeriesVendor];

    private static readonly TimeSpan ReadWait = TimeSpan.FromSeconds(1);

    /// <summary>How a model is asked, and how its answer reads.</summary>
    private enum Family
    {
        Nova7, Nova7Steps, Nova5, Arctis7Plus, GameBuds,                // B0 headsets
        Rival3, Aerox,                                                  // mice
        Arctis1, Arctis7Linked, Arctis7, Arctis9, ProWireless, NovaPro, // older headsets
    }

    private sealed record Model(Family Family, int[]? Interfaces = null, ushort? UsagePage = null);

    private static readonly Dictionary<ushort, (string Name, DeviceKind Kind, Model Model)> Models = new()
    {
        [0x22A1] = ("Arctis Nova 7", DeviceKind.Headset, new(Family.Nova7)),
        [0x2202] = ("Arctis Nova 7", DeviceKind.Headset, new(Family.Nova7Steps)),
        [0x227E] = ("Arctis Nova 7 Gen 2", DeviceKind.Headset, new(Family.Nova7)),
        [0x2206] = ("Arctis Nova 7x", DeviceKind.Headset, new(Family.Nova7Steps)),
        [0x2258] = ("Arctis Nova 7x", DeviceKind.Headset, new(Family.Nova7)),
        [0x229E] = ("Arctis Nova 7x", DeviceKind.Headset, new(Family.Nova7)),
        [0x22AD] = ("Arctis Nova 7x", DeviceKind.Headset, new(Family.Nova7)),
        [0x22A4] = ("Arctis Nova 7X", DeviceKind.Headset, new(Family.Nova7Steps)),
        [0x22A5] = ("Arctis Nova 7X", DeviceKind.Headset, new(Family.Nova7)),
        [0x223A] = ("Arctis Nova 7 Diablo IV", DeviceKind.Headset, new(Family.Nova7Steps)),
        [0x22A9] = ("Arctis Nova 7 Diablo IV", DeviceKind.Headset, new(Family.Nova7)),
        [0x227A] = ("Arctis Nova 7 WoW Edition", DeviceKind.Headset, new(Family.Nova7Steps)),
        [0x2232] = ("Arctis Nova 5", DeviceKind.Headset, new(Family.Nova5)),
        [0x2253] = ("Arctis Nova 5X", DeviceKind.Headset, new(Family.Nova5)),
        [0x220A] = ("Arctis Nova 7P", DeviceKind.Headset, new(Family.Nova7Steps)),
        [0x22A7] = ("Arctis Nova 7P", DeviceKind.Headset, new(Family.Nova7)),
        [0x2298] = ("Arctis Nova 7P", DeviceKind.Headset, new(Family.Nova7)),
        [0x2269] = ("Arctis Nova 3P Wireless", DeviceKind.Headset, new(Family.Nova5)),
        [0x226D] = ("Arctis Nova 3X Wireless", DeviceKind.Headset, new(Family.Nova5)),
        [0x220E] = ("Arctis 7+", DeviceKind.Headset, new(Family.Arctis7Plus)),
        [0x2212] = ("Arctis 7+ PS5", DeviceKind.Headset, new(Family.Arctis7Plus)),
        [0x2216] = ("Arctis 7+ Xbox", DeviceKind.Headset, new(Family.Arctis7Plus)),
        [0x2236] = ("Arctis 7+ Destiny", DeviceKind.Headset, new(Family.Arctis7Plus)),
        [0x230A] = ("Arctis GameBuds", DeviceKind.Headset, new(Family.GameBuds)),
        [0x2317] = ("Arctis GameBuds X", DeviceKind.Headset, new(Family.GameBuds)),

        [0x1830] = ("SteelSeries Rival 3 Wireless", DeviceKind.Mouse, new(Family.Rival3)),
        [0x1872] = ("SteelSeries Rival 3 Wireless Gen 2", DeviceKind.Mouse, new(Family.Rival3)),
        [0x1838] = ("SteelSeries Aerox 3 Wireless", DeviceKind.Mouse, new(Family.Aerox)),
        [0x1878] = ("SteelSeries Aerox 3 Wireless CS2 Dragon Lore", DeviceKind.Mouse, new(Family.Aerox)),
        [0x1852] = ("SteelSeries Aerox 5 Wireless", DeviceKind.Mouse, new(Family.Aerox)),
        [0x185C] = ("SteelSeries Aerox 5 Wireless Destiny 2 Edition", DeviceKind.Mouse, new(Family.Aerox)),
        [0x1860] = ("SteelSeries Aerox 5 Wireless Diablo IV Edition", DeviceKind.Mouse, new(Family.Aerox)),
        [0x1858] = ("SteelSeries Aerox 9 Wireless", DeviceKind.Mouse, new(Family.Aerox)),
        [0x1874] = ("SteelSeries Aerox 9 Wireless WOW Edition", DeviceKind.Mouse, new(Family.Aerox)),

        [0x12B3] = ("Arctis 1 Wireless", DeviceKind.Headset, new(Family.Arctis1, [3], 0xFF43)),
        [0x12B6] = ("Arctis 1 Wireless Xbox", DeviceKind.Headset, new(Family.Arctis1, [3], 0xFF43)),
        [0x12D7] = ("Arctis 7X", DeviceKind.Headset, new(Family.Arctis1, [3], 0xFF43)),
        [0x12D5] = ("Arctis 7P", DeviceKind.Headset, new(Family.Arctis1, [3], 0xFF43)),
        [0x12AD] = ("Arctis 7", DeviceKind.Headset, new(Family.Arctis7Linked, [5])),
        [0x1260] = ("Arctis 7", DeviceKind.Headset, new(Family.Arctis7, [5])),
        [0x1252] = ("Arctis Pro Wireless 2019", DeviceKind.Headset, new(Family.Arctis7, [5])),
        [0x12C2] = ("Arctis 9", DeviceKind.Headset, new(Family.Arctis9, [0])),
        [0x1290] = ("Arctis Pro Wireless", DeviceKind.Headset, new(Family.ProWireless, [0])),
        [0x12E0] = ("Arctis Nova Pro Wireless", DeviceKind.Headset, new(Family.NovaPro, [3, 4])),
        [0x12E5] = ("Arctis Nova Pro Wireless X", DeviceKind.Headset, new(Family.NovaPro, [3, 4])),
    };

    public override VendorModel? Identify(ushort vendor, ushort product) =>
        Models.TryGetValue(product, out var m) ? new VendorModel(m.Name, $"{product:X4}", m.Kind, Detail: m.Model) : null;

    public override async Task<VendorReading?> ReadAsync(VendorDevice device, CancellationToken token)
    {
        var model = (Model)device.ByPreference.First().Model.Detail!;
        foreach (var collection in Candidates(device, model))
        {
            using var link = HidLink.Open(collection);
            if (link is null)
                continue;
            var reading = await AskAsync(link, model.Family, token);
            if (reading is not null)
                return reading;
        }
        return null;
    }

    /// <summary>The collections a model may be asked on.</summary>
    private static IEnumerable<HidCollection> Candidates(VendorDevice device, Model model)
    {
        var all = device.Collections.Values.Select(c => c.Collection);
        if (model.Interfaces is null)
        {
            // The configuration collection, on interface 3 when it appears more than once.
            return all.Where(c => c.UsagePage == 0xFFC0 && c.Usage == 0x0001).OrderBy(c => c.Interface == 3 ? 0 : 1);
        }
        // Older headsets: a given interface, on a given usage page or any vendor-defined one.
        return all.Where(c => model.Interfaces.Contains(c.Interface) &&
                              (model.UsagePage is ushort page ? c.UsagePage == page : c.UsagePage >= 0xFF00));
    }

    private static async Task<VendorReading?> AskAsync(HidLink link, Family family, CancellationToken token)
    {
        async Task<byte[]?> Ask(byte reportId, byte[] payload, Func<byte[], bool> accept) =>
            await link.AskAsync(link.OutputReport(reportId, payload), accept, ReadWait, token);

        // A coarse level (a few steps) is shown as that level, not as an exact percentage.
        static VendorReading Read(int percent, bool charging, BatteryLevel? level = null) =>
            new(level is null ? Math.Clamp(percent, 0, 100) : null, charging ? ChargeState.Charging : ChargeState.Discharging, level);

        switch (family)
        {
            case Family.Nova7 or Family.Nova7Steps or Family.Nova5 or Family.Arctis7Plus or Family.GameBuds:
            {
                var r = await Ask(0x00, [0xB0], r => r.Length >= 4 && r[0] == 0xB0);
                if (r is null)
                    return null;
                switch (family)
                {
                    case Family.Nova7 or Family.Nova7Steps:
                        // B0 <link> <level> <status>: link 03 connected (the level byte keeps its old value
                        // otherwise); status 00 off, 01 or 02 charging, 03 on battery. Older firmware: 0..4.
                        if (r[1] != 0x03 || r[3] == 0x00)
                            return VendorReading.SwitchedOff;
                        return family == Family.Nova7Steps
                            ? Read(Math.Min((int)r[2], 4) * 25, r[3] is 0x01 or 0x02, Steps(Math.Min((int)r[2], 4)))
                            : Read(r[2], r[3] is 0x01 or 0x02);
                    case Family.Nova5:
                        // B0 <status> .. <level> <charging>: status 02 off.
                        if (r.Length < 5 || r[1] == 0x02)
                            return VendorReading.SwitchedOff;
                        return Read(r[3], r[4] == 0x01);
                    case Family.Arctis7Plus:
                        // B0 <status> <level 0..4> <charging>: status 01 off.
                        if (r[1] == 0x01)
                            return VendorReading.SwitchedOff;
                        return Read(Math.Min((int)r[2], 4) * 25, r[3] == 0x01, Steps(Math.Min((int)r[2], 4)));
                    default:
                        // B0 .. .. <left status> <right status> <left %> <right %>: a bud out of the case has
                        // status 03; one in the case reads 0%, so only the buds out are counted.
                        if (r.Length < 7)
                            return null;
                        int[] levels = [.. new[] { 0, 1 }.Where(i => r[3 + i] == 0x03).Select(i => (int)r[5 + i])];
                        return levels.Length == 0 ? VendorReading.SwitchedOff : Read(levels.Min(), false);
                }
            }

            case Family.Rival3:
            {
                // AA <level> .. <charging>, or (as another reference has it) <level> .. <charging> with no echo,
                // accepted only when the charging byte is 0 or 1.
                var r = await Ask(0x00, Pad([0xAA, 0x01], 63), r =>
                    (r.Length >= 4 && r[0] == 0xAA && r[1] <= 100) || (r.Length >= 3 && r[0] <= 100 && r[2] is 0 or 1));
                if (r is null)
                    return null;
                return r[0] == 0xAA ? Read(r[1], r[3] != 0) : Read(r[0], r[2] == 1);
            }

            case Family.Aerox:
            {
                // D2 <level byte>: bit 7 charging; below it a step 1..21 (21 full), or above 21 a percentage.
                // 0 means asleep or off, not empty.
                var r = await Ask(0x00, Pad([0xD2], 63), r => r.Length >= 2 && r[0] == 0xD2);
                if (r is null)
                    return null;
                int value = r[1] & 0x7F;
                if (value == 0)
                    return null;
                return Read(value > 21 ? value : (value - 1) * 5, (r[1] & 0x80) != 0);
            }

            case Family.Arctis1:
            {
                // 06 12 -> 06 12 <status> <level>: status 01 off; no charging flag.
                var r = await Ask(0x06, [0x12], r => r.Length >= 4 && r[0] == 0x06 && r[1] == 0x12);
                if (r is null)
                    return null;
                return r[2] == 0x01 ? VendorReading.SwitchedOff : Read(r[3], false);
            }

            case Family.Arctis7Linked or Family.Arctis7:
            {
                if (family == Family.Arctis7Linked)
                {
                    // 06 14 -> 06 14 <link>: 03 connected.
                    var link14 = await Ask(0x06, [0x14], r => r.Length >= 3 && r[0] == 0x06 && r[1] == 0x14);
                    if (link14 is null)
                        return null;
                    if (link14[2] != 0x03)
                        return VendorReading.SwitchedOff;
                }
                // 06 18 -> 06 18 <level>; it reads 0 while the headset is off.
                var r = await Ask(0x06, [0x18], r => r.Length >= 3 && r[0] == 0x06 && r[1] == 0x18);
                if (r is null)
                    return null;
                return r[2] == 0 ? VendorReading.SwitchedOff : Read(r[2], false);
            }

            case Family.Arctis9:
            {
                // 00 20 -> AA 01 .. <raw level> <charging>; anything else (55 ..) means off. Raw 0x64..0x9A is 0..100%.
                var r = await Ask(0x00, [0x20], r => r.Length >= 5 && r[0] is 0xAA or 0x55);
                if (r is null)
                    return null;
                if (r[0] != 0xAA || r[1] != 0x01)
                    return VendorReading.SwitchedOff;
                int raw = Math.Clamp((int)r[3], 0x64, 0x9A);
                return Read((raw - 0x64) * 100 / (0x9A - 0x64), r[4] == 0x01);
            }

            case Family.ProWireless:
            {
                // 41 AA -> <state>: 02 off, 04 on; then 40 AA -> <level 0..4>. Neither reply echoes its request.
                var state = await Ask(0x41, Pad([0xAA], 30), r => r.Length >= 1 && r[0] is 0x02 or 0x04);
                if (state is null)
                    return null;
                if (state[0] == 0x02)
                    return VendorReading.SwitchedOff;
                var level = await Ask(0x40, Pad([0xAA], 30), r => r.Length >= 1 && r[0] <= 4);
                return level is null ? null : Read(level[0] * 25, false, Steps(level[0]));
            }

            case Family.NovaPro:
            {
                // 06 B0 -> a nine-step level (0..8) in byte 6, the headset's state in byte 15: 01 off, 02 charging,
                // 08 on battery. No echo, so only those states are accepted.
                var r = await Ask(0x06, [0xB0], r => r.Length >= 16 && r[15] is 0x01 or 0x02 or 0x08);
                if (r is null || r[6] > 8)
                    return null;
                if (r[15] == 0x01)
                    return VendorReading.SwitchedOff;
                int percent = r[6] * 100 / 8;
                // Nine steps aren't a percentage: shown by level, with the percentage as a guide.
                return new VendorReading(null, r[15] == 0x02 ? ChargeState.Charging : ChargeState.Discharging,
                    ControllerBattery.LevelFromPercent(percent));
            }
        }
        return null;
    }

    /// <summary>A five-step level (0..4) as a coarse level.</summary>
    private static BatteryLevel Steps(int step) => step switch
    {
        0 => BatteryLevel.Empty,
        1 => BatteryLevel.Low,
        2 or 3 => BatteryLevel.Medium,
        _ => BatteryLevel.Full,
    };

    private static byte[] Pad(byte[] start, int length)
    {
        var padded = new byte[length];
        start.CopyTo(padded, 0);
        return padded;
    }
}
