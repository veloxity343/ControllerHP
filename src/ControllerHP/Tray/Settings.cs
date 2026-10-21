using Microsoft.Win32;

namespace ControllerHP.Tray;

/// <summary>User preferences, kept under HKCU\Software\ControllerHP.</summary>
internal static class Settings
{
    private const string KeyPath = @"Software\ControllerHP";

    public static ToastCorner ToastCorner
    {
        get => Enum.TryParse(Read(nameof(ToastCorner)) as string, out ToastCorner corner) && Enum.IsDefined(corner)
            ? corner
            : ToastCorner.TopRight;
        set => Write(nameof(ToastCorner), value.ToString());
    }

    /// <summary>How many controllers get their own tray icon; 0 means one per controller.</summary>
    public static int TrayIconLimit
    {
        get => Read(nameof(TrayIconLimit)) is int value && value >= 0 ? value : 1;
        set => Write(nameof(TrayIconLimit), value);
    }

    /// <summary>Whether connecting and disconnecting show a pop-up; battery warnings always do.</summary>
    public static bool ConnectionToasts
    {
        get => Read(nameof(ConnectionToasts)) is not int value || value != 0;
        set => Write(nameof(ConnectionToasts), value ? 1 : 0);
    }

    private static object? Read(string name)
    {
        using var key = Registry.CurrentUser.OpenSubKey(KeyPath);
        return key?.GetValue(name);
    }

    private static void Write(string name, object value)
    {
        using var key = Registry.CurrentUser.CreateSubKey(KeyPath);
        key.SetValue(name, value);
    }
}
