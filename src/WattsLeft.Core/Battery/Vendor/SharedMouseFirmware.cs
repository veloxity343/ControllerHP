namespace WattsLeft.Battery.Vendor;

/// <summary>
/// Wireless gaming mice from several makers whose mice run the same firmware family, and so answer the same
/// battery request: WLmouse, LAMZU, G-Wolves and MCHOSE's first-generation A5 Pro Max, on their receivers or
/// their cables.
/// </summary>
/// <remarks>
/// The request is a 64-byte feature report with no report id, the bytes 00 00 02 02 00 83 then zeros. The
/// mouse answers in the same feature report: a status byte (A1 awake), two bytes, 02 00 83, then the charging
/// flag and the battery percentage. Older G-Wolves models use a variant: 00 02 8F, then 01 over a receiver
/// or 00 on the cable; the answer is A1 02 8F, a byte, the charging flag and the percentage. The request
/// only goes to a collection on usage page FFFF whose feature report is exactly that 64 bytes, which is
/// where the makers' own web drivers send it; if Windows doesn't give the size, nothing is sent.
/// </remarks>
internal sealed class SharedMouseFirmware : VendorProtocol
{
    public override string Name => "Mouse receivers";

    internal enum Request { Shared, OlderGWolves }

    /// <summary>The report id byte plus the 64 bytes of the request.</summary>
    private const int FeatureLength = 65;
    private const int ReplyTries = 15;

    private static VendorModel Mouse(string name, string group, bool cable, Request request) =>
        new(name, group, DeviceKind.Mouse, cable, request);

    private const ushort WLmouse = 0x36A7, Lamzu = 0x373E, GWolves = 0x33E4, MchoseA5 = 0x2023;

    private static readonly Dictionary<(ushort Vendor, ushort Product), VendorModel> Models = BuildModels();

    private static Dictionary<(ushort, ushort), VendorModel> BuildModels()
    {
        var models = new Dictionary<(ushort, ushort), VendorModel>
        {
            // WLmouse: each id is a receiver, and all of them one mouse's icon.
            [(WLmouse, 0xA887)] = Mouse("WLmouse Beast X", "wlmouse", false, Request.Shared),
            [(WLmouse, 0xA868)] = Mouse("WLmouse Beast X Mini Pro", "wlmouse", false, Request.Shared),
            [(WLmouse, 0xA880)] = Mouse("WLmouse Beast X Max", "wlmouse", false, Request.Shared),
            // LAMZU Maya X: its 8K receiver and its cable.
            [(Lamzu, 0x001E)] = Mouse("LAMZU Maya X", "lamzu-maya-x", false, Request.Shared),
            [(Lamzu, 0x001C)] = Mouse("LAMZU Maya X", "lamzu-maya-x", true, Request.Shared),
            // MCHOSE's first A5 Pro Max: its 1K and 4K receivers, and its cable.
            [(MchoseA5, 0xF013)] = Mouse("MCHOSE A5 Pro Max", "mchose-a5", false, Request.Shared),
            [(MchoseA5, 0xF015)] = Mouse("MCHOSE A5 Pro Max", "mchose-a5", false, Request.Shared),
            [(MchoseA5, 0xF019)] = Mouse("MCHOSE A5 Pro Max", "mchose-a5", true, Request.Shared),
            // G-Wolves' shared 8K receiver, which doesn't say which mouse it has.
            [(GWolves, 0x3854)] = Mouse("G-Wolves mouse", "gwolves-3854", false, Request.Shared),
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
            models[(GWolves, cable)] = Mouse(name, "gwolves-3854", true, Request.Shared);

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
            models[(GWolves, receiver)] = Mouse(name, group, false, request);
            foreach (ushort cable in cables)
                models[(GWolves, cable)] = Mouse(name, group, true, request);
        }
        return models;
    }

    public override IReadOnlyCollection<ushort> Vendors { get; } = [WLmouse, Lamzu, GWolves, MchoseA5];

    public override VendorModel? Identify(ushort vendor, ushort product) => Models.GetValueOrDefault((vendor, product));

    public override async Task<VendorReading?> ReadAsync(VendorDevice device, CancellationToken token)
    {
        foreach (var (collection, model) in device.ByPreference)
        {
            if (collection.UsagePage != 0xFFFF || collection.FeatureLength != FeatureLength)
                continue;
            using var link = HidLink.Open(collection);
            if (link is null)
                continue;
            var request = (Request)model.Detail!;
            byte[] payload = request == Request.Shared
                ? [0x00, 0x00, 0x02, 0x02, 0x00, 0x83]
                : [0x00, 0x02, 0x8F, (byte)(model.Cable ? 0x00 : 0x01)];
            if (await link.AskFeatureAsync(link.FeatureReport(0, payload), r => Parse(r, request) is not null,
                    ReplyTries, FeatureWait, token) is { } reply &&
                Parse(reply, request) is var (percent, charging))
            {
                return new VendorReading(percent, charging ? ChargeState.Charging : ChargeState.Discharging);
            }
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

}
