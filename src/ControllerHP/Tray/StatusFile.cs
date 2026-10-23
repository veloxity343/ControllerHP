using System.Text.Json;
using ControllerHP.Battery;

namespace ControllerHP.Tray;

/// <summary>
/// status.json: every connected device's battery, kept up to date for other apps (Rainmeter skins,
/// scripts and the like) when the "Share battery levels" setting is on. It's replaced in one step, so a
/// reader never sees half a file.
/// </summary>
internal static class StatusFile
{
    public const string FileName = "status.json";
    /// <summary>Bumped if a field changes meaning or goes away; new fields don't bump it.</summary>
    private const int Format = 1;

    public static string FullPath => Path.Combine(Program.LogDirectory, FileName);
    public const string DisplayPath = @"%LOCALAPPDATA%\ControllerHP\" + FileName;

    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };
    /// <summary>What was last written, less the time, so an unchanged state isn't rewritten every check.</summary>
    private static string? _written;
    private static bool _failureLogged;

    public static void Write(IReadOnlyList<ControllerBattery> readings, IReadOnlyDictionary<string, string> timeLeft,
        bool running = true)
    {
        var devices = readings.Select(r => new
        {
            id = r.Id,
            name = r.Name,
            deviceName = r.DeviceName,
            type = r.Kind switch
            {
                DeviceKind.Gamepad => "controller",
                DeviceKind.Mouse => "mouse",
                DeviceKind.Keyboard => "keyboard",
                DeviceKind.Headset => "headset",
                _ => "other",
            },
            percent = r.EffectivePercent,
            // false when the source only reports a coarse level (empty, low, medium, full).
            exact = r.Percent is not null,
            power = r.Power switch
            {
                ChargeState.Discharging => "battery",
                ChargeState.Charging => "charging",
                ChargeState.Wired => "wired",
                _ => "unknown",
            },
            connection = r.Connection switch
            {
                Connection.Usb => "usb",
                Connection.Bluetooth => "bluetooth",
                Connection.WirelessAdapter => "wireless-adapter",
                Connection.Receiver => "receiver",
                _ => "unknown",
            },
            timeLeft = timeLeft.GetValueOrDefault(r.Id),
        }).ToList();

        string state = JsonSerializer.Serialize(new { running, devices });
        // Rewritten if it's gone, say someone deleted it, even though nothing changed.
        if (state == _written && File.Exists(FullPath))
            return;

        string json = JsonSerializer.Serialize(new
        {
            app = "ControllerHP",
            format = Format,
            version = Application.ProductVersion.Split('+')[0],
            running,
            // Lets a reader tell a running ControllerHP from one that crashed with running still true.
            pid = Environment.ProcessId,
            updated = DateTimeOffset.Now,
            devices,
        }, Indented);

        try
        {
            Directory.CreateDirectory(Program.LogDirectory);
            string temp = FullPath + ".tmp";
            File.WriteAllText(temp, json);
            File.Move(temp, FullPath, overwrite: true);
            _written = state;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Most likely a reader has it open without sharing; the next check tries again.
            if (!_failureLogged)
                Program.Log($"Couldn't write {FileName}: {e.Message}");
            _failureLogged = true;
        }
    }

    public static void Delete()
    {
        _written = null;
        try
        {
            File.Delete(FullPath);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Program.Log($"Couldn't delete {FileName}: {e.Message}");
        }
    }
}
