using System.Runtime.InteropServices;

namespace ControllerHP.Battery;

/// <summary>A HID collection that's plugged in: its path, and what it is.</summary>
internal readonly record struct HidInfo(string Path, ushort Vendor, ushort Product, ushort UsagePage, ushort Usage);

/// <summary>
/// The HID collections plugged in, kept up to date for every source that reads devices over HID. Windows'
/// configuration manager lists them, and <see cref="DeviceNotices"/> says when something arrives or goes;
/// each notice is answered by listing them again and comparing, so paths are always spelled as the list
/// spells them (the sources key devices, and some their history, by path). What a collection is comes from
/// the collection itself, opened with no access, which costs the device nothing.
/// </summary>
internal static partial class HidMonitor
{
    private static readonly Guid HidInterfaceClass = new("4D1E55B2-F16F-11CF-88CB-001111000030");

    private sealed class Subscription(Func<HidInfo, bool> wanted, Action<HidInfo> added, Action<string> removed) : IDisposable
    {
        public readonly Func<HidInfo, bool> Wanted = wanted;
        public readonly Action<HidInfo> Added = added;
        public readonly Action<string> Removed = removed;
        public readonly TaskCompletionSource Listed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        /// <summary>The paths this subscriber has been told of, so each hears of a removal only for its own.</summary>
        public readonly HashSet<string> Known = new(StringComparer.OrdinalIgnoreCase);

        public void Dispose()
        {
            lock (Gate)
                Subscribers.Remove(this);
        }
    }

    private static readonly object Gate = new();
    /// <summary>One listing at a time, events and all, so a subscriber hears of arrivals and removals in order.</summary>
    private static readonly object RescanGate = new();
    private static readonly List<Subscription> Subscribers = [];
    /// <summary>Every collection present at the last listing; null for one that couldn't be opened to identify.</summary>
    private static readonly Dictionary<string, HidInfo?> Present = new(StringComparer.OrdinalIgnoreCase);
    private static IDisposable? _notices;
    /// <summary>Set while a retry listing is queued.</summary>
    private static int _rescanQueued;
    /// <summary>
    /// How many listings in a row each collection couldn't be opened in. Windows announces a collection a moment
    /// before it can be opened, so one that can't is tried again every second, for a while.
    /// </summary>
    private static readonly Dictionary<string, int> Unopened = new(StringComparer.OrdinalIgnoreCase);
    private const int OpenAttempts = 15;
    private static readonly TimeSpan RetryWait = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Tells <paramref name="added"/> of every wanted collection now plugged in, then of each that arrives, and
    /// <paramref name="removed"/> of each of those that goes. Events come on worker threads. <paramref name="listed"/>
    /// completes once those already plugged in have been told of.
    /// </summary>
    public static IDisposable Watch(Func<HidInfo, bool> wanted, Action<HidInfo> added, Action<string> removed, out Task listed)
    {
        var subscription = new Subscription(wanted, added, removed);
        listed = subscription.Listed.Task;
        lock (Gate)
        {
            Subscribers.Add(subscription);
            _notices ??= DeviceNotices.Watch(Rescan);
        }
        _ = Task.Run(() =>
        {
            try
            {
                Rescan();
            }
            catch (Exception ex)
            {
                Log.Once($"Listing HID devices failed: {ex.GetType().Name}: {ex.Message}");
            }
            finally
            {
                subscription.Listed.TrySetResult();
            }
        });
        return subscription;
    }

    /// <summary>Every collection plugged in now, for diagnostics.</summary>
    public static IReadOnlyList<HidInfo> List()
    {
        Rescan();
        lock (Gate)
            return [.. Present.Values.OfType<HidInfo>()];
    }

    /// <summary>
    /// Lists the collections, identifies new ones, and tells each subscriber what arrived or went among those
    /// it wants. Calls into subscribers are made outside the lock, in order.
    /// </summary>
    private static void Rescan()
    {
        lock (RescanGate)
            RescanLocked();
    }

    private static void RescanLocked()
    {
        // A listing that failed says nothing about what's plugged in: keep what's known rather than report every
        // device gone, and try again shortly.
        if (ListPaths() is not { } paths)
        {
            Log.Once("Listing HID devices failed; trying again");
            QueueRescan(RetryWait);
            return;
        }
        var events = new List<Action>();
        bool retry = false;
        lock (Gate)
        {
            var current = new HashSet<string>(paths, StringComparer.OrdinalIgnoreCase);
            foreach (var gone in Present.Keys.Where(p => !current.Contains(p)).ToList())
            {
                Present.Remove(gone);
                Unopened.Remove(gone);
            }
            foreach (var path in paths)
            {
                if (Present.TryGetValue(path, out var known) && known is not null)
                    continue;
                Present[path] = Identify(path);
                if (Present[path] is not null)
                    Unopened.Remove(path);
                else
                    retry |= (Unopened[path] = Unopened.GetValueOrDefault(path) + 1) < OpenAttempts;
            }
            foreach (var subscriber in Subscribers)
            {
                foreach (var path in subscriber.Known.Where(p => !Present.ContainsKey(p)).ToList())
                {
                    subscriber.Known.Remove(path);
                    events.Add(() => subscriber.Removed(path));
                }
                foreach (var info in Present.Values.OfType<HidInfo>())
                {
                    if (!subscriber.Known.Contains(info.Path) && subscriber.Wanted(info))
                    {
                        subscriber.Known.Add(info.Path);
                        events.Add(() => subscriber.Added(info));
                    }
                }
            }
        }
        if (retry)
            QueueRescan(RetryWait);
        foreach (var raise in events)
        {
            try
            {
                raise();
            }
            catch (Exception ex)
            {
                Log.Once($"A HID device event failed: {ex.GetType().Name}: {ex.Message}");
            }
        }
    }

    private static HidInfo? Identify(string path)
    {
        using var handle = Hid.Open(path, 0);
        if (handle.IsInvalid)
            return null;
        var attributes = new Hid.HiddAttributes { Size = Marshal.SizeOf<Hid.HiddAttributes>() };
        if (!Hid.HidD_GetAttributes(handle, ref attributes) || Hid.GetCaps(handle) is not { } caps)
            return null;
        return new HidInfo(path, attributes.VendorID, attributes.ProductID, caps.UsagePage, caps.Usage);
    }

    // The configuration manager: listing a class's interfaces.
    private const uint CrSuccess = 0, CrBufferSmall = 0x1A;
    private const uint ListPresent = 0;

    [LibraryImport("cfgmgr32.dll", EntryPoint = "CM_Get_Device_Interface_List_SizeW", StringMarshalling = StringMarshalling.Utf16)]
    private static partial uint CM_Get_Device_Interface_List_Size(out uint length, in Guid interfaceClass, string? deviceId, uint flags);

    [LibraryImport("cfgmgr32.dll", EntryPoint = "CM_Get_Device_Interface_ListW", StringMarshalling = StringMarshalling.Utf16)]
    private static partial uint CM_Get_Device_Interface_List(in Guid interfaceClass, string? deviceId, char[] buffer, uint length, uint flags);

    /// <summary>The HID interfaces present, as the configuration manager spells their paths; null if it couldn't list them.</summary>
    private static List<string>? ListPaths()
    {
        // The list can grow between asking its size and reading it; ask again if so.
        for (int attempt = 0; attempt < 5; attempt++)
        {
            if (CM_Get_Device_Interface_List_Size(out uint length, HidInterfaceClass, null, ListPresent) != CrSuccess)
                break;
            var buffer = new char[length];
            uint result = CM_Get_Device_Interface_List(HidInterfaceClass, null, buffer, length, ListPresent);
            if (result == CrBufferSmall)
                continue;
            if (result != CrSuccess)
                break;
            // Paths separated by nulls, ending with an empty one.
            return [.. new string(buffer).Split('\0', StringSplitOptions.RemoveEmptyEntries)];
        }
        return null;
    }

    /// <summary>Lists the collections again after <paramref name="wait"/>, unless a listing is already queued.</summary>
    private static void QueueRescan(TimeSpan wait)
    {
        if (Interlocked.Exchange(ref _rescanQueued, 1) != 0)
            return;
        _ = Task.Run(async () =>
        {
            await Task.Delay(wait);
            Interlocked.Exchange(ref _rescanQueued, 0);
            try
            {
                Rescan();
            }
            catch (Exception ex)
            {
                Log.Once($"Listing HID devices failed: {ex.GetType().Name}: {ex.Message}");
            }
        });
    }
}
