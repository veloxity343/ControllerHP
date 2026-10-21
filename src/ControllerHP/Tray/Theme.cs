using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace ControllerHP.Tray;

/// <summary>Colours for one of Windows' light or dark modes, modelled on Windows 11's own menus and flyouts.</summary>
internal sealed record Palette(
    bool Light,
    Color Background,
    Color Surface,
    Color Hover,
    Color Text,
    Color SubtleText,
    Color Separator,
    Color Border,
    Color Accent)
{
    public static readonly Palette Dark = new(
        Light: false,
        Background: Color.FromArgb(43, 43, 43),
        Surface: Color.FromArgb(50, 50, 50),
        Hover: Color.FromArgb(61, 61, 61),
        Text: Color.White,
        SubtleText: Color.FromArgb(160, 160, 160),
        Separator: Color.FromArgb(64, 64, 64),
        Border: Color.FromArgb(70, 70, 70),
        Accent: Color.FromArgb(52, 199, 89));

    public static readonly Palette LightMode = new(
        Light: true,
        Background: Color.FromArgb(249, 249, 249),
        Surface: Color.White,
        Hover: Color.FromArgb(234, 234, 234),
        Text: Color.FromArgb(26, 26, 26),
        SubtleText: Color.FromArgb(110, 110, 110),
        Separator: Color.FromArgb(222, 222, 222),
        Border: Color.FromArgb(212, 212, 212),
        Accent: Color.FromArgb(30, 150, 60));
}

/// <summary>Reads Windows' light/dark settings and applies Windows 11 window styling.</summary>
internal static partial class Theme
{
    private const string PersonalizeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    /// <summary>The taskbar's mode, which the tray icon, its menu and the flyout follow.</summary>
    public static bool SystemUsesLight() => ReadFlag("SystemUsesLightTheme");

    /// <summary>The mode for app windows, which the settings window follows.</summary>
    public static bool AppsUseLight() => ReadFlag("AppsUseLightTheme");

    public static Palette For(bool light) => light ? Palette.LightMode : Palette.Dark;

    private static bool ReadFlag(string name)
    {
        using var key = Registry.CurrentUser.OpenSubKey(PersonalizeKey);
        return key?.GetValue(name) is int value && value != 0;
    }

    private const int DwmwaUseImmersiveDarkMode = 20;
    private const int DwmwaWindowCornerPreference = 33;
    private const int DwmwaBorderColor = 34;

    public enum Corners { Default = 0, None = 1, Round = 2, RoundSmall = 3 }

    [LibraryImport("dwmapi.dll")]
    private static partial int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    /// <summary>Rounded corners and a border colour on Windows 11; ignored on Windows 10.</summary>
    public static void StyleWindow(IntPtr handle, Corners corners, Color? border = null)
    {
        int preference = (int)corners;
        _ = DwmSetWindowAttribute(handle, DwmwaWindowCornerPreference, ref preference, sizeof(int));
        if (border is Color c)
        {
            int colorRef = c.R | c.G << 8 | c.B << 16;
            _ = DwmSetWindowAttribute(handle, DwmwaBorderColor, ref colorRef, sizeof(int));
        }
    }

    /// <summary>Dark or light title bar for a normal window.</summary>
    public static void SetTitleBar(IntPtr handle, bool light)
    {
        int dark = light ? 0 : 1;
        _ = DwmSetWindowAttribute(handle, DwmwaUseImmersiveDarkMode, ref dark, sizeof(int));
    }
}
