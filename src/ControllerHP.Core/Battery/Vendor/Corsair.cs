namespace ControllerHP.Battery.Vendor;

/// <summary>
/// Corsair wireless headsets and the Dark Core RGB Pro SE mouse, on their receivers, beside iCUE.
/// </summary>
/// <remarks>
/// Two families. The Void v2 Wireless, HS80 Max and Virtuoso Max take 64-byte output reports on the
/// receiver's interface 4: 02, an endpoint (08 the receiver, 09 the headset behind it), 02, a command. A
/// sleeping headset is reached with a firmware query and two heartbeats (receiver, then headset), which
/// reads it without switching it into software mode; then command 0F reads the battery, bytes 4-5 a
/// little-endian 0..1000 value in tenths of a percent. A headset that doesn't answer its heartbeat is off.
///
/// The newer family (Dark Core RGB Pro SE, Virtuoso RGB Wireless SE and XT) routes a "get" to a property:
/// route (08 the receiver, 09 the device behind it), 02 get, property (0F battery, 10 charging). The answer
/// comes back routed to 01, 02, error 00, then the value little-endian (the Virtuoso puts a source byte
/// before the 02). It can take five seconds, and other apps' frames share the channel without saying which
/// property they answer, so a level is asked twice and taken only if both answers agree.
/// </remarks>
internal sealed class Corsair : VendorProtocol
{
    public override string Name => "Corsair";

    private const ushort CorsairVendor = 0x1B1C;
    public override IReadOnlyCollection<ushort> Vendors { get; } = [CorsairVendor];

    private enum Family { Void, DarkCore, Virtuoso, Older }

    private sealed record Model(Family Family, bool Session = false, bool WiredRoute = false);

    private static readonly Dictionary<ushort, (string Name, string Group, DeviceKind Kind, bool Cable, Model Model)> Models = new()
    {
        [0x2A08] = ("Corsair Void v2 Wireless", "2a08", DeviceKind.Headset, false, new(Family.Void)),
        [0x2A02] = ("Corsair Virtuoso Max Wireless", "2a02", DeviceKind.Headset, false, new(Family.Void)),
        [0x0A97] = ("Corsair HS80 Max Wireless", "0a97", DeviceKind.Headset, false, new(Family.Void)),
        [0x1B7F] = ("Corsair Dark Core RGB Pro SE", "1b7f", DeviceKind.Mouse, false, new(Family.DarkCore)),
        [0x0A40] = ("Corsair Virtuoso RGB Wireless SE", "virtuoso-se", DeviceKind.Headset, false, new(Family.Virtuoso, Session: true)),
        [0x0A3E] = ("Corsair Virtuoso RGB Wireless SE", "virtuoso-se", DeviceKind.Headset, false, new(Family.Virtuoso)),
        [0x0A3D] = ("Corsair Virtuoso RGB Wireless SE", "virtuoso-se", DeviceKind.Headset, true, new(Family.Virtuoso, WiredRoute: true)),
        [0x0A64] = ("Corsair Virtuoso RGB Wireless XT", "virtuoso-xt", DeviceKind.Headset, false, new(Family.Virtuoso)),
        [0x0A62] = ("Corsair Virtuoso RGB Wireless XT", "virtuoso-xt", DeviceKind.Headset, true, new(Family.Virtuoso, WiredRoute: true)),
    };

    /// <summary>Older wireless headsets (Void Wireless, Void Pro, HS70 and their kin), read with C9 64.</summary>
    private static readonly ushort[] OlderHeadsets =
        [0x1B27, 0x0A14, 0x0A16, 0x0A17, 0x0A1D, 0x0A1A, 0x1B2A, 0x1B23, 0x1B29, 0x0A55, 0x0A51, 0x0A52, 0x0A38, 0x0A4F, 0x0A2B, 0x0A75, 0x0A56];

    public override VendorModel? Identify(ushort vendor, ushort product) =>
        Models.TryGetValue(product, out var m) ? new VendorModel(m.Name, m.Group, m.Kind, m.Cable, m.Model)
        : OlderHeadsets.Contains(product) ? new VendorModel("Corsair wireless headset", $"{product:X4}", DeviceKind.Headset, Detail: new Model(Family.Older))
        : null;

    private const byte Receiver = 0x08, Behind = 0x09, AnswerRoute = 0x01, Get = 0x02;
    private const byte Firmware = 0x13, Heartbeat = 0x12, Battery = 0x0F, Charge = 0x10;
    private static readonly TimeSpan ReadWait = TimeSpan.FromMilliseconds(500), DrainWait = TimeSpan.FromMilliseconds(30);
    private static readonly TimeSpan FirstAnswer = TimeSpan.FromSeconds(6), ConfirmAnswer = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan SessionAnswer = TimeSpan.FromMilliseconds(800);

    public override async Task<VendorReading?> ReadAsync(VendorDevice device, CancellationToken token)
    {
        foreach (var (collection, model) in device.ByPreference)
        {
            var detail = (Model)model.Detail!;
            if (detail.Family == Family.Older)
            {
                if (collection.UsagePage != 0xFFC5 || collection.Usage != 0x0001)
                    continue;
                using var older = HidLink.Open(collection);
                if (older is not null && await ReadOlderAsync(older, token) is { } reading)
                    return reading;
                continue;
            }
            var candidates = detail.Family == Family.Void
                ? (collection.Interface == 4 ? [collection] : Array.Empty<HidCollection>())
                : (collection.UsagePage == 0xFF42 && collection.Usage == 0x0001 ? [collection] : Array.Empty<HidCollection>());
            foreach (var candidate in candidates)
            {
                using var link = HidLink.Open(candidate);
                if (link is null)
                    continue;
                var reading = detail.Family == Family.Void
                    ? await ReadVoidAsync(link, token)
                    : await ReadRoutedAsync(link, detail, token);
                if (reading is not null)
                    return reading;
            }
        }
        return null;
    }

    /// <summary>
    /// Output report C9 64 on the FFC5:0001 collection; the answer has the battery in byte 2 (bit 7 is the
    /// microphone up) and the state in byte 4: 0 off, 1 or 2 on battery, 4 or 5 charging.
    /// </summary>
    private static async Task<VendorReading?> ReadOlderAsync(HidLink link, CancellationToken token)
    {
        var r = await link.AskAsync(link.OutputReport(0xC9, [0x64]), r => r.Length >= 5 && r[4] is 0 or 1 or 2 or 4 or 5,
            ReadWait, token);
        if (r is null)
            return null;
        if (r[4] == 0)
            return VendorReading.SwitchedOff;
        int level = r[2] & 0x7F;
        return level > 100 ? null : new VendorReading(level, r[4] is 4 or 5 ? ChargeState.Charging : ChargeState.Discharging);
    }

    private static async Task<VendorReading?> ReadVoidAsync(HidLink link, CancellationToken token)
    {
        Task<bool> Send(byte endpoint, byte command) =>
            link.WriteAsync(link.OutputReport(0x00, [0x02, endpoint, 0x02, command]), token);

        await Send(Receiver, Firmware);
        await Send(Receiver, Heartbeat);
        await DrainAsync(link, token);
        await Send(Behind, Heartbeat);
        if (await link.ReadAsync(_ => true, ReadWait, token) is null)
            return VendorReading.SwitchedOff; // the headset didn't answer its heartbeat
        await DrainAsync(link, token);

        for (int attempt = 0; attempt < 3; attempt++)
        {
            await Send(Behind, Battery);
            var r = await link.ReadAsync(_ => true, ReadWait, token);
            // The receiver sometimes answers with the headset's id instead: 0 or above 1000 isn't a level.
            if (r is { Length: > 5 } && (r[4] | r[5] << 8) is int tenths and > 0 and <= 1000)
                return new VendorReading(tenths / 10, ChargeState.Discharging);
        }
        return null;
    }

    private static async Task<VendorReading?> ReadRoutedAsync(HidLink link, Model model, CancellationToken token)
    {
        // The Virtuoso receivers take a bare 64-byte frame with a leading 02; the Dark Core's takes a
        // report-id-0 frame.
        async Task<bool> Ask(byte route, byte property)
        {
            if (model.Family == Family.Virtuoso)
            {
                var bare = new byte[64];
                bare[0] = 0x02;
                bare[1] = route;
                bare[2] = Get;
                bare[3] = property;
                return await link.WriteAsync(bare, token);
            }
            return await link.WriteAsync(link.OutputReport(0x00, [route, Get, property]), token);
        }

        async Task<int?> AnswerAsync(TimeSpan window) =>
            await link.ReadAsync(r => Value(r) is not null, window, token) is { } r ? Value(r) : null;

        await DrainAsync(link, token);
        if (model.Session)
        {
            foreach (var (property, route) in new[] { (Firmware, Receiver), (Heartbeat, Receiver), (Heartbeat, Behind) })
            {
                if (!await Ask(route, property))
                    return null;
                await link.ReadAsync(_ => false, SessionAnswer, token);
            }
        }

        // A cable answers for itself first; a receiver for the device behind it, with the other as a fallback.
        byte[] routes = model.Family == Family.DarkCore ? [Behind] : model.WiredRoute ? [Receiver, Behind] : [Behind, Receiver];
        foreach (byte route in routes)
        {
            if (!await Ask(route, Battery))
                return null;
            if (await AnswerAsync(FirstAnswer) is not (int first and > 0 and <= 1000))
                continue;
            // A second ask must agree: another app's answer for another property could look the same.
            if (!await Ask(route, Battery))
                return null;
            int? second = await AnswerAsync(ConfirmAnswer);
            if (second is not null && second != first)
                return null;

            var power = ChargeState.Discharging;
            if (model.Family == Family.Virtuoso && await Ask(route, Charge) && await AnswerAsync(ConfirmAnswer) is int charge)
                power = charge == 1 ? ChargeState.Charging : ChargeState.Discharging; // 1 charging, 2 on battery
            return new VendorReading(first / 10, power);
        }
        return null;
    }

    /// <summary>
    /// The value in a routed answer: 01 02 00 &lt;value&gt; (the Dark Core) or 01 01 02 00 &lt;value&gt; (the Virtuoso),
    /// little-endian; null for anything else on the channel (device lists, notices, heartbeats).
    /// </summary>
    internal static int? Value(byte[] r)
    {
        if (r.Length < 5 || r[0] != AnswerRoute)
            return null;
        if (r[1] == Get && r[2] == 0x00)
            return r[3] | r[4] << 8;
        if (r.Length >= 6 && r[1] == AnswerRoute && r[2] == Get && r[3] == 0x00)
            return r[4] | r[5] << 8;
        return null;
    }

    /// <summary>Drops replies still queued, so the next read is the answer to the next request.</summary>
    private static async Task DrainAsync(HidLink link, CancellationToken token)
    {
        for (int i = 0; i < 4; i++)
        {
            if (await link.ReadAsync(_ => true, DrainWait, token) is null)
                return;
        }
    }
}
