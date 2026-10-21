using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using Windows.Devices.Enumeration;
using Windows.Devices.HumanInterfaceDevice;

namespace ControllerHP.Battery;

/// <summary>
/// Diagnostic: listens to a controller's raw HID input reports for a few seconds and prints each
/// distinct report, to find fields Windows doesn't surface (e.g. a charging flag).
/// </summary>
internal static partial class HidProbe
{
    [LibraryImport("hid.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool HidD_GetInputReport(SafeFileHandle device, byte[] buffer, int length);

    [LibraryImport("hid.dll")]
    private static partial int HidP_GetValueCaps(int reportType, [Out] HidpValueCaps[] caps, ref ushort length, IntPtr preparsed);

    /// <summary>HIDP_VALUE_CAPS (72 bytes); only the leading fields are read.</summary>
    [StructLayout(LayoutKind.Sequential, Size = 72)]
    private struct HidpValueCaps
    {
        public ushort UsagePage;
        public byte ReportID;
        public byte IsAlias;
        public ushort BitField, LinkCollection, LinkUsage, LinkUsagePage;
        public byte IsRange, IsStringRange, IsDesignatorRange, IsAbsolute, HasNull, Reserved;
        public ushort BitSize, ReportCount;
        public ushort Reserved2a, Reserved2b, Reserved2c, Reserved2d, Reserved2e;
        public uint UnitsExp, Units;
        public int LogicalMin, LogicalMax, PhysicalMin, PhysicalMax;
        public ushort UsageMin, UsageMax;
    }

    private static void DescribeCaps(SafeFileHandle handle)
    {
        if (!Hid.HidD_GetPreparsedData(handle, out var preparsed))
        {
            Console.WriteLine("    no preparsed data");
            return;
        }
        try
        {
            Hid.HidP_GetCaps(preparsed, out var caps);
            Console.WriteLine($"    caps: input={caps.InputReportByteLength}B output={caps.OutputReportByteLength}B feature={caps.FeatureReportByteLength}B");
            foreach (var (type, name, count) in new[] { (0, "input", caps.NumberInputValueCaps), (2, "feature", caps.NumberFeatureValueCaps) })
            {
                if (count == 0)
                    continue;
                var values = new HidpValueCaps[count];
                ushort length = count;
                HidP_GetValueCaps(type, values, ref length, preparsed);
                foreach (var v in values.Take(length))
                    Console.WriteLine($"    {name} report 0x{v.ReportID:X2}: page 0x{v.UsagePage:X2} usage 0x{v.UsageMin:X2} {v.BitSize}bit x{v.ReportCount} range {v.LogicalMin}..{v.LogicalMax}");
            }
        }
        finally
        {
            Hid.HidD_FreePreparsedData(preparsed);
        }
    }

    /// <summary>Asks the device for each input report ID directly rather than waiting for it to send one.</summary>
    private static void RequestReports(SafeFileHandle handle)
    {
        for (int id = 0; id <= 0x0F; id++)
        {
            var buffer = new byte[64];
            buffer[0] = (byte)id;
            bool ok = HidD_GetInputReport(handle, buffer, buffer.Length);
            int error = ok ? 0 : Marshal.GetLastWin32Error();
            if (ok)
                Console.WriteLine($"    get report 0x{id:X2}: {Convert.ToHexString(buffer).TrimEnd('0')}");
            else
                Console.WriteLine($"    get report 0x{id:X2}: error {error}");
        }
    }

    public static void Dump(ushort vendorId, ushort productId, TimeSpan duration)
    {
        // Game controllers are usage page 0x01 (Generic Desktop), usage 0x05 (Game Pad).
        string selector = HidDevice.GetDeviceSelector(0x01, 0x05, vendorId, productId);
        var devices = DeviceInformation.FindAllAsync(selector).AsTask().GetAwaiter().GetResult();
        if (devices.Count == 0)
        {
            Console.WriteLine($"  no HID gamepad {vendorId:X4}:{productId:X4}");
            return;
        }

        foreach (var device in devices)
        {
            Console.WriteLine($"  {device.Name}: {device.Id}");
            using var handle = Hid.Open(device.Id, Hid.GenericRead);
            if (handle.IsInvalid)
            {
                Console.WriteLine($"    open failed: error {Marshal.GetLastWin32Error()}");
                continue;
            }

            DescribeCaps(handle);
            RequestReports(handle);

            using var stream = new FileStream(handle, FileAccess.Read, 0, isAsync: true);
            var buffer = new byte[512];
            var seen = new Dictionary<byte, string>();
            var counts = new Dictionary<byte, int>();
            var deadline = DateTime.UtcNow + duration;
            while (DateTime.UtcNow < deadline)
            {
                int read;
                try
                {
                    using var timeout = new CancellationTokenSource(deadline - DateTime.UtcNow);
                    read = stream.ReadAsync(buffer, timeout.Token).AsTask().GetAwaiter().GetResult();
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (IOException ex)
                {
                    Console.WriteLine($"    read failed: {ex.Message}");
                    break;
                }
                if (read == 0)
                    break;

                byte id = buffer[0];
                counts[id] = counts.GetValueOrDefault(id) + 1;
                string hex = Convert.ToHexString(buffer, 0, read);
                if (!seen.TryGetValue(id, out var last) || last != hex)
                {
                    seen[id] = hex;
                    if (counts[id] <= 3 || id != 0x01)
                        Console.WriteLine($"    report 0x{id:X2} ({read} bytes): {string.Join(' ', Enumerable.Range(0, read).Select(i => buffer[i].ToString("X2")))}");
                }
            }
            foreach (var (id, count) in counts)
                Console.WriteLine($"    report 0x{id:X2}: {count} received");
        }
    }
}
