namespace WattsLeft.Battery.Vendor;

/// <summary>
/// Pulsar, ATK, VXE and Hitscan wireless mice, which share one firmware platform, on their receivers or
/// their cables.
/// </summary>
/// <remarks>
/// Frames are 17 bytes, output report 08: 08 (the report id and header), a command, arguments, and a
/// checksum making the frame's bytes add up to 55. Command 04 asks for power details; the reply has the
/// level in byte 6, the "on its cable" flag in byte 7 and millivolts big-endian in bytes 8-9. The mice push
/// events on the same collection, so a reply counts only with the right header, command and checksum. It's
/// sent to the FF02:0002 collection, or else an interface 1 one, whose output report takes 17 bytes. The
/// mice stop answering a few seconds after the last movement, so a silent poll is normal: the last level is
/// kept for as long as the receiver is plugged in.
/// </remarks>
internal sealed class Pulsar : VendorProtocol
{
    public override string Name => "Pulsar";

    public override IReadOnlyCollection<ushort> Vendors { get; } = [0x3554, 0x373B, 0x3770, 0x3710];

    private static readonly Dictionary<(ushort, ushort), (string Name, bool Cable)> Models = new()
    {
        [(0x3554, 0xF508)] = ("Pulsar X2 V2 Mini", false), [(0x3554, 0xF507)] = ("Pulsar X2 V2 Mini", true),
        [(0x373B, 0x1085)] = ("ATK VXE R1 SE+", false), [(0x3554, 0xF58F)] = ("ATK VXE R1 SE+", true),
        [(0x3554, 0xF58A)] = ("VXE R1 Pro Max", false), [(0x3554, 0xF58C)] = ("VXE R1 Pro Max", true),
        [(0x3770, 0x0200)] = ("Hitscan Hyperlight", false), [(0x3770, 0x0100)] = ("Hitscan Hyperlight", true),
        [(0x3710, 0x5406)] = ("Pulsar X2 CrazyLight Mini", false), [(0x3710, 0x3414)] = ("Pulsar X2 CrazyLight Mini", true),
    };

    public override VendorModel? Identify(ushort vendor, ushort product) =>
        Models.TryGetValue((vendor, product), out var m)
            ? new VendorModel(m.Name, m.Name.ToLowerInvariant().Replace(' ', '-'), DeviceKind.Mouse, m.Cable)
            : null;

    public override TimeSpan SilentKeep(VendorModel model) => TimeSpan.MaxValue;

    private const int FrameLength = 17;
    private const byte Header = 0x08, PowerDetails = 0x04;
    private static readonly TimeSpan ReadWait = TimeSpan.FromMilliseconds(250);

    public override async Task<VendorReading?> ReadAsync(VendorDevice device, CancellationToken token)
    {
        var candidates = device.ByPreference
            .Where(c => c.Collection.OutputLength is 0 or FrameLength &&
                        ((c.Collection.UsagePage == 0xFF02 && c.Collection.Usage == 0x0002) || c.Collection.Interface == 1))
            .OrderBy(c => c.Collection.UsagePage == 0xFF02 && c.Collection.Usage == 0x0002 ? 0 : 1);
        foreach (var (collection, _) in candidates)
        {
            using var link = HidLink.Open(collection);
            if (link is null)
                continue;
            while (await link.ReadAsync(_ => true, TimeSpan.FromMilliseconds(30), token) is not null) { }
            var frame = new byte[FrameLength];
            frame[0] = Header;
            frame[1] = PowerDetails;
            frame[^1] = Checksum(frame);
            var r = await link.AskAsync(frame, IsPowerReply, ReadWait, token);
            if (r is not null)
                return new VendorReading(r[6], r[7] != 0 && r[6] < 100 ? ChargeState.Charging : ChargeState.Discharging);
        }
        return null;
    }

    private static byte Checksum(ReadOnlySpan<byte> frame)
    {
        int sum = 0;
        for (int i = 0; i < FrameLength - 1; i++)
            sum += frame[i];
        return (byte)(0x55 - sum);
    }

    internal static bool IsPowerReply(byte[] r) =>
        r.Length >= FrameLength && r[0] == Header && r[1] == PowerDetails && r[FrameLength - 1] == Checksum(r) && r[6] <= 100;
}
