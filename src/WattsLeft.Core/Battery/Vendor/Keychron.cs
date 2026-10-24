namespace WattsLeft.Battery.Vendor;

/// <summary>
/// Keychron's Ultra-Link 8K receiver and the M5 mouse on its cable.
/// </summary>
/// <remarks>
/// On the vendor collection of interface 4: feature report B3 06 (the status command), padded to 64 bytes.
/// The answer is an input report starting B4 06, with the battery in byte 20. No charging flag.
/// </remarks>
internal sealed class Keychron : VendorProtocol
{
    public override string Name => "Keychron";

    private const ushort KeychronVendor = 0x3434;
    public override IReadOnlyCollection<ushort> Vendors { get; } = [KeychronVendor];

    private static readonly Dictionary<ushort, (string Name, bool Cable)> Models = new()
    {
        [0xD028] = ("Keychron Ultra-Link 8K", false),
        [0xD048] = ("Keychron M5", true),
    };

    public override VendorModel? Identify(ushort vendor, ushort product) =>
        Models.TryGetValue(product, out var m) ? new VendorModel(m.Name, $"{product:X4}", DeviceKind.Mouse, m.Cable) : null;

    private static readonly TimeSpan ReadWait = TimeSpan.FromMilliseconds(500), RetryWait = TimeSpan.FromMilliseconds(100);

    public override async Task<VendorReading?> ReadAsync(VendorDevice device, CancellationToken token)
    {
        foreach (var (collection, _) in device.ByPreference)
        {
            if (collection.Interface != 4)
                continue;
            using var link = HidLink.Open(collection);
            if (link is null)
                continue;
            while (await link.ReadAsync(_ => true, TimeSpan.FromMilliseconds(30), token) is not null) { }
            for (int attempt = 0; attempt < 3; attempt++)
            {
                if (attempt > 0)
                    await Task.Delay(RetryWait, token);
                if (!link.SetFeature(link.FeatureReport(0xB3, [0x06])))
                    break;
                var r = await link.ReadAsync(r => r.Length > 20 && r[0] == 0xB4 && r[1] == 0x06 && r[20] <= 100, ReadWait, token);
                if (r is not null)
                    return new VendorReading(r[20], ChargeState.Discharging);
            }
        }
        return null;
    }
}
