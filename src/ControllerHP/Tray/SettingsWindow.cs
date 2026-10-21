using Microsoft.Win32;

namespace ControllerHP.Tray;

/// <summary>
/// The full window behind the flyout's gear: navigation on the left, a page on the right. Follows
/// Windows' app mode, light or dark, and stays in step with the tray menu through <see cref="Settings"/>.
/// </summary>
internal sealed class SettingsWindow : Form
{
    private const int NavWidth = 220, PagePadding = 32, CardGap = 4, SectionGap = 24;

    private readonly List<NavItem> _nav = [];
    private readonly Panel _page = new() { AutoScroll = true };
    private readonly List<Control> _pageItems = [];
    private Palette _palette;
    /// <summary>Set for the --preview-settings render, which fixes the palette instead of following Windows.</summary>
    private readonly bool _fixedPalette;
    /// <summary>Set while controls are filled from Settings, so they don't write the same values back.</summary>
    private bool _loading;

    // The Settings page's controls, kept to refresh them when settings change elsewhere (the tray menu).
    private readonly SegmentedControl _iconLimit = new(["1", "2", "One per controller"], 130);
    private readonly SegmentedControl _corner = new(["Top left", "Top right", "Bottom left", "Bottom right"], 105);
    private readonly ToggleSwitch _connectionToasts = new();
    private readonly Stepper _lowWarning = new("", Settings.MinWarning + Settings.WarningStep, Settings.MaxWarning, Settings.WarningStep);
    private readonly Stepper _criticalWarning = new("", Settings.MinWarning, Settings.MaxWarning - Settings.WarningStep, Settings.WarningStep);
    private readonly ToggleSwitch _startWithWindows = new();

    private static readonly int[] IconLimits = [1, 2, 0];
    private static readonly ToastCorner[] Corners =
        [ToastCorner.TopLeft, ToastCorner.TopRight, ToastCorner.BottomLeft, ToastCorner.BottomRight];

    public SettingsWindow(Palette? palette = null)
    {
        _palette = palette ?? Theme.For(Theme.AppsUseLight());
        _fixedPalette = palette is not null;
        Text = "ControllerHP";
        Icon = Icon.ExtractAssociatedIcon(Environment.ProcessPath!);
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.None; // the controls scale themselves
        KeyPreview = true;

        _ = Handle;
        ClientSize = new Size(S(780), S(600));
        MinimumSize = SizeFromClientSize(new Size(S(640), S(420)));

        var nav = new NavItem("Settings") { Selected = true };
        _nav.Add(nav);
        Controls.Add(nav);
        Controls.Add(_page);

        BuildSettingsPage();
        LoadValues();
        WireEvents();
        ApplyPalette();

        Settings.Changed += OnSettingsChanged;
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
    }

    private int S(float logicalPixels) => (int)Math.Round(logicalPixels * DeviceDpi / 96f);

    private void BuildSettingsPage()
    {
        _pageItems.Add(new Heading("Settings", 26));

        _pageItems.Add(new Heading("Tray", 15));
        _pageItems.Add(new SettingCard("Tray icons",
            "How many controllers get their own icon. The rest are listed when you hover or click.",
            _iconLimit, stacked: true));

        _pageItems.Add(new Heading("Pop-ups", 15));
        _pageItems.Add(new SettingCard("Position", "Which corner of the screen pop-ups appear in.", _corner, stacked: true));
        _pageItems.Add(new SettingCard("Connection pop-ups", "When a controller connects or disconnects.", _connectionToasts));

        _pageItems.Add(new Heading("Battery warnings", 15));
        _pageItems.Add(new Note("These pop-ups always show, whatever the connection pop-ups setting."));
        _pageItems.Add(new SettingCard("Low battery", "Warn when a controller drops to this level.", _lowWarning));
        _pageItems.Add(new SettingCard("Critical battery", "Warn again at this level. Always below the low level.", _criticalWarning));

        _pageItems.Add(new Heading("Startup", 15));
        _pageItems.Add(new SettingCard("Start with Windows", "Run ControllerHP when you sign in.", _startWithWindows));

        string version = Application.ProductVersion.Split('+')[0];
        _pageItems.Add(new Note($"ControllerHP {version}"));

        foreach (var item in _pageItems)
            _page.Controls.Add(item);
    }

    private void LoadValues()
    {
        _loading = true;
        _iconLimit.SelectedIndex = Math.Max(0, Array.IndexOf(IconLimits, Settings.TrayIconLimit));
        _corner.SelectedIndex = Math.Max(0, Array.IndexOf(Corners, Settings.ToastCorner));
        _connectionToasts.Checked = Settings.ConnectionToasts;
        _lowWarning.Value = Settings.LowWarning;
        _criticalWarning.Maximum = _lowWarning.Value - Settings.WarningStep;
        _criticalWarning.Value = Settings.CriticalWarning;
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
        _lowWarning.ValueChanged += (_, _) =>
        {
            if (_loading)
                return;
            Settings.LowWarning = _lowWarning.Value;
            // Keep critical below low; lowering low can push critical down with it.
            _criticalWarning.Maximum = _lowWarning.Value - Settings.WarningStep;
            if (_criticalWarning.Value > _criticalWarning.Maximum)
                _criticalWarning.Value = _criticalWarning.Maximum;
            _criticalWarning.Invalidate();
        };
        _criticalWarning.ValueChanged += (_, _) =>
        {
            if (!_loading)
                Settings.CriticalWarning = _criticalWarning.Value;
        };
        _startWithWindows.CheckedChanged += (_, _) =>
        {
            if (!_loading)
                Settings.StartWithWindows = _startWithWindows.Checked;
        };
    }

    /// <summary>A change from the tray menu; setting a control to its current value raises nothing.</summary>
    private void OnSettingsChanged()
    {
        if (IsHandleCreated)
            BeginInvoke(LoadValues);
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

    private void ApplyPalette()
    {
        BackColor = _palette.Background;
        _page.BackColor = _palette.Background;
        Theme.SetTitleBar(Handle, _palette.Light);
        Theme.SetControlTheme(_page.Handle, _palette.Light);
        foreach (var control in _nav.Concat(_pageItems).OfType<ThemedControl>())
        {
            control.Palette = _palette;
            foreach (var child in control.Controls.OfType<ThemedControl>())
                child.Palette = _palette;
        }
        Invalidate(true);
    }

    protected override void OnLayout(LayoutEventArgs levent)
    {
        base.OnLayout(levent);
        if (_nav.Count == 0)
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
        foreach (var item in _pageItems)
        {
            if (item is Heading && previous is not null)
                y += S(SectionGap) - S(CardGap);
            int height = item is SettingCard card ? card.PreferredHeight : item.Height;
            item.Bounds = new Rectangle(x, y, width, height);
            y += height + S(CardGap);
            previous = item;
        }
        // Room below the last item, so the page can scroll it clear of the window edge.
        _page.AutoScrollMinSize = new Size(0, y - _page.AutoScrollPosition.Y + S(PagePadding));
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyCode == Keys.Escape)
            Close();
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        Settings.Changed -= OnSettingsChanged;
        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
        base.OnFormClosed(e);
    }
}
