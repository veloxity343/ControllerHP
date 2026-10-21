using Microsoft.Win32;

namespace ControllerHP.Tray;

/// <summary>
/// User preferences, kept under HKCU\Software\ControllerHP. The tray menu and the settings window both
/// write here, and <see cref="Changed"/> tells the tray app to apply them.
/// </summary>
internal static class Settings
{
    private const string KeyPath = @"Software\ControllerHP";
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunValueName = "ControllerHP";

    /// <summary>Raised on the writing thread after any setting changes.</summary>
    public static event Action? Changed;

    public static ToastCorner ToastCorner
    {
        get => Enum.TryParse(Read(nameof(ToastCorner)) as string, out ToastCorner corner) && Enum.IsDefined(corner)
            ? corner
            : ToastCorner.TopRight;
        set => Write(nameof(ToastCorner), value.ToString());
    }

    /// <summary>How many controllers get their own tray icon: 1, 2, or 0 for one per controller.</summary>
    public static int TrayIconLimit
    {
        // 3 and 4 were options before; anything above 2 now means one per controller.
        get => Read(nameof(TrayIconLimit)) is int value && value >= 0 ? (value > 2 ? 0 : value) : 1;
        set => Write(nameof(TrayIconLimit), value);
    }

    /// <summary>Whether connecting and disconnecting show a pop-up; battery warnings always do.</summary>
    public static bool ConnectionToasts
    {
        get => Read(nameof(ConnectionToasts)) is not int value || value != 0;
        set => Write(nameof(ConnectionToasts), value ? 1 : 0);
    }

    public const int MinWarning = 5, MaxWarning = 50, WarningStep = 5;

    /// <summary>The "Battery low" warning level, in percent.</summary>
    public static int LowWarning
    {
        get => ReadWarning(nameof(LowWarning), 20, MinWarning + WarningStep, MaxWarning);
        set => Write(nameof(LowWarning), value);
    }

    /// <summary>The "Battery critical" warning level, in percent; always below <see cref="LowWarning"/>.</summary>
    public static int CriticalWarning
    {
        get => ReadWarning(nameof(CriticalWarning), 10, MinWarning, LowWarning - WarningStep);
        set => Write(nameof(CriticalWarning), value);
    }

    /// <summary>Whether the "Battery low" pop-up shows at all.</summary>
    public static bool LowWarningEnabled
    {
        get => Read(nameof(LowWarningEnabled)) is not int value || value != 0;
        set => Write(nameof(LowWarningEnabled), value ? 1 : 0);
    }

    /// <summary>Whether the "Battery critical" pop-up shows at all.</summary>
    public static bool CriticalWarningEnabled
    {
        get => Read(nameof(CriticalWarningEnabled)) is not int value || value != 0;
        set => Write(nameof(CriticalWarningEnabled), value ? 1 : 0);
    }

    private static int ReadWarning(string name, int fallback, int min, int max) =>
        Read(name) is int value ? Math.Clamp(value, min, max) : Math.Clamp(fallback, min, max);

    /// <summary>Whether Windows starts the app at sign-in, via the current user's Run key.</summary>
    public static bool StartWithWindows
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
            return key?.GetValue(RunValueName) is string;
        }
        set
        {
            using (var key = Registry.CurrentUser.CreateSubKey(RunKeyPath))
            {
                if (value)
                    key.SetValue(RunValueName, $"\"{Environment.ProcessPath}\"");
                else
                    key.DeleteValue(RunValueName, throwOnMissingValue: false);
            }
            Changed?.Invoke();
        }
    }

    private static object? Read(string name)
    {
        using var key = Registry.CurrentUser.OpenSubKey(KeyPath);
        return key?.GetValue(name);
    }

    private static void Write(string name, object value)
    {
        using (var key = Registry.CurrentUser.CreateSubKey(KeyPath))
            key.SetValue(name, value);
        Changed?.Invoke();
    }
}
