using Microsoft.Win32;

namespace ControllerHP.Tray;

/// <summary>
/// The full window behind the flyout's gear: navigation on the left, a page on the right. Follows
/// Windows' app mode, light or dark, and stays in step with the tray menu through <see cref="Settings"/>.
/// </summary>
internal sealed partial class SettingsWindow : Form, IMessageFilter
{
    private const int NavWidth = 220, PagePadding = 32, CardGap = 4, SectionGap = 24;

    private readonly List<NavItem> _nav = [];
    private readonly Panel _page = new() { AutoScroll = true };
    /// <summary>Each nav item's page: the controls stacked on it, shown only while it's selected.</summary>
    private readonly List<List<Control>> _pages = [];
    private int _current;
    private readonly int _controllersPage;
    private List<Control> CurrentPage => _pages[_current];
    private Palette _palette;
    /// <summary>Set for the --preview-settings render, which fixes the palette instead of following Windows.</summary>
    private readonly bool _fixedPalette;
    /// <summary>Where the history chart's data comes from; the preview swaps in sample data.</summary>
    private readonly Func<DateTime, List<HistorySeries>> _loadHistory;
    /// <summary>Showing sample data: never let it change the real settings.</summary>
    private readonly bool _sampleData;
    /// <summary>Set while controls are filled from Settings, so they don't write the same values back.</summary>
    private bool _loading;

    // The Settings page's controls, kept to refresh them when settings change elsewhere (the tray menu).
    private readonly SegmentedControl _iconLimit = new(["1", "2", "One per controller"], 130);
    private readonly SegmentedControl _corner = new(["Top left", "Top right", "Bottom left", "Bottom right"], 105);
    private readonly ToggleSwitch _connectionToasts = new();
    private readonly ToggleSwitch _holdInFullscreen = new();
    private readonly SwitchedStepper _low = new(Settings.MinWarning + Settings.WarningStep, Settings.MaxWarning, Settings.WarningStep);
    private readonly SwitchedStepper _critical = new(Settings.MinWarning, Settings.MaxWarning - Settings.WarningStep, Settings.WarningStep);
    private readonly ToggleSwitch _startWithWindows = new();

    // The Controllers page: each controller's battery history, its stats, and removing it.
    private readonly SegmentedControl _range = new(["2 hours", "6 hours", "24 hours", "7 days"], 95) { SelectedIndex = 1 };
    private static readonly TimeSpan[] Ranges =
        [TimeSpan.FromHours(2), TimeSpan.FromHours(6), TimeSpan.FromHours(24), TimeSpan.FromDays(7)];
    private readonly HistoryChart _chart = new();
    private readonly Note _statsNote = new("Averages and totals cover the range above. Predictions and health use recent history.");
    private readonly Note _historyNote = new($"Recorded whenever a controller's battery changes, and kept for {BatteryHistory.Kept.Days} days.");
    private readonly List<StatsCard> _statsCards = [];
    private readonly UndoBar _undoBar = new();
    /// <summary>Beside the dropdown, while any controllers are removed: lists them to restore.</summary>
    private readonly ActionButton _restoreButton = new("Restore removed");
    private List<(string Id, string Name, DateTime When)> _removed = [];
    private readonly Note _versionNote = new($"ControllerHP {Application.ProductVersion.Split('+')[0]}");
    /// <summary>
    /// Which controller's figures to show: whichever has the lowest battery until the user picks a
    /// controller or all of them, then their pick.
    /// </summary>
    private readonly DropDown _controllerPicker = new(340);
    private const string AllControllers = "all";
    /// <summary>A controller id or <see cref="AllControllers"/>; null follows the lowest battery.</summary>
    private string? _pickerChoice;
    /// <summary>What each of the picker's items stands for: a controller id, then <see cref="AllControllers"/>.</summary>
    private List<string> _pickerKeys = [];
    /// <summary>Keeps the chart's "now" edge moving while the Controllers page is open.</summary>
    private readonly System.Windows.Forms.Timer _historyTimer = new() { Interval = 30_000 };

    private static readonly int[] IconLimits = [1, 2, 0];
    private static readonly ToastCorner[] Corners =
        [ToastCorner.TopLeft, ToastCorner.TopRight, ToastCorner.BottomLeft, ToastCorner.BottomRight];

    public SettingsWindow(Palette? palette = null, Func<DateTime, List<HistorySeries>>? loadHistory = null)
    {
        _palette = palette ?? Theme.For(Theme.AppsUseLight());
        _fixedPalette = palette is not null;
        _loadHistory = loadHistory ?? BatteryHistory.Load;
        _sampleData = loadHistory is not null;
        Text = "ControllerHP";
        Icon = Icon.ExtractAssociatedIcon(Environment.ProcessPath!);
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.None; // the controls scale themselves
        KeyPreview = true;

        _ = Handle;
        ClientSize = new Size(S(780), S(600));
        MinimumSize = SizeFromClientSize(new Size(S(640), S(420)));

        Controls.Add(_page);
        AddPage("Settings", BuildSettingsPage());
        AddPage("Controllers", BuildControllersPage());
        _controllersPage = _pages.Count - 1;
        ShowPage(0);

        LoadValues();
        WireEvents();
        ApplyPalette();

        Settings.Changed += OnSettingsChanged;
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
    }

    private int S(float logicalPixels) => (int)Math.Round(logicalPixels * DeviceDpi / 96f);

    private void AddPage(string title, List<Control> items)
    {
        // Registered before anything is added: adding a control triggers a layout pass, which reads the pages.
        int index = _pages.Count;
        _pages.Add(items);
        foreach (var item in items)
        {
            item.Visible = false;
            _page.Controls.Add(item);
        }
        var nav = new NavItem(title);
        nav.Click += (_, _) => ShowPage(index);
        _nav.Add(nav);
        Controls.Add(nav);
    }

    public int PageCount => _pages.Count;


    public void ShowPage(int index)
    {
        _current = index;
        for (int i = 0; i < _pages.Count; i++)
        {
            _nav[i].Selected = i == index;
            foreach (var item in _pages[i])
                item.Visible = i == index && (item is not UndoBar bar || bar.Showing) && (item != _restoreButton || _removed.Count > 0);
        }
        _page.AutoScrollPosition = Point.Empty;
        if (CurrentPage.Contains(_chart))
        {
            ReloadHistory();
            _historyTimer.Start();
        }
        else
        {
            _historyTimer.Stop();
        }
        PerformLayout();
    }

    private List<Control> BuildSettingsPage()
    {
        var items = new List<Control> { new Heading("Settings", 26) };

        items.Add(new Heading("Tray", 15));
        items.Add(new SettingCard("Tray icons",
            "How many controllers get their own icon. The rest are listed when you hover or click.",
            _iconLimit, stacked: true));

        items.Add(new Heading("Pop-ups", 15));
        items.Add(new SettingCard("Position", "Which corner of the screen pop-ups appear in.", _corner, stacked: true));
        items.Add(new SettingCard("Connection pop-ups", "When a controller connects or disconnects.", _connectionToasts));
        items.Add(new SettingCard("Hold while a game is fullscreen", "Show them once the game closes.", _holdInFullscreen));

        items.Add(new Heading("Battery warnings", 15));
        items.Add(new Note("Pop-ups when a controller's battery runs down, whatever the connection pop-ups setting."));
        items.Add(new SettingCard("Low battery", "Warn when a controller drops to this level.", _low));
        items.Add(new SettingCard("Critical battery", "Warn again at this lower level.", _critical));

        items.Add(new Heading("Startup", 15));
        items.Add(new SettingCard("Start with Windows", "Run ControllerHP when you sign in.", _startWithWindows));

        items.Add(_versionNote);
        return items;
    }

    private List<Control> BuildControllersPage()
    {
        _range.SelectedChanged += (_, _) => ReloadHistory();
        _historyTimer.Tick += (_, _) => ReloadHistory();
        _controllerPicker.SelectedChanged += (_, _) =>
        {
            _pickerChoice = _pickerKeys[_controllerPicker.SelectedIndex];
            ReloadHistory();
        };
        _undoBar.ShownChanged += (_, _) =>
        {
            _undoBar.Visible = _undoBar.Showing && _current == _controllersPage;
            PerformLayout();
        };
        _restoreButton.Click += (_, _) => OpenRestoreList();
        return [new Heading("Controllers", 26), _undoBar, _range, _chart, _controllerPicker, _restoreButton, _statsNote, _historyNote];
    }

    private void ReloadHistory()
    {
        var now = DateTime.Now;
        var since = now - Ranges[_range.SelectedIndex];
        var full = _loadHistory(now - BatteryHistory.Kept).ToDictionary(s => s.Id);
        // Removed controllers stay out until they next connect, when the tray app unhides them.
        var hidden = Settings.HiddenControllers;
        var shown = _loadHistory(since).Where(s => !hidden.ContainsKey(s.Id)).ToList();
        _removed = RemovedControllers(full);
        _restoreButton.Text = $"Restore removed ({_removed.Count})";
        _restoreButton.Visible = _current == _controllersPage && _removed.Count > 0;
        _chart.EmptyMessage = shown.Count == 0 && hidden.Count > 0
            ? "Every controller with history here has been removed. Use Restore removed below to bring one back."
            : HistoryChart.NoHistoryMessage;
        _chart.SetData(shown, since, now);

        // A card per controller in the chart, its figures worked out from its whole retained history.
        var page = _pages[_controllersPage];
        foreach (var card in _statsCards)
        {
            page.Remove(card);
            _page.Controls.Remove(card);
            card.Dispose();
        }
        _statsCards.Clear();

        // The picker lists each controller, then all; until the user picks, it follows the lowest battery.
        if (_pickerChoice is not (null or AllControllers) && !shown.Any(s => s.Id == _pickerChoice))
            _pickerChoice = null; // the chosen controller isn't in this range
        string? lowest = LowestFirst(shown).FirstOrDefault()?.Id;
        string? showing = _pickerChoice ?? lowest;
        _pickerKeys = [.. shown.Select(s => s.Id), AllControllers];
        _controllerPicker.SetItems([.. shown.Select(s => s.Name), "All controllers"], Math.Max(0, _pickerKeys.IndexOf(showing ?? "")));
        _controllerPicker.Visible = _current == _controllersPage && shown.Count > 0;

        var cards = showing == AllControllers ? shown : shown.Where(s => s.Id == showing).ToList();

        int? lowWarning = Settings.LowWarningEnabled ? Settings.LowWarning : null;
        int insertAt = page.IndexOf(_statsNote) + 1;
        foreach (var series in cards)
        {
            var stats = BatteryStats.Compute(full.GetValueOrDefault(series.Id, series), since, now, lowWarning);
            var card = new StatsCard(series, stats) { Palette = _palette, Visible = _current == _controllersPage };
            card.RemoveRequested += (_, _) => Remove(series);
            _statsCards.Add(card);
            page.Insert(insertAt++, card);
            _page.Controls.Add(card);
        }
        _statsNote.Visible = _current == _controllersPage && shown.Count > 0;
        PerformLayout();
    }

    private void LoadValues()
    {
        _loading = true;
        _iconLimit.SelectedIndex = Math.Max(0, Array.IndexOf(IconLimits, Settings.TrayIconLimit));
        _corner.SelectedIndex = Math.Max(0, Array.IndexOf(Corners, Settings.ToastCorner));
        _connectionToasts.Checked = Settings.ConnectionToasts;
        _holdInFullscreen.Checked = Settings.HoldInFullscreen;
        _low.Switch.Checked = Settings.LowWarningEnabled;
        _low.Stepper.Enabled = _low.Switch.Checked;
        _critical.Switch.Checked = Settings.CriticalWarningEnabled;
        _critical.Stepper.Enabled = _critical.Switch.Checked;
        _low.Stepper.Value = Settings.LowWarning;
        _critical.Stepper.Maximum = _low.Stepper.Value - Settings.WarningStep;
        _critical.Stepper.Value = Settings.CriticalWarning;
        _startWithWindows.Checked = Settings.StartWithWindows;
        _loading = false;
    }

    private void WireEvents()
    {
        _iconLimit.SelectedChanged += (_, _) =>
        {
            if (!_loading)
                Settings.TrayIconLimit = IconLimits[_iconLimit.SelectedIndex];
        };
        _corner.SelectedChanged += (_, _) =>
        {
            if (!_loading)
                Settings.ToastCorner = Corners[_corner.SelectedIndex];
        };
        _connectionToasts.CheckedChanged += (_, _) =>
        {
            if (!_loading)
                Settings.ConnectionToasts = _connectionToasts.Checked;
        };
        _holdInFullscreen.CheckedChanged += (_, _) =>
        {
            if (!_loading)
                Settings.HoldInFullscreen = _holdInFullscreen.Checked;
        };
        _low.Stepper.ValueChanged += (_, _) =>
        {
            if (_loading)
                return;
            Settings.LowWarning = _low.Stepper.Value;
            // Keep critical below low; lowering low can push critical down with it.
            _critical.Stepper.Maximum = _low.Stepper.Value - Settings.WarningStep;
            if (_critical.Stepper.Value > _critical.Stepper.Maximum)
                _critical.Stepper.Value = _critical.Stepper.Maximum;
            _critical.Stepper.Invalidate();
        };
        _critical.Stepper.ValueChanged += (_, _) =>
        {
            if (!_loading)
                Settings.CriticalWarning = _critical.Stepper.Value;
        };
        _low.Switch.CheckedChanged += (_, _) =>
        {
            if (!_loading)
                Settings.LowWarningEnabled = _low.Switch.Checked;
        };
        _critical.Switch.CheckedChanged += (_, _) =>
        {
            if (!_loading)
                Settings.CriticalWarningEnabled = _critical.Switch.Checked;
        };
        _startWithWindows.CheckedChanged += (_, _) =>
        {
            if (!_loading)
                Settings.StartWithWindows = _startWithWindows.Checked;
        };
    }

    /// <summary>A change from the tray menu; setting a control to its current value raises nothing.</summary>
    /// <summary>A controller connected, disconnected or changed level: refresh the Controllers page if it's showing.</summary>
    public void ReadingsChanged()
    {
        if (_current == _controllersPage)
            ReloadHistory();
    }

    /// <summary>Hides a controller, with a few seconds to undo; Settings lists it for restoring later.</summary>
    private void Remove(HistorySeries series)
    {
        if (!ConfirmDialog.Ask(this, $"Remove {series.Name}?",
                "It'll be hidden from the Controllers page. Its history is kept, and it comes back by itself the next " +
                "time it connects. You can also restore it with Restore removed, on this page.",
                "Remove", _palette))
            return;
        if (_pickerChoice == series.Id)
            _pickerChoice = null;
        // The Remove button's card is about to be rebuilt, taking focus with it; left alone, Windows would move
        // focus to an arbitrary control and the page would scroll to it. Keep it on the dropdown, on screen.
        _controllerPicker.Focus();
        Settings.Hide(series.Id);
        _undoBar.Show($"Removed {series.Name}", () => Settings.Unhide(series.Id));
    }

    /// <summary>
    /// The removed controllers that still have history, oldest removal first, for the Restore removed button.
    /// Entries whose history has expired are forgotten.
    /// </summary>
    private List<(string Id, string Name, DateTime When)> RemovedControllers(Dictionary<string, HistorySeries> full)
    {
        var hidden = Settings.HiddenControllers;
        // Sample data doesn't hold the real controllers, so they'd all look expired: never forget then.
        var expired = hidden.Keys.Where(id => !full.ContainsKey(id)).ToList();
        if (expired.Count > 0 && !_sampleData)
            Settings.ForgetHidden(expired);
        return [.. hidden.Where(kv => full.ContainsKey(kv.Key)).OrderBy(kv => kv.Value).Select(kv => (kv.Key, full[kv.Key].Name, kv.Value))];
    }

    /// <summary>Lists removed controllers under the button; picking one restores it, or all of them at once.</summary>
    private void OpenRestoreList()
    {
        var removed = _removed.ToList();
        var labels = removed.Select(r => $"{r.Name}  ·  removed {r.When:d MMM}").ToList();
        if (removed.Count > 1)
            labels.Add("Restore all");
        ListPopup.Open(_restoreButton, labels, checkedIndex: -1, _palette, index =>
        {
            // Restoring the last one hides this button, taking focus with it: keep focus on the dropdown instead.
            if (removed.Count == 1 || index >= removed.Count)
                _controllerPicker.Focus();
            Settings.Unhide(index < removed.Count ? [removed[index].Id] : removed.Select(r => r.Id).ToList());
        });
    }

    private void OnSettingsChanged()
    {
        if (IsHandleCreated)
            BeginInvoke(() =>
            {
                LoadValues();
                if (_current == _controllersPage)
                    ReloadHistory();
                else
                    PerformLayout();
            });
    }

    private void OnUserPreferenceChanged(object? sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category == UserPreferenceCategory.General && IsHandleCreated && !_fixedPalette)
            BeginInvoke(() =>
            {
                _palette = Theme.For(Theme.AppsUseLight());
                ApplyPalette();
            });
    }

    /// <summary>
    /// The tray icon's order: on battery, lowest first; then charging; then disconnected or level unknown
    /// (history can't tell those apart). Ties keep the chart's order.
    /// </summary>
    private static List<HistorySeries> LowestFirst(IEnumerable<HistorySeries> series) =>
        [.. series.OrderBy(s => s.Points[^1] switch
            {
                { Percent: null } => 2,
                { Charging: true } => 1,
                _ => 0,
            })
            .ThenBy(s => s.Points[^1].Percent ?? 100)];

    // WinForms sends the mouse wheel to the focused control, usually a nav item outside the scrolling page,
    // so the page never scrolled. Catch the wheel for the whole window and scroll the page under the pointer.
    private const int WmMouseWheel = 0x020A;

    private struct NativePoint
    {
        public int X, Y;
    }

    [System.Runtime.InteropServices.LibraryImport("user32.dll")]
    private static partial IntPtr WindowFromPoint(NativePoint point);

    public bool PreFilterMessage(ref Message m)
    {
        if (m.Msg != WmMouseWheel || !Visible || Form.ActiveForm != this)
            return false;
        // An open dropdown list is its own window over the page: leave the wheel to it, so it scrolls the list.
        var cursor = Cursor.Position;
        if (Control.FromChildHandle(WindowFromPoint(new NativePoint { X = cursor.X, Y = cursor.Y })) is not { } under ||
            under.FindForm() != this)
            return false;
        var pointer = _page.PointToClient(Cursor.Position);
        if (!_page.ClientRectangle.Contains(pointer) || !_page.VerticalScroll.Visible)
            return false;
        // 120 per notch; touchpads send smaller amounts, so scroll in proportion rather than whole notches.
        int delta = (short)((long)m.WParam >> 16);
        int y = -_page.AutoScrollPosition.Y - delta * S(60) / 120;
        _page.AutoScrollPosition = new Point(0, Math.Max(0, y));
        return true;
    }

    protected override void OnActivated(EventArgs e)
    {
        base.OnActivated(e);
        Application.AddMessageFilter(this);
    }

    protected override void OnDeactivate(EventArgs e)
    {
        base.OnDeactivate(e);
        Application.RemoveMessageFilter(this);
    }

    private void ApplyPalette()
    {
        BackColor = _palette.Background;
        _page.BackColor = _palette.Background;
        Theme.SetTitleBar(Handle, _palette.Light);
        Theme.SetControlTheme(_page.Handle, _palette.Light);
        // Each control passes the palette on to the controls inside it.
        foreach (var control in _nav.Concat(_pages.SelectMany(p => p)).OfType<ThemedControl>())
            control.Palette = _palette;
        Invalidate(true);
    }

    protected override void OnLayout(LayoutEventArgs levent)
    {
        base.OnLayout(levent);
        if (_nav.Count == 0 || _pages.Count == 0)
            return;

        int navWidth = S(NavWidth);
        int y = S(PagePadding) + S(44); // level with the page title's baseline area
        foreach (var item in _nav)
        {
            item.Bounds = new Rectangle(S(12), y, navWidth - S(24), S(36));
            y += S(40);
        }
        _page.Bounds = new Rectangle(navWidth, 0, ClientSize.Width - navWidth, ClientSize.Height);
        LayoutPage();
    }

    /// <summary>Stacks the page's items top to bottom at the page's current width.</summary>
    private void LayoutPage()
    {
        int width = Math.Min(_page.ClientSize.Width - S(PagePadding) * 2, S(760));
        int x = S(PagePadding) - _page.HorizontalScroll.Value;
        int y = S(PagePadding) + _page.AutoScrollPosition.Y;
        Control? previous = null;
        foreach (var item in CurrentPage)
        {
            if (item is UndoBar { Showing: false })
                continue; // takes no room until it has something to say
            if (item == _restoreButton)
            {
                // Beside the dropdown rather than below it; hidden while nothing is removed.
                if (_restoreButton.Visible && previous is not null)
                {
                    // With every controller removed the dropdown hides, so the button takes its place.
                    _restoreButton.Location = previous.Visible
                        ? new Point(previous.Right + S(8), previous.Top + (previous.Height - _restoreButton.Height) / 2)
                        : previous.Location;
                }
                continue;
            }
            if (item is Heading && previous is not null)
                y += S(SectionGap) - S(CardGap);
            else if (item is DropDown && previous is HistoryChart)
                y += S(12); // the chart's card and the box below it read as one block without room between
            int height = item is SettingCard card ? card.PreferredHeight : item.Height;
            // Choice rows keep their own width; everything else spans the page.
            item.Bounds = new Rectangle(x, y, item is SegmentedControl or DropDown ? item.Width : width, height);
            y += height + S(CardGap);
            previous = item;
        }
        // Room below the last item, so the page can scroll it clear of the window edge.
        _page.AutoScrollMinSize = new Size(0, y - _page.AutoScrollPosition.Y + S(PagePadding));
        // Setting the same size again doesn't recalculate the scroll bars, which then keep a stale range
        // from before the page changed (e.g. ending above stats cards added since). Force it.
        _page.PerformLayout();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyCode == Keys.Escape)
            Close();
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _historyTimer.Dispose();
        Application.RemoveMessageFilter(this);
        Settings.Changed -= OnSettingsChanged;
        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
        base.OnFormClosed(e);
    }
}
