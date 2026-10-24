using System.Runtime.InteropServices;

namespace WattsLeft;

internal enum FullscreenState
{
    None,
    /// <summary>A fullscreen app (often a borderless game) or presentation mode: pop-ups can still show over it.</summary>
    App,
    /// <summary>An exclusive-fullscreen game: nothing can show over it, so pop-ups would be lost.</summary>
    ExclusiveGame,
}

/// <summary>Asks Windows whether something is fullscreen, the same check it uses to hold its own notifications.</summary>
internal static partial class Fullscreen
{
    // QUERY_USER_NOTIFICATION_STATE values.
    private const int Busy = 2, RunningD3DFullScreen = 3, PresentationMode = 4;

    [LibraryImport("shell32.dll")]
    private static partial int SHQueryUserNotificationState(out int state);

    public static FullscreenState Query()
    {
        if (SHQueryUserNotificationState(out int state) != 0)
            return FullscreenState.None;
        return state switch
        {
            RunningD3DFullScreen => FullscreenState.ExclusiveGame,
            Busy or PresentationMode => FullscreenState.App,
            _ => FullscreenState.None,
        };
    }
}
