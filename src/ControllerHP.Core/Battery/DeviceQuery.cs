using System.Runtime.InteropServices;

namespace ControllerHP.Battery;

/// <summary>A device property's key: its property set and id, as written "{set} id".</summary>
internal readonly record struct DevPropKey(Guid Set, uint Id)
{
    public static DevPropKey Parse(string text)
    {
        int space = text.LastIndexOf(' ');
        return new(Guid.Parse(text[..space]), uint.Parse(text[(space + 1)..]));
    }

    public override string ToString() => $"{{{Set}}} {Id}";
}

/// <summary>One object a device query found: its id, and the properties asked for that it has.</summary>
internal sealed record DeviceObject(string Id, IReadOnlyDictionary<DevPropKey, object> Properties)
{
    public object? this[DevPropKey key] => Properties.GetValueOrDefault(key);

    /// <summary>Its display name, as Windows shows it, if <see cref="DeviceQuery.NameKeys"/> were asked for.</summary>
    public string Name => DeviceQuery.NameKeys.Select(k => this[k] as string).FirstOrDefault(n => !string.IsNullOrEmpty(n)) ?? "";
}

/// <summary>
/// Windows' device query API (DevQuery): lists devices or device interfaces with chosen properties,
/// filtered on their properties. It's what Windows' own device listings are built on, called directly.
/// </summary>
internal static unsafe partial class DeviceQuery
{
    public enum ObjectType
    {
        DeviceInterface = 1,
        Device = 3,
    }

    public static readonly DevPropKey Name = new(new("b725f130-47ef-101a-a5f1-02608c9eebac"), 10);
    public static readonly DevPropKey FriendlyName = new(new("a45c254e-df1c-4efd-8020-67d146a850e0"), 14);
    public static readonly DevPropKey Description = new(new("a45c254e-df1c-4efd-8020-67d146a850e0"), 2);

    /// <summary>The properties a device's display name is taken from, best first.</summary>
    public static readonly DevPropKey[] NameKeys = [Name, FriendlyName, Description];
    public static readonly DevPropKey InstanceId = new(new("78c34fc8-104a-4aca-9ea4-524d52996e57"), 256);
    public static readonly DevPropKey ContainerId = new(new("8c7ed206-3f8a-4827-b3ab-ae9e1faefc6c"), 2);
    public static readonly DevPropKey ClassGuid = new(new("a45c254e-df1c-4efd-8020-67d146a850e0"), 10);

    /// <summary>A filter on one property: its value begins with some text (ignoring case), or equals a GUID or flag.</summary>
    public readonly record struct Filter(DevPropKey Key, string? BeginsWith = null, Guid? EqualsGuid = null, bool? EqualsBool = null);

    /// <summary>Every object of a type that passes the filters, with the properties asked for.</summary>
    public static List<DeviceObject> Find(ObjectType type, DevPropKey[] properties, params Filter[] filters)
    {
        using var request = new Request(properties, filters);
        int hr = DevGetObjects(type, QueryFlagNone, (uint)properties.Length, request.Keys, (uint)filters.Length, request.Filters,
            out uint count, out DevObject* objects);
        if (hr < 0)
            throw new COMException("Device query failed", hr);
        try
        {
            var found = new List<DeviceObject>((int)count);
            for (int i = 0; i < count; i++)
                found.Add(ToObject(objects[i]));
            return found;
        }
        finally
        {
            DevFreeObjects(count, objects);
        }
    }

    private static DeviceObject ToObject(in DevObject obj)
    {
        var properties = new Dictionary<DevPropKey, object>();
        for (int i = 0; i < obj.PropertyCount; i++)
        {
            ref var property = ref obj.Properties[i];
            if (Decode(property.Type, property.Buffer, property.BufferSize) is { } value)
                properties[new DevPropKey(property.CompKey.Key.Set, property.CompKey.Key.Id)] = value;
        }
        return new DeviceObject(new string(obj.ObjectId), properties);
    }

    // Property types, as the device property API codes them.
    private const uint TypeSByte = 0x02, TypeByte = 0x03, TypeInt16 = 0x04, TypeUInt16 = 0x05, TypeInt32 = 0x06, TypeUInt32 = 0x07,
        TypeInt64 = 0x08, TypeUInt64 = 0x09, TypeGuid = 0x0D, TypeFileTime = 0x10, TypeBoolean = 0x11, TypeString = 0x12,
        TypeStringList = 0x2012;

    /// <summary>A property's value as the type it's stored as; null for types nothing here reads.</summary>
    private static object? Decode(uint type, byte* buffer, uint size)
    {
        if (buffer is null || size == 0)
            return null;
        return type switch
        {
            TypeSByte => (sbyte)*buffer,
            TypeByte => *buffer,
            TypeInt16 => *(short*)buffer,
            TypeUInt16 => *(ushort*)buffer,
            TypeInt32 => *(int*)buffer,
            TypeUInt32 => *(uint*)buffer,
            TypeInt64 => *(long*)buffer,
            TypeUInt64 => *(ulong*)buffer,
            TypeGuid => *(Guid*)buffer,
            TypeFileTime => DateTimeOffset.FromFileTime(*(long*)buffer),
            TypeBoolean => *buffer != 0,
            TypeString => new string((char*)buffer, 0, (int)(size / 2)).TrimEnd('\0'),
            TypeStringList => new string((char*)buffer, 0, (int)(size / 2)).Split('\0', StringSplitOptions.RemoveEmptyEntries),
            _ => null,
        };
    }

    /// <summary>The native form of the properties asked for and the filters, kept alive for the call.</summary>
    private sealed class Request : IDisposable
    {
        public readonly CompKey* Keys;
        public readonly FilterExpression* Filters;
        private readonly List<IntPtr> _allocations = [];

        public Request(DevPropKey[] properties, Filter[] filters)
        {
            // An empty list is passed as null.
            Keys = properties.Length == 0 ? null : (CompKey*)Alloc(sizeof(CompKey) * properties.Length);
            for (int i = 0; i < properties.Length; i++)
                Keys[i] = new CompKey { Key = new PropKey { Set = properties[i].Set, Id = properties[i].Id } };
            Filters = filters.Length == 0 ? null : (FilterExpression*)Alloc(sizeof(FilterExpression) * filters.Length);
            for (int i = 0; i < filters.Length; i++)
            {
                var filter = filters[i];
                var expression = new FilterExpression();
                expression.Property.CompKey = new CompKey { Key = new PropKey { Set = filter.Key.Set, Id = filter.Key.Id } };
                if (filter.BeginsWith is string text)
                {
                    expression.Operator = OperatorBeginsWith | OperatorIgnoreCase;
                    expression.Property.Type = TypeString;
                    expression.Property.BufferSize = (uint)((text.Length + 1) * 2);
                    expression.Property.Buffer = (byte*)Marshal.StringToHGlobalUni(text);
                    _allocations.Add((IntPtr)expression.Property.Buffer);
                }
                else if (filter.EqualsGuid is Guid guid)
                {
                    expression.Operator = OperatorEquals;
                    expression.Property.Type = TypeGuid;
                    expression.Property.BufferSize = 16;
                    var buffer = (Guid*)Alloc(16);
                    *buffer = guid;
                    expression.Property.Buffer = (byte*)buffer;
                }
                else if (filter.EqualsBool is bool flag)
                {
                    expression.Operator = OperatorEquals;
                    expression.Property.Type = TypeBoolean;
                    expression.Property.BufferSize = 1;
                    var buffer = (byte*)Alloc(1);
                    *buffer = flag ? (byte)0xFF : (byte)0;
                    expression.Property.Buffer = buffer;
                }
                Filters[i] = expression;
            }
        }

        private void* Alloc(int size)
        {
            var memory = Marshal.AllocHGlobal(size);
            new Span<byte>((void*)memory, size).Clear();
            _allocations.Add(memory);
            return (void*)memory;
        }

        public void Dispose()
        {
            foreach (var memory in _allocations)
                Marshal.FreeHGlobal(memory);
            _allocations.Clear();
        }
    }

    // The API's structures and constants (devquery.h, devpropdef.h, devfiltertypes.h).
    private const uint QueryFlagNone = 0;
    private const uint OperatorEquals = 2, OperatorBeginsWith = 9, OperatorIgnoreCase = 0x00020000;

    [StructLayout(LayoutKind.Sequential)]
    private struct PropKey
    {
        public Guid Set;
        public uint Id;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CompKey
    {
        public PropKey Key;
        public int Store; // 0, the system store
        public char* LocaleName;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DevProperty
    {
        public CompKey CompKey;
        public uint Type;
        public uint BufferSize;
        public byte* Buffer;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FilterExpression
    {
        public uint Operator;
        public DevProperty Property;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DevObject
    {
        public ObjectType Type;
        public char* ObjectId;
        public uint PropertyCount;
        public DevProperty* Properties;
    }

    [LibraryImport("api-ms-win-devices-query-l1-1-0.dll")]
    private static partial int DevGetObjects(ObjectType type, uint flags, uint propertyCount, CompKey* properties,
        uint filterCount, FilterExpression* filters, out uint objectCount, out DevObject* objects);

    [LibraryImport("api-ms-win-devices-query-l1-1-0.dll")]
    private static partial void DevFreeObjects(uint objectCount, DevObject* objects);
}
