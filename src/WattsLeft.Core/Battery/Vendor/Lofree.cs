namespace WattsLeft.Battery.Vendor;

/// <summary>Lofree Hyzen keyboards on their 2.4 GHz dongle.</summary>
/// <remarks>
/// Each command is a small transaction on report 04 of the FF1C:0092 collection, 31 bytes after the report
/// id: 00 00 01 starts it (answered with 01 in byte 2), 00 00 &lt;cmd&gt; asks (answered with the command or 00
/// in byte 2, an offset of 0 in bytes 4-5, and the answer from byte 7), and 00 00 02 ends it. Command AA
/// asks whether the keyboard is online (0 off); 1A its battery level. The battery is only read over the
/// dongle, not while the keyboard is also on its cable. No charging flag.
/// </remarks>
internal sealed class Lofree : VendorProtocol
{
    public override string Name => "Lofree";

    private const ushort LofreeVendor = 0x388D;
    public override IReadOnlyCollection<ushort> Vendors { get; } = [LofreeVendor];

    public override VendorModel? Identify(ushort vendor, ushort product) => product switch
    {
        0x0025 => new VendorModel("Lofree Hyzen", "hyzen", DeviceKind.Keyboard),
        0x0024 => new VendorModel("Lofree Hyzen", "hyzen", DeviceKind.Keyboard, Cable: true),
        _ => null,
    };

    private const byte ReportId = 0x04, Start = 0x01, End = 0x02, Online = 0xAA, Battery = 0x1A;
    private static readonly TimeSpan Transaction = TimeSpan.FromSeconds(3), EndWait = TimeSpan.FromMilliseconds(500);

    public override async Task<VendorReading?> ReadAsync(VendorDevice device, CancellationToken token)
    {
        var all = device.Collections.Values.ToList();
        // On its cable too: the battery is read only over the dongle.
        if (all.Any(c => c.Model.Cable))
            return null;
        var collection = all.Select(c => c.Collection).FirstOrDefault(c => c.UsagePage == 0xFF1C && c.Usage == 0x0092);
        if (collection is null || HidLink.Open(collection) is not { } link)
            return null;
        using (link)
        {
            if (await CommandAsync(link, Online, token) is not byte online)
                return null;
            if (online == 0)
                return VendorReading.SwitchedOff;
            return await CommandAsync(link, Battery, token) is byte level and <= 100
                ? new VendorReading(level, ChargeState.Discharging)
                : null;
        }
    }

    /// <summary>One transaction: start, the command, end. Its first answer byte, or null.</summary>
    private static async Task<byte?> CommandAsync(HidLink link, byte command, CancellationToken token)
    {
        var deadline = DateTime.UtcNow + Transaction;
        TimeSpan Left() => deadline - DateTime.UtcNow is var left && left > TimeSpan.Zero ? left : TimeSpan.Zero;

        if (await link.AskAsync(link.OutputReport(ReportId, [0x00, 0x00, Start]), r => r.Length >= 4 && r[0] == ReportId && r[3] == Start,
                Left(), token) is null)
            return null;
        var answer = await link.AskAsync(link.OutputReport(ReportId, [0x00, 0x00, command]),
            r => r.Length > 8 && r[0] == ReportId && (r[3] == command || r[3] == 0x00) && r[5] == 0x00 && r[6] == 0x00, Left(), token);
        // Ended in any case, as Lofree's own driver does.
        await link.AskAsync(link.OutputReport(ReportId, [0x00, 0x00, End]), r => r.Length >= 4 && r[0] == ReportId && r[3] == End,
            Left() < EndWait ? Left() : EndWait, token);
        return answer?[8];
    }
}
