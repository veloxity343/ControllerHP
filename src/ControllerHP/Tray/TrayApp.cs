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
    /// <summary>
    /// Tray icons are slots reused as controllers come and go, so they keep their place in the taskbar.
    /// The first slot always exists and shows the placeholder when nothing is connected.
    /// </summary>
    private readonly List<TrayEntry> _slots = [];
    private readonly List<string?> _slotIds = [];
    private int _iconLimit = Settings.TrayIconLimit;
    private readonly System.Windows.Forms.Timer _timer = new();
    private readonly System.Windows.Forms.Timer _settleTimer = new();
    private readonly ControllerNotifier _notifier = new();
    private IReadOnlyList<ControllerBattery> _readings = [];
    private bool _lightTaskbar = Theme.SystemUsesLight();

    public TrayApp(IReadOnlyList<IBatterySource> sources)
    {
        _sources = sources;
        // Creating the ContextMenuStrip installs the WinForms SynchronizationContext on this thread.
        _ui = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();
        _menu.Opening += (_, _) => BuildMenu();
        _notifier.Corner = Settings.ToastCorner;
        _notifier.ConnectionToasts = Settings.ConnectionToasts;

        _slots.Add(new TrayEntry(_menu));
        _slotIds.Add(null);
        _slots[0].Update(null, _lightTaskbar);

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
            _lightTaskbar = Theme.SystemUsesLight();
            Refresh();
        }, null);
    }

    private void Refresh()
    {
        var previous = _readings;
        _readings = ReadAllSources();
        if (!_readings.SequenceEqual(previous))
            LogHistory(_readings);

        UpdateSlots();

        if (_notifier.Update(_readings))
            _settleTimer.Start();
    }

    /// <summary>
    /// Shows the controllers that most need attention, up to the icon limit: lowest battery first,
    /// charging ones last. The rest are listed in the last icon's tooltip and in the menu.
    /// </summary>
    private void UpdateSlots()
    {
        var shown = Prioritise(_readings).ToList();
        var hidden = new List<ControllerBattery>();
        if (_iconLimit > 0 && shown.Count > _iconLimit)
        {
            hidden = shown[_iconLimit..];
            shown = shown[.._iconLimit];
        }

        int count = Math.Max(1, shown.Count);
        while (_slots.Count < count)
        {
            _slots.Add(new TrayEntry(_menu));
            _slotIds.Add(null);
        }
        while (_slots.Count > count)
        {
            _slots[^1].Dispose();
            _slots.RemoveAt(_slots.Count - 1);
            _slotIds.RemoveAt(_slotIds.Count - 1);
        }

        // Controllers still shown keep their slot; newcomers fill the free ones.
        var assigned = new ControllerBattery?[count];
        var unplaced = new List<ControllerBattery>();
        foreach (var reading in shown)
        {
            int slot = _slotIds.IndexOf(reading.Id);
            if (slot >= 0 && assigned[slot] is null)
                assigned[slot] = reading;
            else
                unplaced.Add(reading);
        }
        foreach (var reading in unplaced)
            assigned[Array.IndexOf(assigned, null)] = reading;

        string? more = hidden.Count == 0
            ? null
            : $"+{hidden.Count} more: {string.Join("; ", hidden.Select(r => r.Describe()))}";
        for (int i = 0; i < count; i++)
        {
            _slotIds[i] = assigned[i]?.Id;
            _slots[i].Update(assigned[i], _lightTaskbar, i == count - 1 ? more : null);
        }
    }

    /// <summary>
    /// Another controller takes a shown one's icon once it's this many points lower, so batteries that
    /// report in 1% steps don't trade places on every step, while 10% steps switch straight away.
    /// </summary>
    private const int TakeOverMargin = 3;

    private IEnumerable<ControllerBattery> Prioritise(IEnumerable<ControllerBattery> readings) =>
        readings
            .Select((reading, index) => (reading, index, shown: _slotIds.Contains(reading.Id)))
            .OrderBy(r => r.reading.Power is ChargeState.Charging or ChargeState.Wired ? 2
                : r.reading.EffectivePercent is null ? 1 : 0)
            // Half a point under the margin, so exactly the margin lower takes over.
            .ThenBy(r => (r.reading.EffectivePercent ?? 0) - (r.shown ? TakeOverMargin - 0.5 : 0))
            .ThenBy(r => r.shown ? 0 : 1)
            .ThenBy(r => r.index)
            .Select(r => r.reading);

    /// <summary>Records every change so battery behaviour (step sizes, update latency) can be checked later.</summary>
    private static void LogHistory(IReadOnlyList<ControllerBattery> readings) =>
        Program.Log(readings.Count == 0 ? "no controllers" : string.Join(" | ", readings.Select(r => r.Describe())),
            Program.HistoryLogFile);

    /// <summary>Each source covers different controllers; overlapping ones are grouped in a <see cref="FirstAvailableSource"/>.</summary>
    private List<ControllerBattery> ReadAllSources()
    {
        var readings = new List<ControllerBattery>();
        foreach (var source in _sources)
        {
            try
            {
                readings.AddRange(source.Read());
            }
            catch (Exception ex)
            {
                Program.LogOnce($"{source.Name} failed: {ex.GetType().Name}: {ex.Message}");
            }
        }
        return readings;
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

        var icons = new ToolStripMenuItem("Tray icons");
        foreach (var (limit, label) in IconLimitLabels)
        {
            icons.DropDownItems.Add(new ToolStripMenuItem(label, null, (_, _) => SetIconLimit(limit))
            {
                Checked = _iconLimit == limit,
            });
        }
        _menu.Items.Add(icons);

        var position = new ToolStripMenuItem("Pop-up position");
        foreach (var (corner, label) in CornerLabels)
        {
            position.DropDownItems.Add(new ToolStripMenuItem(label, null, (_, _) => SetToastCorner(corner))
            {
                Checked = _notifier.Corner == corner,
            });
        }
        _menu.Items.Add(position);
        _menu.Items.Add(new ToolStripMenuItem("Connection pop-ups", null, (_, _) =>
        {
            Settings.ConnectionToasts = _notifier.ConnectionToasts = !_notifier.ConnectionToasts;
        })
        {
            Checked = _notifier.ConnectionToasts,
        });
        _menu.Items.Add(new ToolStripMenuItem("Start with Windows", null, (_, _) => SetStartWithWindows(!StartsWithWindows()))
        {
            Checked = StartsWithWindows(),
        });
        _menu.Items.Add(new ToolStripMenuItem("Exit", null, (_, _) => ExitThread()));

        MenuRenderer.Apply(_menu, Theme.For(_lightTaskbar));
    }

    private static readonly (int Limit, string Label)[] IconLimitLabels =
        [(1, "1"), (2, "2"), (0, "One per controller")];

    private void SetIconLimit(int limit)
    {
        Settings.TrayIconLimit = _iconLimit = limit;
        UpdateSlots();
    }

    private static readonly (ToastCorner Corner, string Label)[] CornerLabels =
    [
        (ToastCorner.TopRight, "Top right"), (ToastCorner.TopLeft, "Top left"),
        (ToastCorner.BottomRight, "Bottom right"), (ToastCorner.BottomLeft, "Bottom left"),
    ];

    private void SetToastCorner(ToastCorner corner)
    {
        Settings.ToastCorner = _notifier.Corner = corner;
        _notifier.ShowSample();
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
        foreach (var slot in _slots)
            slot.Dispose();
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
    private string? _shownMore;

    public TrayEntry(ContextMenuStrip menu)
    {
        _notifyIcon = new NotifyIcon { ContextMenuStrip = menu, Visible = true };
    }

    /// <param name="battery">null shows the "no controllers" placeholder.</param>
    /// <param name="more">Controllers without an icon of their own, appended to the tooltip.</param>
    public void Update(ControllerBattery? battery, bool lightTaskbar, string? more = null)
    {
        if (_image is not null && battery == _shown && lightTaskbar == _shownLightTaskbar && more == _shownMore)
            return;

        var image = BatteryIconRenderer.Render(battery, lightTaskbar);
        _notifyIcon.Icon = image.Icon;
        _image?.Dispose();
        _image = image;

        string text = battery?.Describe() ?? "ControllerHP: no controllers connected";
        if (more is not null)
            text += "\n" + more;
        _notifyIcon.Text = text.Length > MaxTooltipLength ? text[..MaxTooltipLength] : text;

        _shown = battery;
        _shownLightTaskbar = lightTaskbar;
        _shownMore = more;
    }

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _image?.Dispose();
    }
}
