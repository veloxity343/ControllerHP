using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace ControllerHP.Battery;

/// <summary>A gamepad's battery report, as Windows.Gaming.Input gives it.</summary>
internal sealed record PadBatteryReport(PadBatteryStatus Status, int? RemainingMilliwattHours, int? FullChargeMilliwattHours,
    int? DesignMilliwattHours, int? ChargeRateMilliwatts);

/// <summary>Windows' battery status codes (Windows.System.Power.BatteryStatus).</summary>
internal enum PadBatteryStatus { NotPresent = 0, Discharging = 1, Idle = 2, Charging = 3 }

/// <summary>
/// One gamepad Windows.Gaming.Input lists, read at once. <paramref name="Identity"/> tells pads apart while they're
/// connected (it's where Windows keeps the pad); it's only compared, never used.
/// </summary>
internal sealed record PadInfo(string? NonRoamableId, string? DisplayName, ushort VendorId, ushort ProductId, bool IsWireless,
    PadBatteryReport? Battery, nint Identity);

/// <summary>
/// Windows.Gaming.Input's gamepads, called directly. It's a Windows Runtime API, which is COM underneath:
/// each object is a pointer to a table of methods in a fixed order. These are the few methods the app uses,
/// each called by its place in its interface's table, and released straight after.
/// </summary>
internal static unsafe partial class GamingInput
{
    // The interfaces used, by id (from Windows' metadata).
    private static readonly Guid IGamepadStatics = new("8BBCE529-D49C-39E9-9560-E47DDE96B7C8");
    private static readonly Guid IRawGameControllerStatics = new("EB8D0792-E95A-4B19-AFC7-0A59F8BF759E");
    private static readonly Guid IGameController = new("1BAF6522-5F64-42C5-8267-B9FE2215BFBD");
    private static readonly Guid IGameControllerBatteryInfo = new("DCECC681-3963-4DA6-955D-553F3B6F6161");
    private static readonly Guid IRawGameController = new("7CAD6D91-A7E1-4F71-9A78-33E9C5DFEA62");
    private static readonly Guid IRawGameController2 = new("43C0C035-BB73-4756-A787-3ED6BEA617BD");
    private static readonly Guid IUnknownId = new("00000000-0000-0000-C000-000000000046");
    private static readonly Guid IAgileObject = new("94EA2B94-E9CC-49E0-C0FF-EE64CA8F5B90");
    private const string GamepadDefaultInterface = "bc7bb43c-0a69-3903-9e9d-a50f86a45de5";

    /// <summary>The id of EventHandler&lt;Gamepad&gt;, worked out from its signature as Windows does for generic types.</summary>
    private static readonly Guid GamepadEventHandler = GenericId(
        $"pinterface({{9de1c535-6ae1-11e0-84e1-18a905bcc53f}};rc(Windows.Gaming.Input.Gamepad;{{{GamepadDefaultInterface}}}))");

    // Each method's place in its table: IInspectable's six come first.
    private const int GamepadsAdded = 6, GamepadsRemoved = 8, GamepadsList = 10;
    private const int VectorGetAt = 6, VectorSize = 7;
    private const int ControllerIsWireless = 13;
    private const int TryGetBatteryReport = 6;
    private const int ReportChargeRate = 6, ReportDesign = 7, ReportFull = 8, ReportRemaining = 9, ReportStatus = 10;
    private const int ReferenceValue = 6;
    private const int RawFromGameController = 11, RawProductId = 9, RawVendorId = 10, RawNonRoamableId = 7, RawDisplayName = 8;

    /// <summary>Every gamepad Windows.Gaming.Input lists now. Empty until its first enumeration finishes.</summary>
    public static List<PadInfo> Pads()
    {
        var pads = new List<PadInfo>();
        IntPtr statics = Factory("Windows.Gaming.Input.Gamepad", IGamepadStatics);
        IntPtr list = IntPtr.Zero;
        try
        {
            Check(((delegate* unmanaged<IntPtr, IntPtr*, int>)Method(statics, GamepadsList))(statics, &list));
            uint count;
            Check(((delegate* unmanaged<IntPtr, uint*, int>)Method(list, VectorSize))(list, &count));
            for (uint i = 0; i < count; i++)
            {
                IntPtr pad;
                Check(((delegate* unmanaged<IntPtr, uint, IntPtr*, int>)Method(list, VectorGetAt))(list, i, &pad));
                try
                {
                    pads.Add(Read(pad));
                }
                catch (Exception ex)
                {
                    // A pad that has just gone away can linger in the list and fail every call; skip it.
                    Program.LogOnce($"Windows.Gaming.Input pad failed: {ex.GetType().Name}: {ex.Message}");
                }
                finally
                {
                    Release(pad);
                }
            }
        }
        finally
        {
            Release(list);
            Release(statics);
        }
        return pads;
    }

    private static PadInfo Read(IntPtr pad)
    {
        IntPtr controller = Query(pad, IGameController), battery = Query(pad, IGameControllerBatteryInfo), identity = Query(pad, IUnknownId);
        IntPtr rawStatics = IntPtr.Zero, raw = IntPtr.Zero, raw2 = IntPtr.Zero, report = IntPtr.Zero;
        try
        {
            byte wireless;
            Check(((delegate* unmanaged<IntPtr, byte*, int>)Method(controller, ControllerIsWireless))(controller, &wireless));
            Check(((delegate* unmanaged<IntPtr, IntPtr*, int>)Method(battery, TryGetBatteryReport))(battery, &report));

            // The raw controller behind the gamepad says what hardware it is; one can be missing while a pad goes away.
            rawStatics = Factory("Windows.Gaming.Input.RawGameController", IRawGameControllerStatics);
            Check(((delegate* unmanaged<IntPtr, IntPtr, IntPtr*, int>)Method(rawStatics, RawFromGameController))(rawStatics, controller, &raw));
            ushort vendor = 0, product = 0;
            string? id = null, name = null;
            if (raw != IntPtr.Zero)
            {
                Check(((delegate* unmanaged<IntPtr, ushort*, int>)Method(raw, RawVendorId))(raw, &vendor));
                Check(((delegate* unmanaged<IntPtr, ushort*, int>)Method(raw, RawProductId))(raw, &product));
                raw2 = Query(raw, IRawGameController2);
                id = String(raw2, RawNonRoamableId);
                name = String(raw2, RawDisplayName);
            }
            return new PadInfo(id, name, vendor, product, wireless != 0, report == IntPtr.Zero ? null : ReadReport(report), identity);
        }
        finally
        {
            foreach (var pointer in new[] { report, raw2, raw, rawStatics, battery, controller, identity })
                Release(pointer);
        }
    }

    private static PadBatteryReport ReadReport(IntPtr report)
    {
        int status;
        Check(((delegate* unmanaged<IntPtr, int*, int>)Method(report, ReportStatus))(report, &status));
        return new PadBatteryReport((PadBatteryStatus)status, NullableInt(report, ReportRemaining), NullableInt(report, ReportFull),
            NullableInt(report, ReportDesign), NullableInt(report, ReportChargeRate));
    }

    /// <summary>A property that's an int or nothing: an IReference&lt;int&gt;, or null.</summary>
    private static int? NullableInt(IntPtr obj, int slot)
    {
        IntPtr reference;
        Check(((delegate* unmanaged<IntPtr, IntPtr*, int>)Method(obj, slot))(obj, &reference));
        if (reference == IntPtr.Zero)
            return null;
        try
        {
            int value;
            Check(((delegate* unmanaged<IntPtr, int*, int>)Method(reference, ReferenceValue))(reference, &value));
            return value;
        }
        finally
        {
            Release(reference);
        }
    }

    private static string? String(IntPtr obj, int slot)
    {
        IntPtr hstring;
        Check(((delegate* unmanaged<IntPtr, IntPtr*, int>)Method(obj, slot))(obj, &hstring));
        if (hstring == IntPtr.Zero)
            return null;
        try
        {
            char* text = WindowsGetStringRawBuffer(hstring, out uint length);
            return new string(text, 0, (int)length);
        }
        finally
        {
            WindowsDeleteString(hstring);
        }
    }

    /// <summary>
    /// Runs <paramref name="onChange"/> whenever a gamepad is added or removed, on Windows.Gaming.Input's thread.
    /// The first call also starts its enumeration: pads already connected arrive as additions.
    /// </summary>
    public static IDisposable WatchPads(Action onChange)
    {
        var handler = new PadHandler(onChange);
        IntPtr statics = Factory("Windows.Gaming.Input.Gamepad", IGamepadStatics);
        try
        {
            long added, removed;
            Check(((delegate* unmanaged<IntPtr, IntPtr, long*, int>)Method(statics, GamepadsAdded))(statics, handler.Native, &added));
            Check(((delegate* unmanaged<IntPtr, IntPtr, long*, int>)Method(statics, GamepadsRemoved))(statics, handler.Native, &removed));
            handler.Tokens = (added, removed);
            return handler;
        }
        finally
        {
            Release(statics);
        }
    }

    /// <summary>
    /// The handler Windows calls back: a small COM object of our own, answering for EventHandler&lt;Gamepad&gt;.
    /// It's agile (callable from any thread), and lives as long as the app.
    /// </summary>
    private sealed class PadHandler : IDisposable
    {
        public readonly Action OnChange;
        public readonly IntPtr Native;
        public (long Added, long Removed) Tokens;
        private readonly GCHandle _self;

        private static readonly IntPtr* Table = CreateTable();

        public PadHandler(Action onChange)
        {
            OnChange = onChange;
            _self = GCHandle.Alloc(this);
            // The object: its method table, then a handle back to this.
            Native = Marshal.AllocHGlobal(2 * IntPtr.Size);
            ((IntPtr*)Native)[0] = (IntPtr)Table;
            ((IntPtr*)Native)[1] = GCHandle.ToIntPtr(_self);
        }

        private static IntPtr* CreateTable()
        {
            var table = (IntPtr*)Marshal.AllocHGlobal(4 * IntPtr.Size);
            table[0] = (IntPtr)(delegate* unmanaged<IntPtr, Guid*, IntPtr*, int>)&HandlerQueryInterface;
            table[1] = (IntPtr)(delegate* unmanaged<IntPtr, uint>)&HandlerAddRef;
            table[2] = (IntPtr)(delegate* unmanaged<IntPtr, uint>)&HandlerRelease;
            table[3] = (IntPtr)(delegate* unmanaged<IntPtr, IntPtr, IntPtr, int>)&HandlerInvoke;
            return table;
        }

        public void Dispose()
        {
            IntPtr statics = IntPtr.Zero;
            try
            {
                statics = Factory("Windows.Gaming.Input.Gamepad", IGamepadStatics);
                ((delegate* unmanaged<IntPtr, long, int>)Method(statics, GamepadsAdded + 1))(statics, Tokens.Added);
                ((delegate* unmanaged<IntPtr, long, int>)Method(statics, GamepadsRemoved + 1))(statics, Tokens.Removed);
            }
            catch (Exception)
            {
                // Shutting down anyway.
            }
            finally
            {
                Release(statics);
            }
            // The object itself is kept: Windows may still hold a reference to it.
        }
    }

    [UnmanagedCallersOnly]
    private static int HandlerQueryInterface(IntPtr self, Guid* iid, IntPtr* result)
    {
        if (*iid == IUnknownId || *iid == IAgileObject || *iid == GamepadEventHandler)
        {
            *result = self;
            return 0;
        }
        *result = IntPtr.Zero;
        return unchecked((int)0x80004002); // E_NOINTERFACE
    }

    // Reference counting isn't needed: the handler lives as long as the app.
    [UnmanagedCallersOnly]
    private static uint HandlerAddRef(IntPtr self) => 1;

    [UnmanagedCallersOnly]
    private static uint HandlerRelease(IntPtr self) => 1;

    [UnmanagedCallersOnly]
    private static int HandlerInvoke(IntPtr self, IntPtr sender, IntPtr gamepad)
    {
        try
        {
            if (GCHandle.FromIntPtr(((IntPtr*)self)[1]).Target is PadHandler handler)
                handler.OnChange();
        }
        catch (Exception ex)
        {
            Program.LogOnce($"A gamepad event failed: {ex.GetType().Name}: {ex.Message}");
        }
        return 0;
    }

    /// <summary>
    /// A generic Windows Runtime interface's id: a version 5 UUID of its signature, in Windows' own namespace for
    /// these, as the Windows Runtime type system defines it.
    /// </summary>
    internal static Guid GenericId(string signature)
    {
        byte[] space = [0x11, 0xF4, 0x7A, 0xD5, 0x7B, 0x73, 0x42, 0xC0, 0xAB, 0xAE, 0x87, 0x8B, 0x1E, 0x16, 0xAD, 0xEE];
        byte[] hash = SHA1.HashData([.. space, .. Encoding.UTF8.GetBytes(signature)]);
        int a = BinaryPrimitives.ReadInt32BigEndian(hash);
        short b = BinaryPrimitives.ReadInt16BigEndian(hash.AsSpan(4));
        short c = (short)((BinaryPrimitives.ReadUInt16BigEndian(hash.AsSpan(6)) & 0x0FFF) | 0x5000);
        return new Guid(a, b, c, (byte)((hash[8] & 0x3F) | 0x80), hash[9], hash[10], hash[11], hash[12], hash[13], hash[14], hash[15]);
    }

    private static IntPtr Method(IntPtr obj, int slot) => (*(IntPtr**)obj)[slot];

    private static IntPtr Query(IntPtr obj, Guid iid)
    {
        IntPtr result;
        Check(((delegate* unmanaged<IntPtr, Guid*, IntPtr*, int>)Method(obj, 0))(obj, &iid, &result));
        return result;
    }

    private static void Release(IntPtr obj)
    {
        if (obj != IntPtr.Zero)
            ((delegate* unmanaged<IntPtr, uint>)Method(obj, 2))(obj);
    }

    private static void Check(int hr)
    {
        if (hr < 0)
            Marshal.ThrowExceptionForHR(hr);
    }

    /// <summary>A runtime class's static methods, by its name and the interface they're on.</summary>
    private static IntPtr Factory(string className, Guid iid)
    {
        Check(WindowsCreateString(className, (uint)className.Length, out IntPtr name));
        try
        {
            Check(RoGetActivationFactory(name, iid, out IntPtr factory));
            return factory;
        }
        finally
        {
            WindowsDeleteString(name);
        }
    }

    [LibraryImport("combase.dll", StringMarshalling = StringMarshalling.Utf16)]
    private static partial int WindowsCreateString(string text, uint length, out IntPtr hstring);

    [LibraryImport("combase.dll")]
    private static partial int WindowsDeleteString(IntPtr hstring);

    [LibraryImport("combase.dll")]
    private static partial char* WindowsGetStringRawBuffer(IntPtr hstring, out uint length);

    [LibraryImport("combase.dll")]
    private static partial int RoGetActivationFactory(IntPtr classId, in Guid iid, out IntPtr factory);
}
