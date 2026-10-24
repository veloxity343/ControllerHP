using Microsoft.Win32;

namespace WattsLeft;

/// <summary>
/// Carries an install from WattsLeft's old name, ControllerHP, over on its first start: the settings, the
/// folder of history and logs, and Start with Windows. Each is moved rather than copied, so nothing is left
/// behind under the old name, and anything already under the new name is kept.
/// </summary>
internal static class Migration
{
    private const string OldName = "ControllerHP";
    private const string OldKeyPath = @"Software\" + OldName;
    private const string OldMutexName = @"Local\" + OldName + ".SingleInstance";

    /// <summary>Whether ControllerHP is running, which would read devices and write history alongside.</summary>
    public static bool OldAppRunning()
    {
        if (!Mutex.TryOpenExisting(OldMutexName, out var mutex))
            return false;
        mutex.Dispose();
        return true;
    }

    public static void Run()
    {
        // The folder first: logging a failure would create the new folder, and it's simplest to move whole.
        string? folderError = Try("history folder", MoveFolder);
        string? settingsError = Try("settings", MoveSettings);
        string? startupError = Try("Start with Windows", MoveStartup);
        foreach (var error in new[] { folderError, settingsError, startupError }.OfType<string>())
            Log.Write($"Moving from {OldName}: {error}");
    }

    private static string? Try(string what, Action step)
    {
        try
        {
            step();
            return null;
        }
        catch (Exception ex)
        {
            return $"{what} failed: {ex.GetType().Name}: {ex.Message}";
        }
    }

    /// <summary>%LOCALAPPDATA%\ControllerHP's history, logs and status file.</summary>
    private static void MoveFolder()
    {
        string old = Path.Combine(Path.GetDirectoryName(Log.Folder)!, OldName);
        if (!Directory.Exists(old))
            return;
        if (!Directory.Exists(Log.Folder))
        {
            Directory.Move(old, Log.Folder);
            return;
        }
        // Both exist: bring over what the new folder doesn't have yet.
        foreach (var file in Directory.GetFiles(old))
        {
            string target = Path.Combine(Log.Folder, Path.GetFileName(file));
            if (!File.Exists(target))
                File.Move(file, target);
        }
        if (Directory.GetFileSystemEntries(old).Length == 0)
            Directory.Delete(old);
    }

    /// <summary>HKCU\Software\ControllerHP's values, then the old key itself.</summary>
    private static void MoveSettings()
    {
        using (var old = Registry.CurrentUser.OpenSubKey(OldKeyPath))
        {
            if (old is null)
                return;
            using var key = Registry.CurrentUser.CreateSubKey(Settings.KeyPath);
            var existing = key.GetValueNames().ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var name in old.GetValueNames().Where(n => !existing.Contains(n)))
                key.SetValue(name, old.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames)!, old.GetValueKind(name));
        }
        Registry.CurrentUser.DeleteSubKeyTree(OldKeyPath, throwOnMissingSubKey: false);
    }

    /// <summary>ControllerHP's Start with Windows entry, which points at its old exe, becomes WattsLeft's.</summary>
    private static void MoveStartup()
    {
        using (var run = Registry.CurrentUser.OpenSubKey(Settings.RunKeyPath, writable: true))
        {
            if (run?.GetValue(OldName) is null)
                return;
            run.DeleteValue(OldName, throwOnMissingValue: false);
        }
        Settings.StartWithWindows = true;
    }
}
