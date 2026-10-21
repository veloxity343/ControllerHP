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

    public Palette Palette
    {
        get => _palette;
        set
        {
            _palette = value;
            Invalidate();
        }
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

    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);
        _control.Palette = Palette;
        // The control sits on the card, so whatever it doesn't paint should match the card.
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
        TextRenderer.DrawText(g, Text, titleFont, new Rectangle(S(16), S(14), textRight - S(16), S(20)), Palette.Text, TextFlags);
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

    protected override Size? LogicalSize => new Size(84, 22);

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
        using var font = MakeFont(13);
        int trackWidth = S(40);
        TextRenderer.DrawText(g, Checked ? "On" : "Off", font,
            new Rectangle(0, 0, Width - trackWidth - S(10), Height), Palette.Text,
            TextFlags & ~TextFormatFlags.Left | TextFormatFlags.Right);

        var track = new RectangleF(Width - trackWidth - S(1), S(1), trackWidth, Height - S(2));
        float radius = track.Height / 2;
        if (Checked)
            Fill(g, track, radius, Palette.Accent);
        else
            Outline(g, track, radius, Palette.SubtleText, S(1));

        float knob = Checked ? track.Height - S(8) : track.Height - S(10);
        float x = Checked ? track.Right - S(4) - knob : track.X + S(5);
        var knobColor = Checked ? (Palette.Light ? Color.White : Palette.Background) : Palette.SubtleText;
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

    protected override Size? LogicalSize => new Size(Text.Length == 0 ? 110 : 190, 32);

    public int Minimum { get; set; }
    public int Maximum { get; set; }
    public int Step { get; }

    public int Value
    {
        get => _value;
        set
        {
            value = Math.Clamp(value, Minimum, Maximum);
            if (_value == value)
                return;
            _value = value;
            Invalidate();
            ValueChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    // Layout: label, then [ − | value | + ] on the right.
    private RectangleF Box => new(Width - S(110), S(0.5f), S(110) - S(1), Height - S(1));
    private RectangleF Minus => new(Box.X, Box.Y, S(32), Box.Height);
    private RectangleF Plus => new(Box.Right - S(32), Box.Y, S(32), Box.Height);

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
        Cursor = Minus.Contains(e.Location) || Plus.Contains(e.Location) ? Cursors.Hand : Cursors.Default;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        if (Text.Length > 0)
        {
            using var labelFont = MakeFont(13);
            TextRenderer.DrawText(g, Text, labelFont, new Rectangle(0, 0, (int)Box.X - S(8), Height), Palette.Text, TextFlags);
        }

        Fill(g, Box, S(5), Palette.Background);
        Outline(g, Box, S(5), Palette.Border, S(1));
        foreach (var (r, enabled) in new[] { (Minus, Value > Minimum), (Plus, Value < Maximum) })
        {
            if (enabled && Hover is Point p && r.Contains(p))
                Fill(g, RectangleF.Inflate(r, -S(3), -S(3)), S(3), Palette.Hover);
        }

        var flags = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix |
            TextFormatFlags.SingleLine;
        using var signFont = MakeFont(16);
        TextRenderer.DrawText(g, "−", signFont, Rectangle.Round(Minus),
            Value > Minimum ? Palette.Text : Palette.Separator, flags);
        TextRenderer.DrawText(g, "+", signFont, Rectangle.Round(Plus),
            Value < Maximum ? Palette.Text : Palette.Separator, flags);
        using var valueFont = MakeFont(13, semibold: true);
        TextRenderer.DrawText(g, $"{Value}%", valueFont,
            Rectangle.Round(new RectangleF(Minus.Right, Box.Y, Plus.X - Minus.Right, Box.Height)), Palette.Text, flags);
        DrawFocus(g, Box, S(5));
    }
}
