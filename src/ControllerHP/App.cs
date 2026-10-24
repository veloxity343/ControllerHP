using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Themes.Fluent;

namespace ControllerHP;

/// <summary>The Avalonia application: no main window, just whatever <see cref="Startup"/> sets up.</summary>
internal sealed class App : Application
{
    /// <summary>Runs once Avalonia is ready, on the UI thread.</summary>
    public static Action? Startup { get; set; }

    /// <summary>Raised on the UI thread as the app exits, however it was asked to.</summary>
    public static event Action? Exiting;

    public override void Initialize() => Styles.Add(new FluentTheme());

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            // Exit, not ShutdownRequested: that one is only raised when Windows ends the session, not for Shutdown().
            desktop.Exit += (_, _) => Exiting?.Invoke();
        base.OnFrameworkInitializationCompleted();
        // Once the message loop is running, so the start-up work can also end it.
        Avalonia.Threading.Dispatcher.UIThread.Post(() => Startup?.Invoke());
    }

    /// <summary>Ends the app; <paramref name="exitCode"/> is what the process returns.</summary>
    public static void Exit(int exitCode = 0)
    {
        if (Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.Shutdown(exitCode);
    }
}
