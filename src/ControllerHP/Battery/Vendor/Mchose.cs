namespace ControllerHP.Battery.Vendor;

/// <summary>
/// MCHOSE wireless mice: the 5253 and 3837 families (L7, M7, A7, K7) and the G7, on their receivers or
/// cables; and the X9 Wireless headset. (The first-generation A5 Pro Max runs the shared mouse firmware: see <see cref="SharedMouseFirmware"/>.)
/// </summary>
/// <remarks>
/// The 5253 and 3837 families answer on their FF01 configuration collection, through feature reports 12
/// (64 bytes) or 11 (20 bytes) whose payload is inverted, byte by byte (XOR FF), padding and all. Command
/// 06 asks for the status; the reply echoes F9 (06 inverted), then its inverted payload: the vendor id and
/// model id (little-endian), firmware, a link byte, the level and the charging flag. It must be asked
/// afresh for every attempt, and the receiver answers with zeros while the mouse sleeps. The receiver's
/// product id doesn't say which mouse it is, so any product on those vendor ids is asked, and the model id
/// in the reply names it.
///
/// The G7 (A8A5:2255) is another chip: output report 00 55 30 A5 0B 2E 01 01 01 on its FF01:0010
/// collection, answered by an input report AA 30 with the level in byte 8 and the charging flag in byte 9.
/// </remarks>
internal sealed class Mchose : VendorProtocol
{
    public override string Name => "MCHOSE";

    private const ushort FamilyVendor = 0x5253, NewerVendor = 0x3837, G7Vendor = 0xA8A5, G7Product = 0x2255;
    public override IReadOnlyCollection<ushort> Vendors { get; } = [FamilyVendor, NewerVendor, G7Vendor];

    private static readonly Dictionary<ushort, string> ModelNames = new() { [0x0031] = "MCHOSE M7 Ultra" };

    private enum Kind { Family, G7, X9 }

    public override VendorModel? Identify(ushort vendor, ushort product) => (vendor, product) switch
    {
        (G7Vendor, G7Product) => new VendorModel("MCHOSE G7", "g7", DeviceKind.Mouse, Detail: Kind.G7),
        (NewerVendor, 0x6045) => new VendorModel("MCHOSE X9 Wireless", "x9", DeviceKind.Headset, Detail: Kind.X9),
        (FamilyVendor or NewerVendor, _) => new VendorModel("MCHOSE mouse", $"{vendor:X4}", DeviceKind.Mouse, Detail: Kind.Family),
        _ => null,
    };

    private const byte Status = 0x06;
    private static readonly TimeSpan AttemptGap = TimeSpan.FromMilliseconds(120), G7Wait = TimeSpan.FromMilliseconds(500);

    public override async Task<VendorReading?> ReadAsync(VendorDevice device, CancellationToken token)
    {
        foreach (var (collection, model) in device.ByPreference)
        {
            var kind = (Kind)model.Detail!;
            bool wanted = kind switch
            {
                Kind.G7 => collection.UsagePage == 0xFF01 && collection.Usage == 0x0010,
                Kind.X9 => collection.UsagePage == 0xFF90 && collection.Usage == 0x0001,
                _ => collection.UsagePage == 0xFF01,
            };
            if (!wanted)
                continue;
            using var link = HidLink.Open(collection);
            if (link is null)
                continue;
            var reading = kind switch
            {
                Kind.G7 => await ReadG7Async(link, token),
                Kind.X9 => await ReadX9Async(link, token),
                _ => await ReadFamilyAsync(link, token),
            };
            if (reading is not null)
                return reading;
        }
        return null;
    }

    private static async Task<VendorReading?> ReadFamilyAsync(HidLink link, CancellationToken token)
    {
        foreach (var (reportId, length) in new[] { (0x12, 64), (0x11, 20) })
        {
            var payload = new byte[length];
            payload[0] = Status;
            for (int i = 0; i < payload.Length; i++)
                payload[i] ^= 0xFF;
            var request = link.FeatureReport((byte)reportId, payload);
            for (int attempt = 0; attempt < 4; attempt++)
            {
                if (!link.SetFeature(request))
                    break;
                await Task.Delay(AttemptGap, token);
                if (link.GetFeature((byte)reportId) is { } reply && Parse(reply) is { } reading)
                    return reading;
            }
        }
        return null;
    }

    /// <summary>A status reply, or null for zeros (asleep), the request echoed back, or anything else.</summary>
    internal static VendorReading? Parse(byte[] r)
    {
        if (r.Length < 13 || r[0] is not (0x11 or 0x12) || r[1] != (Status ^ 0xFF))
            return null;
        byte Payload(int i) => (byte)(r[2 + i] ^ 0xFF);
        ushort vendor = (ushort)(Payload(0) | Payload(1) << 8);
        if (vendor is not (FamilyVendor or NewerVendor))
            return null;
        ushort model = (ushort)(Payload(2) | Payload(3) << 8);
        byte level = Payload(9), charging = Payload(10);
        if (level > 100)
            return null;
        return new VendorReading(level, charging != 0 ? ChargeState.Charging : ChargeState.Discharging,
            Name: ModelNames.GetValueOrDefault(model));
    }

    /// <summary>The X9 headset: output report 55 65 01, answered 55 65 &lt;level&gt; 02. No charging flag.</summary>
    private static async Task<VendorReading?> ReadX9Async(HidLink link, CancellationToken token)
    {
        var r = await link.AskAsync(link.OutputReport(0x55, [0x65, 0x01]),
            r => r.Length >= 4 && r[0] == 0x55 && r[1] == 0x65 && r[3] == 0x02 && r[2] <= 100, G7Wait, token);
        return r is null ? null : new VendorReading(r[2], ChargeState.Discharging);
    }

    private static async Task<VendorReading?> ReadG7Async(HidLink link, CancellationToken token)
    {
        var r = await link.AskAsync(link.OutputReport(0x00, [0x55, 0x30, 0xA5, 0x0B, 0x2E, 0x01, 0x01, 0x01]),
            r => r.Length > 9 && r[0] == 0xAA && r[1] == 0x30 && r[8] <= 100, G7Wait, token);
        return r is null ? null : new VendorReading(r[8], r[9] != 0 ? ChargeState.Charging : ChargeState.Discharging);
    }
}
