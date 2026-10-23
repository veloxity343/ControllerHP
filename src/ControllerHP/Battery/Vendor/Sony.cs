namespace ControllerHP.Battery.Vendor;

/// <summary>Sony INZONE headsets on their dongles: the H5, the H9 II and the INZONE Buds.</summary>
/// <remarks>
/// The H5 (FF04:0002) and H9 II (FF04:0001, interface 5) dongles speak HCI-style frames in report 02: a
/// length, 01 (a command), opcode 00 FC, a parameter length, Sony's key 96 C3, an address (41: the PC to
/// the dongle), an event id (04 battery info), 01 get, a transaction id little-endian, then a checksum of
/// bytes 6 onwards. The answer is 02 &lt;length&gt; 04 FF 00 .. 96 C3, addressed back to the PC, with a checksum
/// of bytes 5 to the length; its payload, from byte 13, is the charger (non-zero charging) and the percent
/// (FF while the headset is off). The dongle pushes its own notices with transaction id 1, so 1 is skipped.
///
/// The Buds' dongle (0EC2) is only listened to: it pushes 02 12 04 frames with the right earbud in byte 14
/// and the left in byte 16, and the lower of the two is shown.
/// </remarks>
internal sealed class Sony : VendorProtocol
{
    public override string Name => "Sony";

    public override IReadOnlyCollection<ushort> Vendors { get; } = [0x054C];

    private enum Model { H5, H9II, Buds }

    public override VendorModel? Identify(ushort vendor, ushort product) => product switch
    {
        0x0EBF => new VendorModel("Sony INZONE H5", "inzone-h5", DeviceKind.Headset, Detail: Model.H5),
        0x0FA8 => new VendorModel("Sony INZONE H9 II", "inzone-h9-ii", DeviceKind.Headset, Detail: Model.H9II),
        0x0EC2 => new VendorModel("Sony INZONE Buds", "inzone-buds", DeviceKind.Headset, Detail: Model.Buds),
        _ => null,
    };

    private const byte BatteryInfo = 0x04;
    private static readonly TimeSpan ReadWait = TimeSpan.FromMilliseconds(500), BudsListen = TimeSpan.FromSeconds(5);

    public override async Task<VendorReading?> ReadAsync(VendorDevice device, CancellationToken token)
    {
        var collections = device.Collections.Values.Select(c => c.Collection).ToList();
        var model = (Model)device.Collections.Values.First().Model.Detail!;
        if (model == Model.Buds)
            return await ListenBudsAsync(collections, token);

        var collection = collections.FirstOrDefault(c => c.UsagePage == 0xFF04 &&
            (model == Model.H5 ? c.Usage == 0x0002 : c.Usage == 0x0001 && c.Interface is 5 or -1));
        if (collection is null || HidLink.Open(collection) is not { } link)
            return null;
        using (link)
        {
            ushort tid = (ushort)(device.Memo.GetValueOrDefault("tid") is ushort t && t < 0xFFFF ? t + 1 : 2);
            device.Memo["tid"] = tid;
            var request = link.OutputReport(0x02, [12, 0x01, 0x00, 0xFC, 8, 0x96, 0xC3, 0x41, BatteryInfo, 0x01, (byte)tid, (byte)(tid >> 8), 0x00]);
            int sum = 0;
            for (int i = 6; i <= 12; i++)
                sum += request[i];
            request[13] = (byte)sum;
            if (!await link.WriteAsync(request, token))
                return null;
            for (int attempt = 0; attempt < 10; attempt++)
            {
                if (await link.ReadAsync(r => Answer(r, tid) is not null, ReadWait, token) is not { } r)
                    continue;
                var payload = Answer(r, tid)!;
                if (payload.Length < 2 || payload[1] > 100 && payload[1] != 0xFF)
                    return null;
                return payload[1] == 0xFF
                    ? VendorReading.SwitchedOff
                    : new VendorReading(payload[1], payload[0] != 0 ? ChargeState.Charging : ChargeState.Discharging);
            }
            return null;
        }
    }

    /// <summary>The payload of a well-formed battery answer to this transaction, or null.</summary>
    internal static byte[]? Answer(byte[] r, ushort tid)
    {
        if (r.Length < 14 || r[0] != 0x02 || r[1] is < 12 or > 62 || r[1] + 1 >= r.Length)
            return null;
        int length = r[1];
        if (r[2] != 0x04 || r[3] != 0xFF || r[5] != 0x00 || r[6] != 0x96 || r[7] != 0xC3 || (r[8] >> 4) != 0x1)
            return null;
        int sum = 0;
        for (int i = 5; i <= length; i++)
            sum += r[i];
        if ((byte)sum != r[length + 1])
            return null;
        if (r[9] != BatteryInfo || r[10] is not (0x10 or 0xA0) || (r[11] | r[12] << 8) != tid)
            return null;
        return r[13..(length + 1)];
    }

    private static async Task<VendorReading?> ListenBudsAsync(List<HidCollection> collections, CancellationToken token)
    {
        var links = collections.Select(HidLink.Open).OfType<HidLink>().ToList();
        try
        {
            static bool Battery(byte[] r) => r.Length > 16 && r[0] == 0x02 && r[1] == 0x12 && r[2] == 0x04;
            foreach (var r in await Task.WhenAll(links.Select(l => l.ReadAsync(Battery, BudsListen, token))))
            {
                if (r is null)
                    continue;
                int level = Math.Min(r[14], r[16]);
                if (level <= 100)
                    return new VendorReading(level, ChargeState.Discharging);
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
