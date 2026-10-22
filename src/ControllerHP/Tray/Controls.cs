using System.Drawing.Drawing2D;

namespace ControllerHP.Tray;

/// <summary>
/// Base for the settings window's controls, which are drawn in a <see cref="Palette"/> because WinForms'
/// own controls don't follow Windows' dark mode. Sizes are logical pixels scaled to the monitor's DPI.
/// </summary>
internal abstract class ThemedControl : Control
{
    private Palette _palette = Palette.Dark;

    protected ThemedControl()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
    }

    /// <summary>Setting it passes it on to any themed controls inside, so nested controls follow theme changes.</summary>
    public Palette Palette
    {
        get => _palette;
        set
        {
            _palette = value;
            foreach (var child in Controls.OfType<ThemedControl>())
                child.Palette = value;
            OnPaletteChanged();
            Invalidate();
        }
    }

    protected virtual void OnPaletteChanged() { }

    protected override void OnEnabledChanged(EventArgs e)
    {
        base.OnEnabledChanged(e);
        Invalidate();
    }

    protected int S(float logicalPixels) => (int)Math.Round(logicalPixels * DeviceDpi / 96f);

    /// <summary>A fixed size at 96 DPI, for controls that size themselves; null leaves sizing to the parent.</summary>
    protected virtual Size? LogicalSize => null;

    /// <summary>Call at the end of a derived constructor, once <see cref="LogicalSize"/> can be computed.</summary>
    protected void ResizeForDpi()
    {
        if (LogicalSize is Size size)
            Size = new Size(S(size.Width), S(size.Height));
    }

    protected override void OnDpiChangedAfterParent(EventArgs e)
    {
        base.OnDpiChangedAfterParent(e);
        ResizeForDpi();
    }

    protected Font MakeFont(float logicalPixels, bool semibold = false) =>
        new(semibold ? "Segoe UI Semibold" : "Segoe UI", logicalPixels * DeviceDpi / 96f, GraphicsUnit.Pixel);

    protected const TextFormatFlags TextFlags = TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
        TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding;

    protected static GraphicsPath RoundedRect(RectangleF r, float radius)
    {
        float d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
        var path = new GraphicsPath();
        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    protected void Fill(Graphics g, RectangleF r, float radius, Color color)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var brush = new SolidBrush(color);
        using var path = RoundedRect(r, radius);
        g.FillPath(brush, path);
    }

    protected void Outline(Graphics g, RectangleF r, float radius, Color color, float width)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var pen = new Pen(color, width);
        using var path = RoundedRect(r, radius);
        g.DrawPath(pen, path);
    }

    /// <summary>Hover feedback for clickable controls.</summary>
    protected Point? Hover { get; private set; }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        Hover = e.Location;
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        Hover = null;
        Invalidate();
    }

    protected override void OnGotFocus(EventArgs e)
    {
        base.OnGotFocus(e);
        Invalidate();
    }

    protected override void OnLostFocus(EventArgs e)
    {
        base.OnLostFocus(e);
        Invalidate();
    }

    /// <summary>A keyboard focus ring, shown only when focus came from the keyboard.</summary>
    protected void DrawFocus(Graphics g, RectangleF r, float radius)
    {
        if (Focused && ShowFocusCues)
            Outline(g, RectangleF.Inflate(r, S(2), S(2)), radius + S(2), Palette.Text, S(1.5f));
    }
}

/// <summary>A page title or section heading.</summary>
internal sealed class Heading : ThemedControl
{
    private readonly float _size;

    public Heading(string text, float logicalSize)
    {
        Text = text;
        _size = logicalSize;
        ResizeForDpi();
    }

    protected override Size? LogicalSize => new Size(400, (int)(_size * 1.6f));

    protected override void OnPaint(PaintEventArgs e)
    {
        using var font = MakeFont(_size, semibold: true);
        TextRenderer.DrawText(e.Graphics, Text, font, ClientRectangle, Palette.Text, TextFlags);
    }
}

/// <summary>Small print, e.g. the version at the foot of a page.</summary>
internal sealed class Note : ThemedControl
{
    public Note(string text)
    {
        Text = text;
        ResizeForDpi();
    }

    protected override Size? LogicalSize => new Size(400, 20);

    protected override void OnPaint(PaintEventArgs e)
    {
        using var font = MakeFont(12);
        TextRenderer.DrawText(e.Graphics, Text, font, ClientRectangle, Palette.SubtleText, TextFlags);
    }
}

/// <summary>One entry in the window's left-hand navigation.</summary>
internal sealed class NavItem : ThemedControl
{
    private bool _selected;

    public NavItem(string text)
    {
        Text = text;
        Cursor = Cursors.Hand;
        TabStop = true;
    }

    public bool Selected
    {
        get => _selected;
        set
        {
            _selected = value;
            Invalidate();
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        var r = new RectangleF(0, S(2), Width, Height - S(4));
        if (Selected || Hover is not null)
            Fill(g, r, S(4), Palette.Hover);
        if (Selected)
            Fill(g, new RectangleF(0, Height / 2f - S(8), S(3), S(16)), S(1.5f), Palette.Accent);
        using var font = MakeFont(14);
        TextRenderer.DrawText(g, Text, font, new Rectangle(S(14), 0, Width - S(14), Height), Palette.Text, TextFlags);
        DrawFocus(g, r, S(4));
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyCode is Keys.Space or Keys.Enter)
            OnClick(EventArgs.Empty);
    }

    // Take focus when clicked, like the window's other controls, so focus never lingers on a control that's
    // scrolled out of view or on the other page.
    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        Focus();
    }
}

/// <summary>
/// A setting laid out like Windows 11's Settings: a rounded card with a title and description, and its
/// control on the right, or underneath when the control is wide.
/// </summary>
internal sealed class SettingCard : ThemedControl
{
    private readonly string _description;
    private readonly ThemedControl _control;
    private readonly bool _stacked;

    public SettingCard(string title, string description, ThemedControl control, bool stacked = false)
    {
        Text = title;
        _description = description;
        _control = control;
        _stacked = stacked;
        Controls.Add(control);
    }

    /// <summary>Height for the current DPI; the card lays out its control to match.</summary>
    public int PreferredHeight => _stacked ? S(70) + _control.Height + S(14) : S(70);

    // The control sits on the card, so whatever it doesn't paint (its anti-aliased edges) should blend
    // into the card. Set on every palette change; a stale colour shows as fringes after a theme switch.
    protected override void OnPaletteChanged() => _control.BackColor = Palette.Surface;

    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);
        _control.BackColor = Palette.Surface;
        _control.Location = _stacked
            ? new Point(S(16), S(62))
            : new Point(Width - _control.Width - S(16), (Height - _control.Height) / 2);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        Fill(g, new RectangleF(0, 0, Width, Height), S(6), Palette.Surface);
        int textRight = _stacked ? Width - S(16) : _control.Left - S(16);
        using var titleFont = MakeFont(14);
        using var descriptionFont = MakeFont(12);
        TextRenderer.DrawText(g, Text, titleFont, new Rectangle(S(16), S(14), textRight - S(16), S(20)),
            Enabled ? Palette.Text : Palette.SubtleText, TextFlags);
        TextRenderer.DrawText(g, _description, descriptionFont,
            new Rectangle(S(16), S(36), textRight - S(16), S(18)), Palette.SubtleText, TextFlags);
    }
}

/// <summary>A Windows 11 style on/off switch.</summary>
internal sealed class ToggleSwitch : ThemedControl
{
    private bool _checked;

    public event EventHandler? CheckedChanged;

    public ToggleSwitch()
    {
        Cursor = Cursors.Hand;
        TabStop = true;
        ResizeForDpi();
    }

    private readonly bool _compact;

    /// <summary>Just a smaller track, without the On/Off text, for switches set among other figures.</summary>
    public bool Compact
    {
        get => _compact;
        init
        {
            _compact = value;
            ResizeForDpi(); // the constructor sized it for the full-size switch
        }
    }

    protected override Size? LogicalSize => Compact ? new Size(32, 18) : new Size(84, 22);

    public bool Checked
    {
        get => _checked;
        set
        {
            if (_checked == value)
                return;
            _checked = value;
            Invalidate();
            CheckedChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    protected override void OnClick(EventArgs e)
    {
        base.OnClick(e);
        Focus();
        Checked = !Checked;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyCode == Keys.Space)
            Checked = !Checked;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        // Label on the left, switch on the right, as in Windows' Settings.
        int trackWidth = Compact ? Width - S(2) : S(40);
        if (!Compact)
        {
            using var font = MakeFont(13);
            TextRenderer.DrawText(g, Checked ? "On" : "Off", font,
                new Rectangle(0, 0, Width - trackWidth - S(10), Height), Enabled ? Palette.Text : Palette.SubtleText,
                TextFlags & ~TextFormatFlags.Left | TextFormatFlags.Right);
        }

        // Disabled, it keeps its position but loses its colour, so it doesn't look like it can be changed.
        var track = new RectangleF(Width - trackWidth - S(1), S(1), trackWidth, Height - S(2));
        float radius = track.Height / 2;
        if (Checked)
            Fill(g, track, radius, Enabled ? Palette.Accent : Palette.Border);
        else
            Outline(g, track, radius, Enabled ? Palette.SubtleText : Palette.Border, S(1));

        // The knob's inset scales with the track, so the compact switch keeps the same proportions.
        float inset = Compact ? 3 : 4;
        float knob = Checked ? track.Height - S(inset * 2) : track.Height - S(inset * 2 + 2);
        float x = Checked ? track.Right - S(inset) - knob : track.X + S(inset + 1);
        var knobColor = Checked ? (Palette.Light ? Color.White : Palette.Background) : Enabled ? Palette.SubtleText : Palette.Border;
        Fill(g, new RectangleF(x, track.Y + (track.Height - knob) / 2, knob, knob), knob / 2, knobColor);
        DrawFocus(g, track, radius);
    }
}

/// <summary>A row of mutually exclusive choices, like a radio group drawn as one pill.</summary>
internal sealed class SegmentedControl : ThemedControl
{
    private readonly string[] _options;
    private int _selected;

    public event EventHandler? SelectedChanged;

    public SegmentedControl(string[] options, int logicalSegmentWidth)
    {
        _options = options;
        SegmentWidth = logicalSegmentWidth;
        Cursor = Cursors.Hand;
        TabStop = true;
        ResizeForDpi();
    }

    protected override Size? LogicalSize => new Size(SegmentWidth * _options.Length, 32);

    private int SegmentWidth { get; }

    public int SelectedIndex
    {
        get => _selected;
        set
        {
            if (_selected == value)
                return;
            _selected = value;
            Invalidate();
            SelectedChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private int IndexAt(Point p) => Math.Clamp(p.X / Math.Max(1, Width / _options.Length), 0, _options.Length - 1);

    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);
        Focus();
        SelectedIndex = IndexAt(e.Location);
    }

    protected override bool IsInputKey(Keys keyData) => keyData is Keys.Left or Keys.Right || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyCode == Keys.Left)
            SelectedIndex = Math.Max(0, SelectedIndex - 1);
        else if (e.KeyCode == Keys.Right)
            SelectedIndex = Math.Min(_options.Length - 1, SelectedIndex + 1);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        var all = new RectangleF(S(0.5f), S(0.5f), Width - S(1), Height - S(1));
        Fill(g, all, S(5), Palette.Background);
        Outline(g, all, S(5), Palette.Border, S(1));

        using var font = MakeFont(13);
        float segment = (float)Width / _options.Length;
        for (int i = 0; i < _options.Length; i++)
        {
            var r = new RectangleF(i * segment + S(3), S(3), segment - S(6), Height - S(6));
            bool hovered = Hover is Point p && IndexAt(p) == i;
            if (i == SelectedIndex)
                Fill(g, r, S(3), Palette.Accent);
            else if (hovered)
                Fill(g, r, S(3), Palette.Hover);
            var textColor = i == SelectedIndex ? (Palette.Light ? Color.White : Palette.Background) : Palette.Text;
            TextRenderer.DrawText(g, _options[i], font, Rectangle.Round(r), textColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix |
                TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
        }
        DrawFocus(g, all, S(5));
    }
}

/// <summary>A value with − and + buttons, e.g. a battery warning level.</summary>
internal sealed class Stepper : ThemedControl
{
    private int _value;

    public event EventHandler? ValueChanged;

    public Stepper(string label, int min, int max, int step)
    {
        Text = label;
        Minimum = min;
        Maximum = max;
        Step = step;
        TabStop = true;
        ResizeForDpi();
    }

    private readonly bool _compact;

    /// <summary>A smaller box, for steppers set among other figures rather than in a settings row.</summary>
    public bool Compact
    {
        get => _compact;
        init
        {
            _compact = value;
            ResizeForDpi(); // the constructor sized it for the full-size box
        }
    }

    private int BoxWidth => Compact ? 84 : 110;
    private int ButtonWidth => Compact ? 24 : 32;

    protected override Size? LogicalSize => new Size(BoxWidth + (Text.Length == 0 ? 0 : 80), Compact ? 26 : 32);

    public int Minimum { get; set; }
    public int Maximum { get; set; }
    public int Step { get; }

    /// <summary>One step below <see cref="Minimum"/> means off, and shows as "Off".</summary>
    public bool CanTurnOff { get; init; }

    public bool IsOff => CanTurnOff && _value < Minimum;

    /// <summary>The value meaning off, when <see cref="CanTurnOff"/>.</summary>
    public int OffValue => Minimum - Step;

    private int Lowest => CanTurnOff ? OffValue : Minimum;

    public int Value
    {
        get => _value;
        set
        {
            value = Math.Clamp(value, Lowest, Math.Max(Lowest, Maximum));
            if (_value == value)
                return;
            _value = value;
            Invalidate();
            ValueChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    // Layout: label, then [ − | value | + ] on the right.
    private RectangleF Box => new(Width - S(BoxWidth), S(0.5f), S(BoxWidth) - S(1), Height - S(1));
    private RectangleF Minus => new(Box.X, Box.Y, S(ButtonWidth), Box.Height);
    private RectangleF Plus => new(Box.Right - S(ButtonWidth), Box.Y, S(ButtonWidth), Box.Height);

    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);
        Focus();
        if (Minus.Contains(e.Location))
            Value -= Step;
        else if (Plus.Contains(e.Location))
            Value += Step;
    }

    protected override bool IsInputKey(Keys keyData) =>
        keyData is Keys.Left or Keys.Right or Keys.Up or Keys.Down || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyCode is Keys.Left or Keys.Down)
            Value -= Step;
        else if (e.KeyCode is Keys.Right or Keys.Up)
            Value += Step;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        Cursor = Enabled && (Minus.Contains(e.Location) || Plus.Contains(e.Location)) ? Cursors.Hand : Cursors.Default;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        if (Text.Length > 0)
        {
            using var labelFont = MakeFont(13);
            TextRenderer.DrawText(g, Text, labelFont, new Rectangle(0, 0, (int)Box.X - S(8), Height), Palette.Text, TextFlags);
        }

        // Disabled: greyed out and inert, but still showing the level it'll use when switched back on.
        Fill(g, Box, S(5), Enabled ? Palette.Background : Palette.Surface);
        Outline(g, Box, S(5), Enabled ? Palette.Border : Palette.Separator, S(1));
        foreach (var (r, canStep) in new[] { (Minus, Value > Lowest), (Plus, Value < Maximum) })
        {
            if (Enabled && canStep && Hover is Point p && r.Contains(p))
                Fill(g, RectangleF.Inflate(r, -S(3), -S(3)), S(3), Palette.Hover);
        }

        var flags = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix |
            TextFormatFlags.SingleLine;
        Color Sign(bool canStep) => Enabled && canStep ? Palette.Text : Palette.Separator;
        using var signFont = MakeFont(Compact ? 14 : 16);
        TextRenderer.DrawText(g, "−", signFont, Rectangle.Round(Minus), Sign(Value > Lowest), flags);
        TextRenderer.DrawText(g, "+", signFont, Rectangle.Round(Plus), Sign(Value < Maximum), flags);
        using var valueFont = MakeFont(Compact ? 12 : 13, semibold: true);
        TextRenderer.DrawText(g, IsOff ? "Off" : $"{Value}%", valueFont,
            Rectangle.Round(new RectangleF(Minus.Right, Box.Y, Plus.X - Minus.Right, Box.Height)),
            Enabled ? Palette.Text : Palette.SubtleText, flags);
        DrawFocus(g, Box, S(5));
    }
}

/// <summary>A warning level with its own on/off switch; the level is greyed out and locked while off.</summary>
internal sealed class SwitchedStepper : ThemedControl
{
    public SwitchedStepper(int min, int max, int step)
    {
        Stepper = new Stepper("", min, max, step);
        Controls.Add(Stepper);
        Controls.Add(Switch);
        Switch.CheckedChanged += (_, _) => Stepper.Enabled = Switch.Checked;
        ResizeForDpi();
    }

    public Stepper Stepper { get; }
    public ToggleSwitch Switch { get; } = new();

    protected override Size? LogicalSize => new Size(110 + 16 + 84, 32);

    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);
        Stepper.Location = new Point(0, (Height - Stepper.Height) / 2);
        Switch.Location = new Point(Width - Switch.Width, (Height - Switch.Height) / 2);
    }
}

/// <summary>
/// A themed dropdown: a box showing the current choice, opening the choices as a menu styled like the
/// tray menu. WinForms' ComboBox can't follow dark mode.
/// </summary>
internal sealed class DropDown : ThemedControl
{
    private IReadOnlyList<string> _items = [];
    private int _selected;
    private ListPopup? _open;

    public event EventHandler? SelectedChanged;

    public DropDown(int logicalWidth)
    {
        LogicalWidth = logicalWidth;
        Cursor = Cursors.Hand;
        TabStop = true;
        ResizeForDpi();
    }

    private int LogicalWidth { get; }

    protected override Size? LogicalSize => new Size(LogicalWidth, 32);

    /// <summary>Replaces the choices without raising <see cref="SelectedChanged"/>.</summary>
    public void SetItems(IReadOnlyList<string> items, int selected)
    {
        _items = items;
        _selected = Math.Clamp(selected, 0, Math.Max(0, items.Count - 1));
        Invalidate();
    }

    public int SelectedIndex => _selected;

    private void Open()
    {
        if (_open is { IsDisposed: false })
        {
            _open.Close(); // a second click on the box closes the list
            return;
        }
        _open = ListPopup.Open(this, _items, _selected, Palette, index =>
        {
            if (index == _selected)
                return;
            _selected = index;
            Invalidate();
            SelectedChanged?.Invoke(this, EventArgs.Empty);
        });
        if (_open is not null)
            _open.FormClosed += (_, _) => _open = null;
    }

    /// <summary>The page scrolled under an open list (with the scroll bar): close it rather than leave it floating.</summary>
    protected override void OnLocationChanged(EventArgs e)
    {
        base.OnLocationChanged(e);
        _open?.Close();
    }

    protected override void OnClick(EventArgs e)
    {
        base.OnClick(e);
        Focus();
        Open();
    }

    protected override bool IsInputKey(Keys keyData) => keyData is Keys.Down or Keys.Up || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyCode is Keys.Space or Keys.Enter or Keys.Down || (e.Alt && e.KeyCode == Keys.Down))
            Open();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        var box = new RectangleF(S(0.5f), S(0.5f), Width - S(1), Height - S(1));
        Fill(g, box, S(5), Hover is not null ? Palette.Hover : Palette.Background);
        Outline(g, box, S(5), Palette.Border, S(1));
        using var font = MakeFont(13);
        string text = _items.Count > 0 ? _items[_selected] : "";
        TextRenderer.DrawText(g, text, font, new Rectangle(S(12), 0, Width - S(44), Height), Palette.Text, TextFlags);
        // Segoe MDL2 Assets' chevron, as in Windows' own dropdowns.
        using var chevron = new Font("Segoe MDL2 Assets", 10f * DeviceDpi / 96f, GraphicsUnit.Pixel);
        TextRenderer.DrawText(g, "", chevron, new Rectangle(Width - S(32), 0, S(20), Height), Palette.SubtleText,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        DrawFocus(g, box, S(5));
    }
}

/// <summary>A compact text button with a hover wash, e.g. Remove, Undo, Restore.</summary>
internal sealed class ActionButton : ThemedControl
{
    public ActionButton(string text)
    {
        Text = text;
        Cursor = Cursors.Hand;
        TabStop = true;
        ResizeForDpi();
    }

    protected override Size? LogicalSize => new Size(Math.Max(72, Text.Length * 8 + 24), 30);

    protected override void OnTextChanged(EventArgs e)
    {
        base.OnTextChanged(e);
        ResizeForDpi();
        Invalidate();
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        base.OnEnabledChanged(e);
        Cursor = Enabled ? Cursors.Hand : Cursors.Default;
    }

    // Take focus when clicked, like any button. Otherwise focus stays on whatever control had it, and when a
    // list or dialog this opened closes, focus returns there and the page scrolls to it (e.g. to the top).
    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (Enabled)
            Focus();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (Enabled && e.KeyCode is Keys.Space or Keys.Enter)
            OnClick(EventArgs.Empty);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        var box = new RectangleF(S(0.5f), S(0.5f), Width - S(1), Height - S(1));
        Fill(g, box, S(5), Enabled && Hover is not null ? Palette.Hover : Palette.Background);
        Outline(g, box, S(5), Enabled ? Palette.Border : Palette.Separator, S(1));
        using var font = MakeFont(13);
        TextRenderer.DrawText(g, Text, font, ClientRectangle, Enabled ? Palette.Text : Palette.SubtleText,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
        DrawFocus(g, box, S(5));
    }
}

/// <summary>
/// "Removed X · Undo" after removing a controller, shown for a few seconds. Removing only hides, so undo
/// just unhides; the bar is the quick way back, and Settings has the lasting one.
/// </summary>
internal sealed class UndoBar : ThemedControl
{
    private static readonly TimeSpan ShownFor = TimeSpan.FromSeconds(10);
    private readonly ActionButton _undo = new("Undo");
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = (int)ShownFor.TotalMilliseconds };
    private Action? _onUndo;

    public UndoBar()
    {
        Controls.Add(_undo);
        _undo.Click += (_, _) =>
        {
            _onUndo?.Invoke();
            Dismiss();
        };
        _timer.Tick += (_, _) => Dismiss();
        Visible = false;
        ResizeForDpi();
    }

    protected override Size? LogicalSize => new Size(600, 48);

    /// <summary>Raised when the bar appears or goes, so the page can make or close the room for it.</summary>
    public event EventHandler? ShownChanged;

    public bool Showing { get; private set; }

    public void Show(string message, Action onUndo)
    {
        Text = message;
        _onUndo = onUndo;
        Showing = true;
        _timer.Stop();
        _timer.Start();
        Invalidate();
        ShownChanged?.Invoke(this, EventArgs.Empty);
    }

    private void Dismiss()
    {
        _timer.Stop();
        _onUndo = null;
        Showing = false;
        ShownChanged?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnPaletteChanged() => _undo.BackColor = Palette.Surface;

    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);
        _undo.BackColor = Palette.Surface;
        _undo.Location = new Point(Width - _undo.Width - S(10), (Height - _undo.Height) / 2);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        Fill(g, new RectangleF(0, 0, Width, Height), S(6), Palette.Surface);
        Fill(g, new RectangleF(0, S(10), S(3), Height - S(20)), S(1.5f), Palette.Accent);
        using var font = MakeFont(13);
        TextRenderer.DrawText(g, Text, font, new Rectangle(S(16), 0, _undo.Left - S(24), Height), Palette.Text, TextFlags);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _timer.Dispose();
        base.Dispose(disposing);
    }
}

/// <summary>
/// A themed list that drops down under a control: at most <see cref="VisibleRows"/> rows, scrolling for the
/// rest with the wheel, the arrow keys, Page Up/Down and Home/End, with a slim scroll bar to show where you
/// are. Picks with a click or Enter; Esc or clicking elsewhere closes it. WinForms' menus can't be both a
/// fixed width and a fixed height and still scroll, so this draws its own.
/// </summary>
internal sealed class ListPopup : Form
{
    public const int VisibleRows = 4;
    private const int RowHeight = 32, Inset = 4;

    // The list closes when it loses focus, which a click on its own button causes too. Remember which
    // button's list closed and when, so that click closes the list rather than reopening it.
    private static Control? s_lastAnchor;
    private static DateTime s_lastClosed;
    private static readonly TimeSpan ReopenGuard = TimeSpan.FromMilliseconds(300);
    private Control? _anchor;
    private const int WsExToolWindow = 0x00000080;

    private readonly IReadOnlyList<string> _items;
    private readonly int _checked;
    private readonly Action<int> _onPick;
    private readonly Palette _palette;
    private int _top, _focus, _hover = -1, _wheel;

    private ListPopup(IReadOnlyList<string> items, int checkedIndex, Action<int> onPick, Palette palette)
    {
        _items = items;
        _checked = checkedIndex;
        _onPick = onPick;
        _palette = palette;
        _focus = Math.Clamp(checkedIndex, 0, items.Count - 1);
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = false;
        KeyPreview = true;
        DoubleBuffered = true;
        BackColor = palette.Background;
        _ = Handle;
        Theme.StyleWindow(Handle, Theme.Corners.RoundSmall, palette.Border);
    }

    /// <summary>
    /// Opens the list under <paramref name="anchor"/>, at least as wide as it; null when this click is the one
    /// that just closed the anchor's list.
    /// </summary>
    public static ListPopup? Open(Control anchor, IReadOnlyList<string> items, int checkedIndex, Palette palette, Action<int> onPick)
    {
        if (s_lastAnchor == anchor && DateTime.UtcNow - s_lastClosed < ReopenGuard)
            return null;
        var popup = new ListPopup(items, checkedIndex, onPick, palette) { _anchor = anchor };
        using var font = popup.RowFont();
        int textWidth = items.Count == 0 ? 0 : items.Max(i => TextRenderer.MeasureText(i, font).Width);
        int width = Math.Max(anchor.Width, textWidth + popup.S(56));
        int rows = Math.Min(VisibleRows, Math.Max(1, items.Count));
        popup.ClientSize = new Size(width, rows * popup.S(RowHeight) + popup.S(Inset) * 2);
        popup.Location = anchor.PointToScreen(new Point(0, anchor.Height + popup.S(2)));
        popup.ScrollIntoView(popup._focus);
        popup.Show(anchor.FindForm());
        popup.Activate();
        return popup;
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= WsExToolWindow;
            return cp;
        }
    }

    private int S(float logicalPixels) => (int)Math.Round(logicalPixels * DeviceDpi / 96f);
    private Font RowFont() => new("Segoe UI", 13f * DeviceDpi / 96f, GraphicsUnit.Pixel);
    private int Rows => Math.Min(VisibleRows, _items.Count);
    private int MaxTop => Math.Max(0, _items.Count - VisibleRows);

    /// <summary>The first visible row, for checking the scroll position.</summary>
    public int FirstVisible => _top;

    private void ScrollTo(int top)
    {
        _top = Math.Clamp(top, 0, MaxTop);
        Invalidate();
    }

    private void ScrollIntoView(int index)
    {
        if (index < _top)
            ScrollTo(index);
        else if (index >= _top + Rows)
            ScrollTo(index - Rows + 1);
    }

    private int RowAt(Point p)
    {
        int row = (p.Y - S(Inset)) / S(RowHeight);
        return p.Y < S(Inset) || row >= Rows ? -1 : _top + row;
    }

    private void Pick(int index)
    {
        if (index < 0 || index >= _items.Count)
            return;
        Close();
        _onPick(index);
    }

    protected override void OnDeactivate(EventArgs e)
    {
        base.OnDeactivate(e);
        // Not straight away: destroying a window while Windows is still switching focus away from it can make
        // Windows bring forward another app instead of the settings window. Picking a row closes the list too,
        // which also lands here, so skip a list that's already closing.
        if (_closing || !IsHandleCreated)
            return;
        BeginInvoke(() =>
        {
            if (!IsDisposed)
                Close();
        });
    }

    private bool _closing;

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _closing = true;
        base.OnFormClosing(e);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        s_lastAnchor = _anchor;
        s_lastClosed = DateTime.UtcNow;
        base.OnFormClosed(e);
        // Hand focus back to the window the list belongs to, rather than leaving Windows to choose one (which
        // can be another app, sending ControllerHP to the background).
        if (Owner is { IsDisposed: false } owner)
            owner.Activate();
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        // 120 per notch; touchpads send smaller amounts, so accumulate them into whole rows.
        _wheel += e.Delta;
        int rows = _wheel / 120;
        _wheel %= 120;
        if (rows != 0)
            ScrollTo(_top - rows);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        int row = RowAt(e.Location);
        if (row != _hover)
        {
            _hover = row;
            Invalidate();
        }
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _hover = -1;
        Invalidate();
    }

    // A click is a press and release on the same row; dragging off the row cancels it.
    private int _pressed = -1;

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        _pressed = e.Button == MouseButtons.Left ? RowAt(e.Location) : -1;
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        int row = RowAt(e.Location);
        if (e.Button == MouseButtons.Left && row >= 0 && row == _pressed)
            Pick(row);
        _pressed = -1;
    }

    protected override bool ProcessDialogKey(Keys keyData)
    {
        int last = _items.Count - 1;
        int focus = keyData switch
        {
            Keys.Down => Math.Min(last, _focus + 1),
            Keys.Up => Math.Max(0, _focus - 1),
            Keys.PageDown => Math.Min(last, _focus + VisibleRows),
            Keys.PageUp => Math.Max(0, _focus - VisibleRows),
            Keys.End => last,
            Keys.Home => 0,
            _ => -1,
        };
        if (focus >= 0)
        {
            _focus = focus;
            _hover = -1;
            ScrollIntoView(focus);
            Invalidate();
            return true;
        }
        if (keyData is Keys.Enter or Keys.Space)
        {
            Pick(_focus);
            return true;
        }
        if (keyData == Keys.Escape)
        {
            Close();
            return true;
        }
        return base.ProcessDialogKey(keyData);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        bool scrolls = _items.Count > VisibleRows;
        int barSpace = scrolls ? S(10) : 0;
        using var font = RowFont();
        for (int row = 0; row < Rows; row++)
        {
            int index = _top + row;
            var r = new Rectangle(S(Inset), S(Inset) + row * S(RowHeight), ClientSize.Width - S(Inset) * 2 - barSpace, S(RowHeight));
            if (index == _hover || (_hover < 0 && index == _focus))
            {
                using var brush = new SolidBrush(_palette.Hover);
                using var path = Rounded(Rectangle.Inflate(r, 0, -S(2)), S(4));
                g.FillPath(brush, path);
            }
            if (index == _checked)
            {
                // A tick for the current choice, as in the tray menu.
                float cx = r.X + S(14), cy = r.Y + r.Height / 2f, s = S(5);
                using var pen = new Pen(_palette.Text, S(1.6f)) { StartCap = System.Drawing.Drawing2D.LineCap.Round, EndCap = System.Drawing.Drawing2D.LineCap.Round };
                g.DrawLines(pen, [new PointF(cx - s, cy), new PointF(cx - s / 3, cy + s * 0.65f), new PointF(cx + s, cy - s * 0.7f)]);
            }
            TextRenderer.DrawText(g, _items[index], font, new Rectangle(r.X + S(30), r.Y, r.Width - S(38), r.Height), _palette.Text,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
        }

        if (scrolls)
        {
            // A slim bar showing which part of the list is in view.
            int trackTop = S(Inset) + S(2), trackHeight = ClientSize.Height - trackTop * 2;
            int thumbHeight = Math.Max(S(16), trackHeight * VisibleRows / _items.Count);
            int thumbTop = trackTop + (trackHeight - thumbHeight) * _top / MaxTop;
            using var brush = new SolidBrush(_palette.SubtleText);
            using var path = Rounded(new Rectangle(ClientSize.Width - S(Inset) - S(5), thumbTop, S(4), thumbHeight), S(2));
            g.FillPath(brush, path);
        }
    }

    private static System.Drawing.Drawing2D.GraphicsPath Rounded(Rectangle r, int radius)
    {
        int d = Math.Max(1, radius * 2);
        var path = new System.Drawing.Drawing2D.GraphicsPath();
        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}
