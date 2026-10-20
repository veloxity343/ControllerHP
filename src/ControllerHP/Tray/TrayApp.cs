using ControllerHP.Battery;
using Microsoft.Win32;

namespace ControllerHP.Tray;

/// <summary>Owns one tray icon per connected controller, or a placeholder icon when none are connected.</summary>
internal sealed class TrayApp : ApplicationContext
{
    // Connects, disconnects and Bluetooth battery changes arrive as events. Polling only catches battery
    // drift on sources without change events (USB / Xbox Wireless Adapter) and anything an event missed.
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(30);
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunValueName = "ControllerHP";

    private readonly IReadOnlyList<IBatterySource> _sources;
    private readonly ContextMenuStrip _menu = new();
    private readonly SynchronizationContext _ui;
    private readonly Dictionary<string, TrayEntry> _entries = [];
    private readonly TrayEntry _placeholder;
    private readonly System.Windows.Forms.Timer _timer = new();
    private readonly System.Windows.Forms.Timer _settleTimer = new();
    private readonly ControllerNotifier _notifier = new();
    private IReadOnlyList<ControllerBattery> _readings = [];
    private bool _lightTaskbar = IsLightTaskbar();

    public TrayApp(IReadOnlyList<IBatterySource> sources)
    {
        _sources = sources;
        // Creating the ContextMenuStrip installs the WinForms SynchronizationContext on this thread.
        _ui = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();
        _menu.Opening += (_, _) => BuildMenu();

        _placeholder = new TrayEntry(_menu);
        _placeholder.Update(null, _lightTaskbar);

        foreach (var source in _sources)
            source.ControllersChanged += OnControllersChanged;
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;

        _timer.Interval = (int)PollInterval.TotalMilliseconds;
        _timer.Tick += (_, _) => Refresh();
        _timer.Start();

        // Re-check once a newly connected controller has settled, so its toast shows the settled reading.
        _settleTimer.Interval = (int)ControllerNotifier.SettleTime.TotalMilliseconds + 100;
        _settleTimer.Tick += (_, _) =>
        {
            _settleTimer.Stop();
            Refresh();
        };

        Refresh();
    }

    private void OnControllersChanged(object? sender, EventArgs e) => _ui.Post(_ => Refresh(), null);

    private void OnUserPreferenceChanged(object? sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category != UserPreferenceCategory.General)
            return;
        _ui.Post(_ =>
        {
            _lightTaskbar = IsLightTaskbar();
            _placeholder.Update(null, _lightTaskbar);
            Refresh();
        }, null);
    }

    private void Refresh()
    {
        var previous = _readings;
        _readings = ReadFirstAvailableSource();
        if (!_readings.SequenceEqual(previous))
            LogHistory(_readings);

        var seen = new HashSet<string>();
        foreach (var reading in _readings)
        {
            seen.Add(reading.Id);
            if (!_entries.TryGetValue(reading.Id, out var entry))
                _entries[reading.Id] = entry = new TrayEntry(_menu);
            entry.Update(reading, _lightTaskbar);
        }

        foreach (var id in _entries.Keys.Where(id => !seen.Contains(id)).ToList())
        {
            _entries[id].Dispose();
            _entries.Remove(id);
        }

        _placeholder.Visible = _entries.Count == 0;

        if (_notifier.Update(_readings))
            _settleTimer.Start();
    }

    /// <summary>Records every change so battery behaviour (step sizes, update latency) can be checked later.</summary>
    private static void LogHistory(IReadOnlyList<ControllerBattery> readings) =>
        Program.Log(readings.Count == 0 ? "no controllers" : string.Join(" | ", readings.Select(r => r.Describe())),
            Program.HistoryLogFile);

    /// <summary>
    /// Sources are ordered best-first; the first one that sees any controller wins so the same
    /// controller isn't shown twice (e.g. once by Windows.Gaming.Input and again by XInput).
    /// </summary>
    private IReadOnlyList<ControllerBattery> ReadFirstAvailableSource()
    {
        foreach (var source in _sources)
        {
            try
            {
                var readings = source.Read();
                if (readings.Count > 0)
                    return readings;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"{source.Name} failed: {ex}");
            }
        }
        return [];
    }

    private void BuildMenu()
    {
        _menu.Items.Clear();

        if (_readings.Count == 0)
            _menu.Items.Add(new ToolStripMenuItem("No controllers connected") { Enabled = false });
        foreach (var reading in _readings)
            _menu.Items.Add(new ToolStripMenuItem(reading.Describe()) { Enabled = false });

        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add(new ToolStripMenuItem("Refresh now", null, (_, _) => Refresh()));
        _menu.Items.Add(new ToolStripMenuItem("Start with Windows", null, (_, _) => SetStartWithWindows(!StartsWithWindows()))
        {
            Checked = StartsWithWindows(),
        });
        _menu.Items.Add(new ToolStripMenuItem("Exit", null, (_, _) => ExitThread()));
    }

    private static bool StartsWithWindows()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
        return key?.GetValue(RunValueName) is string;
    }

    private static void SetStartWithWindows(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath);
        if (enabled)
            key.SetValue(RunValueName, $"\"{Environment.ProcessPath}\"");
        else
            key.DeleteValue(RunValueName, throwOnMissingValue: false);
    }

    private static bool IsLightTaskbar()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
        return key?.GetValue("SystemUsesLightTheme") is int value && value != 0;
    }

    protected override void ExitThreadCore()
    {
        _timer.Dispose();
        _settleTimer.Dispose();
        _notifier.Dispose();
        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
        foreach (var source in _sources)
        {
            source.ControllersChanged -= OnControllersChanged;
            (source as IDisposable)?.Dispose();
        }
        foreach (var entry in _entries.Values)
            entry.Dispose();
        _placeholder.Dispose();
        _menu.Dispose();
        base.ExitThreadCore();
    }
}

/// <summary>A single tray icon and the reading it currently shows.</summary>
internal sealed class TrayEntry : IDisposable
{
    private const int MaxTooltipLength = 127;

    private readonly NotifyIcon _notifyIcon;
    private OwnedIcon? _image;
    private ControllerBattery? _shown;
    private bool? _shownLightTaskbar;

    public TrayEntry(ContextMenuStrip menu)
    {
        _notifyIcon = new NotifyIcon { ContextMenuStrip = menu, Visible = true };
    }

    public bool Visible
    {
        get => _notifyIcon.Visible;
        set => _notifyIcon.Visible = value;
    }

    /// <param name="battery">null shows the "no controllers" placeholder.</param>
    public void Update(ControllerBattery? battery, bool lightTaskbar)
    {
        if (_image is not null && battery == _shown && lightTaskbar == _shownLightTaskbar)
            return;

        var image = BatteryIconRenderer.Render(battery, lightTaskbar);
        _notifyIcon.Icon = image.Icon;
        _image?.Dispose();
        _image = image;

        string text = battery?.Describe() ?? "ControllerHP: no controllers connected";
        _notifyIcon.Text = text.Length > MaxTooltipLength ? text[..MaxTooltipLength] : text;

        _shown = battery;
        _shownLightTaskbar = lightTaskbar;
    }

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _image?.Dispose();
    }
}
