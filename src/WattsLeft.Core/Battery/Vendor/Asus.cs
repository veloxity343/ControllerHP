namespace WattsLeft.Battery.Vendor;

/// <summary>
/// ASUS ROG and TUF wireless mice, on their 2.4 GHz receivers or their cables.
/// </summary>
/// <remarks>
/// On the vendor collection of interface 0: output report 00 12 07 (the battery command), padded to 64
/// bytes. The reply echoes 12 07; byte 4 is the battery (a percentage, or 0..4 on older models) and byte 9
/// non-zero means charging. FF AA means the mouse doesn't know the command; all zeros, no reply. A battery of
/// 0 while not charging is standby, not empty.
/// </remarks>
internal sealed class Asus : VendorProtocol
{
    public override string Name => "ASUS";

    private const ushort AsusVendor = 0x0B05;
    public override IReadOnlyCollection<ushort> Vendors { get; } = [AsusVendor];

    /// <summary>Product id: name, and whether its battery byte is a percentage or a level 0..4.</summary>
    private static readonly Dictionary<ushort, (string Name, bool Steps)> Models = new()
    {
        [0x1A72] = ("ROG Gladius III Aimpoint", false), [0x1A70] = ("ROG Gladius III Aimpoint", false),
        [0x1B0C] = ("ROG Gladius III Eva 2", false), [0x1B0A] = ("ROG Gladius III Eva 2", false),
        [0x197F] = ("ROG Gladius III Wireless", false), [0x197D] = ("ROG Gladius III Wireless", false),
        [0x1A1A] = ("ROG Chakram X", false), [0x1A18] = ("ROG Chakram X", false),
        [0x1A94] = ("ROG Harpe Ace Aim Lab Edition", false), [0x1A92] = ("ROG Harpe Ace Aim Lab Edition", false),
        [0x1A68] = ("ROG Keris Wireless Aimpoint", false), [0x1A66] = ("ROG Keris Wireless Aimpoint", false),
        [0x1979] = ("ROG Spatha X", false), [0x1977] = ("ROG Spatha X", false),
        [0x19F4] = ("TUF Gaming M4 Wireless", false),
        [0x1A8D] = ("TX Gaming Mouse", false),
        [0x1AF5] = ("TX Gaming Mouse Mini", false), [0x1AF3] = ("TX Gaming Mouse Mini", false),
        [0x1C57] = ("TUF Gaming Mini Miku Edition", false), [0x1C56] = ("TUF Gaming Mini Miku Edition", false),
        [0x18E5] = ("ROG Chakram", true), [0x18E3] = ("ROG Chakram", true),
        [0x1960] = ("ROG Keris Wireless", true), [0x195E] = ("ROG Keris Wireless", true),
        [0x1A59] = ("ROG Keris EVA Edition", true), [0x1A57] = ("ROG Keris EVA Edition", true),
        [0x1908] = ("ROG Pugio II", true), [0x1906] = ("ROG Pugio II", true),
        [0x1949] = ("ROG Strix Impact II Wireless", true), [0x1947] = ("ROG Strix Impact II Wireless", true),
    };

    public override VendorModel? Identify(ushort vendor, ushort product) =>
        Models.TryGetValue(product, out var m)
            ? new VendorModel(m.Name, m.Name.ToLowerInvariant().Replace(' ', '-'), DeviceKind.Mouse, Detail: m.Steps)
            : null;

    private static readonly TimeSpan ReadWait = TimeSpan.FromMilliseconds(300);

    public override async Task<VendorReading?> ReadAsync(VendorDevice device, CancellationToken token)
    {
        VendorReading? best = null;
        // The same mouse on its cable and its receiver: the one that's charging says so.
        foreach (var (collection, model) in device.ByPreference)
        {
            if (collection.Interface != 0 || collection.UsagePage < 0xFF00)
                continue;
            using var link = HidLink.Open(collection);
            if (link is null)
                continue;
            if (await AskAsync(link, (bool)model.Detail!, token) is { } reading &&
                (best is null || reading.Power == ChargeState.Charging))
            {
                best = reading;
            }
        }
        return best;
    }

    private static async Task<VendorReading?> AskAsync(HidLink link, bool steps, CancellationToken token)
    {
        // Drop reports already waiting, then ask up to three times; button and profile events are skipped.
        while (await link.ReadAsync(_ => true, TimeSpan.FromMilliseconds(5), token) is not null) { }
        for (int attempt = 0; attempt < 3; attempt++)
        {
            var r = await link.AskAsync(link.OutputReport(0x00, [0x12, 0x07]),
                r => r.Length >= 2 && ((r[0] == 0x12 && r[1] == 0x07) || (r[0] == 0xFF && r[1] == 0xAA) || r.All(b => b == 0)),
                ReadWait, token);
            if (r is null)
                continue;
            if (r[0] != 0x12 || r.Length < 10)
                return null; // FF AA (unknown command) or zeros (no reply)
            int raw = r[4];
            bool charging = r[9] != 0;
            if (raw == 0 && !charging)
                return null; // standby
            var power = charging ? ChargeState.Charging : ChargeState.Discharging;
            if (steps)
            {
                return raw > 4 ? null : new VendorReading(null, power, raw switch
                {
                    0 => BatteryLevel.Empty,
                    1 => BatteryLevel.Low,
                    2 or 3 => BatteryLevel.Medium,
                    _ => BatteryLevel.Full,
                });
            }
            return raw > 100 ? null : new VendorReading(raw, power);
        }
        return null;
    }
}
