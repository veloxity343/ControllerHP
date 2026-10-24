namespace WattsLeft.Battery.Vendor;

/// <summary>Every maker's protocol the vendor source reads devices with.</summary>
internal static class VendorProtocols
{
    public static IReadOnlyList<VendorProtocol> All { get; } =
    [
        new SharedMouseFirmware(),
        new SteelSeries(),
        new Razer(),
        new RazerBlackShark2020(),
        new Corsair(),
        new HyperX(),
        new Asus(),
        new Keychron(),
        new Pulsar(),
        new Mchose(),
        new Lofree(),
        new Astro(),
        new Audeze(),
        new Jbl(),
        new Nzxt(),
        new AmInfinity(),
        new SteelSeriesElite(),
        new LogitechCenturion(),
        new LogitechG930(),
        new Sony(),
        new LenovoVoip(),
        new Nintendo(),
        new SteamPuck(),
        new EightBitDo(),
    ];
}
