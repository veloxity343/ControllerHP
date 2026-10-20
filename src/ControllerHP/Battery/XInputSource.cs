using System.Runtime.InteropServices;

namespace ControllerHP.Battery;

/// <summary>
/// Fallback for Xbox controllers via XInput. Only four coarse levels, no charging state, and slots are
/// identified by player index (0-3) rather than by device.
/// </summary>
public sealed partial class XInputSource : IBatterySource
{
    private const int MaxControllers = 4;
    private const byte BatteryDevTypeGamepad = 0;

    private const byte BatteryTypeDisconnected = 0x00;
    private const byte BatteryTypeWired = 0x01;
    private const byte BatteryTypeAlkaline = 0x02;
    private const byte BatteryTypeNimh = 0x03;

    [StructLayout(LayoutKind.Sequential)]
    private struct XInputBatteryInformation
    {
        public byte BatteryType;
        public byte BatteryLevel;
    }

    [LibraryImport("xinput1_4.dll")]
    private static partial uint XInputGetBatteryInformation(uint userIndex, byte devType, out XInputBatteryInformation info);

    public string Name => "XInput";

    // XInput has no connection notifications; the tray app's polling timer picks up changes.
    public event EventHandler? ControllersChanged { add { } remove { } }

    public IReadOnlyList<ControllerBattery> Read()
    {
        var readings = new List<ControllerBattery>();
        for (uint i = 0; i < MaxControllers; i++)
        {
            if (XInputGetBatteryInformation(i, BatteryDevTypeGamepad, out var info) != 0 ||
                info.BatteryType == BatteryTypeDisconnected)
            {
                continue;
            }

            string id = $"xinput:{i}";
            string name = $"Xbox Controller {i + 1}";
            readings.Add(info.BatteryType switch
            {
                BatteryTypeWired => new(id, name, Name, null, BatteryLevel.Unknown, ChargeState.Wired),
                BatteryTypeAlkaline or BatteryTypeNimh =>
                    new(id, name, Name, null, (BatteryLevel)(info.BatteryLevel + 1), ChargeState.Discharging),
                _ => new(id, name, Name, null, BatteryLevel.Unknown, ChargeState.Unknown),
            });
        }
        return readings;
    }
}
