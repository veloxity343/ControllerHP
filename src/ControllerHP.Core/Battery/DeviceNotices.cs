using System.Runtime.InteropServices;

namespace ControllerHP.Battery;

/// <summary>
/// Tells the sources when a device interface of any kind arrives or goes: something plugged in or
/// unplugged, a Bluetooth device connecting or disconnecting (its HID, audio and GATT interfaces come and
/// go with it), a receiver's slots changing. Windows' configuration manager sends the notices; a burst of
/// them (one device brings several interfaces) is passed on once it settles.
/// </summary>
internal static partial class DeviceNotices
{
    private sealed class Subscription(Action onChange) : IDisposable
    {
        public readonly Action OnChange = onChange;

        public void Dispose()
        {
            lock (Subscribers)
                Subscribers.Remove(this);
        }
    }

    private static readonly List<Subscription> Subscribers = [];
    private static IntPtr _notification;
    private static int _queued;
    private static readonly TimeSpan SettleWait = TimeSpan.FromMilliseconds(300);

    /// <summary>Runs <paramref name="onChange"/>, on a worker thread, after each burst of arrivals and removals.</summary>
    public static unsafe IDisposable Watch(Action onChange)
    {
        var subscription = new Subscription(onChange);
        lock (Subscribers)
        {
            Subscribers.Add(subscription);
            if (_notification == IntPtr.Zero)
            {
                var filter = new NotifyFilter { Size = (uint)sizeof(NotifyFilter), Flags = AllInterfaceClasses, FilterType = FilterTypeDeviceInterface };
                if (CM_Register_Notification(filter, IntPtr.Zero, &OnNotice, out _notification) != 0)
                {
                    _notification = IntPtr.Zero;
                    Log.Once("Couldn't register for device notices; devices plugged in or connected later are seen at the next poll.");
                }
            }
        }
        return subscription;
    }

    /// <summary>Runs the subscribers after <paramref name="wait"/>, unless that's already queued.</summary>
    private static void Queue(TimeSpan wait)
    {
        if (Interlocked.Exchange(ref _queued, 1) != 0)
            return;
        _ = Task.Run(async () =>
        {
            await Task.Delay(wait);
            Interlocked.Exchange(ref _queued, 0);
            Subscription[] subscribers;
            lock (Subscribers)
                subscribers = [.. Subscribers];
            foreach (var subscriber in subscribers)
            {
                try
                {
                    subscriber.OnChange();
                }
                catch (Exception ex)
                {
                    Log.Once($"A device notice failed: {ex.GetType().Name}: {ex.Message}");
                }
            }
        });
    }

    [UnmanagedCallersOnly]
    private static uint OnNotice(IntPtr notification, IntPtr context, int action, IntPtr data, uint size)
    {
        // Passed on from another thread: the configuration manager waits for this to return, and listing
        // devices from inside it isn't allowed.
        if (action is ActionInterfaceArrival or ActionInterfaceRemoval)
            Queue(SettleWait);
        return 0;
    }

    [StructLayout(LayoutKind.Sequential)]
    private unsafe struct NotifyFilter
    {
        public uint Size, Flags, FilterType, Reserved;
        public Guid ClassGuid;
        // The rest of the filter's union, which is sized for a device instance id of 200 characters.
        public fixed byte Rest[400 - 16];
    }

    private const uint FilterTypeDeviceInterface = 0, AllInterfaceClasses = 1;
    private const int ActionInterfaceArrival = 0, ActionInterfaceRemoval = 1;

    [LibraryImport("cfgmgr32.dll", EntryPoint = "CM_Register_Notification")]
    private static unsafe partial uint CM_Register_Notification(in NotifyFilter filter, IntPtr context,
        delegate* unmanaged<IntPtr, IntPtr, int, IntPtr, uint, uint> callback, out IntPtr notification);
}
