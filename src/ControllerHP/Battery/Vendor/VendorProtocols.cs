namespace ControllerHP.Battery.Vendor;

/// <summary>Every maker's protocol the vendor source reads devices with.</summary>
internal static class VendorProtocols
{
    public static IReadOnlyList<VendorProtocol> All { get; } =
    [
        new SharedMouseFirmware(),
        new SteelSeries(),
        new Razer(),
        new Corsair(),
        new HyperX(),
        new Asus(),
        new Keychron(),
    ];
}
