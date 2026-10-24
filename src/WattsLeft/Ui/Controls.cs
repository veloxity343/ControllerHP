using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Rendering;
using Avalonia.Threading;

namespace WattsLeft.Ui;

/// <summary>A control drawn in a <see cref="Ui.Palette"/>, which the window sets on every themed control it holds.</summary>
internal interface IThemed
{
    Palette Palette { get; set; }
}

/// <summary>
/// Sizing for the settings page's items: how tall each is at a width (text wraps rather than being cut off),
/// and the narrowest it can be.
/// </summary>
internal interface IPageItem
{
    /// <summary>Its height when laid out this wide.</summary>
    double HeightFor(double width);

    /// <summary>The narrowest it can be laid out without cutting anything off; the window keeps at least this.</summary>
    double MinimumWidth { get; }

    /// <summary>Whether it keeps its own width, rather than spanning the page.</summary>
    bool OwnWidth { get; }
}

/// <summary>
/// Base for the settings window's controls, drawn in a palette because they follow Windows' light or dark
/// mode in its own colours. Sizes are logical pixels; Avalonia scales them to the monitor.
/// </summary>
internal abstract class ThemedControl : Control, IThemed, IPageItem, ICustomHitTest
{
    /// <summary>Clickable across its whole area, not just where it has drawn something (a nav item's text, say).</summary>
    public bool HitTest(Point point) => IsEffectivelyVisible && new Rect(Bounds.Size).Contains(point);

    private Palette _palette = Palette.Dark;
    private bool _keyboardFocus;

    public Palette Palette
    {
        get => _palette;
        set
        {
            _palette = value;
            OnPaletteChanged();
            InvalidateVisual();
        }
    }

    protected virtual void OnPaletteChanged() { }

    /// <summary>A fixed size, for controls that size themselves; null leaves the width to the page.</summary>
    protected virtual Size? FixedSize => null;

    public virtual double HeightFor(double width) => FixedSize?.Height ?? Bounds.Height;

    public virtual double MinimumWidth => OwnWidth ? FixedSize?.Width ?? 0 : 0;

    public virtual bool OwnWidth => false;

    protected override Size MeasureOverride(Size available)
    {
        if (FixedSize is Size fixedSize)
            return fixedSize;
        double width = double.IsInfinity(available.Width) ? 600 : available.Width;
        return new Size(width, HeightFor(width));
    }

    /// <summary>Hover feedback for clickable controls.</summary>
    protected Point? Hover { get; private set; }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        Hover = e.GetPosition(this);
        InvalidateVisual();
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        Hover = null;
        InvalidateVisual();
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        _keyboardFocus = e.NavigationMethod is NavigationMethod.Tab or NavigationMethod.Directional;
        InvalidateVisual();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        _keyboardFocus = false;
        InvalidateVisual();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        _keyboardFocus = false;
    }

    /// <summary>A keyboard focus ring, shown only when focus came from the keyboard.</summary>
    protected void DrawFocus(DrawingContext g, Rect r, double radius)
    {
        if (IsFocused && _keyboardFocus)
            g.DrawRectangle(null, new Pen(Draw.Brush(Palette.Text), 1.5), new RoundedRect(r.Inflate(2), radius + 2));
    }

    protected static void Fill(DrawingContext g, Rect r, double radius, Color color) => Draw.RoundedRect(g, color, r, radius);

    protected static void Outline(DrawingContext g, Rect r, double radius, Color color, double width) =>
        g.DrawRectangle(null, new Pen(Draw.Brush(color), width), new RoundedRect(r, radius));
}

/// <summary>A themed control that holds other controls, laying them out and drawing behind them itself.</summary>
internal abstract class ThemedPanel : Control, IThemed, IPageItem, ICustomHitTest
{
    public bool HitTest(Point point) => IsEffectivelyVisible && new Rect(Bounds.Size).Contains(point);

    private Palette _palette = Palette.Dark;

    protected ThemedPanel() => Children = new ChildList(this);

    /// <summary>The controls it holds; adding one makes it part of this control's visual and logical tree.</summary>
    public IList<Control> Children { get; }

    private sealed class ChildList(ThemedPanel owner) : System.Collections.ObjectModel.Collection<Control>
    {
        protected override void InsertItem(int index, Control item)
        {
            base.InsertItem(index, item);
            owner.VisualChildren.Add(item);
            owner.LogicalChildren.Add(item);
        }

        protected override void RemoveItem(int index)
        {
            var item = this[index];
            base.RemoveItem(index);
            owner.VisualChildren.Remove(item);
            owner.LogicalChildren.Remove(item);
        }
    }

    public Palette Palette
    {
        get => _palette;
        set
        {
            _palette = value;
            foreach (var child in Children.OfType<IThemed>())
                child.Palette = value;
            OnPaletteChanged();
            InvalidateVisual();
        }
    }

    protected virtual void OnPaletteChanged() { }

    public abstract double HeightFor(double width);

    public virtual double MinimumWidth => 0;

    public bool OwnWidth => false;

    protected override Size MeasureOverride(Size available)
    {
        foreach (var child in Children)
            child.Measure(Size.Infinity);
        double width = double.IsInfinity(available.Width) ? 600 : available.Width;
        return new Size(width, HeightFor(width));
    }

    protected static void Fill(DrawingContext g, Rect r, double radius, Color color) => Draw.RoundedRect(g, color, r, radius);
}

/// <summary>A page title or section heading.</summary>
internal sealed class Heading(string text, double size) : ThemedControl
{
    public override double HeightFor(double width) =>
        Math.Max(size * 1.6, Draw.TextHeight(text, Draw.Font(semibold: true), size, width) + size * 0.4);

    public override void Render(DrawingContext g) =>
        Draw.Wrapped(g, text, Draw.Font(semibold: true), size, Palette.Text, new Rect(Bounds.Size));
}

/// <summary>Small print, e.g. the version at the foot of a page.</summary>
internal sealed class Note(string text) : ThemedControl
{
    public override double HeightFor(double width) => Math.Max(20, Draw.TextHeight(text, Draw.Font(), 12, width) + 4);

    public override void Render(DrawingContext g) => Draw.Wrapped(g, text, Draw.Font(), 12, Palette.SubtleText, new Rect(Bounds.Size));
}

/// <summary>One entry in the window's left-hand navigation.</summary>
internal sealed class NavItem : ThemedControl
{
    private readonly string _text;
    private bool _selected;

    public event Action? Click;

    public NavItem(string text)
    {
        _text = text;
        Cursor = new Cursor(StandardCursorType.Hand);
        Focusable = true;
    }

    public bool Selected
    {
        get => _selected;
        set
        {
            _selected = value;
            InvalidateVisual();
        }
    }

    public override void Render(DrawingContext g)
    {
        var r = new Rect(0, 2, Bounds.Width, Bounds.Height - 4);
        if (Selected || Hover is not null)
            Fill(g, r, 4, Palette.Hover);
        if (Selected)
            Fill(g, new Rect(0, Bounds.Height / 2 - 8, 3, 16), 1.5, Palette.Accent);
        Draw.Text(g, _text, Draw.Font(), 14, Palette.Text, new Rect(14, 0, Bounds.Width - 14, Bounds.Height));
        DrawFocus(g, r, 4);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key is Key.Space or Key.Enter)
            Click?.Invoke();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        Focus();
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (e.InitialPressMouseButton == MouseButton.Left)
            Click?.Invoke();
    }
}

/// <summary>
/// A setting laid out like Windows 11's Settings: a rounded card with a title and description, and its
/// control on the right, or underneath when the control is wide or the card too narrow for both. The text
/// wraps rather than being cut off.
/// </summary>
internal sealed class SettingCard : ThemedPanel
{
    private readonly string _title, _description;
    private readonly Control _control;
    private readonly bool _stacked;

    /// <summary>Narrower than this beside its control, the text moves the control underneath it instead.</summary>
    private const double MinTextWidth = 180;

    public SettingCard(string title, string description, Control control, bool stacked = false)
    {
        _title = title;
        _description = description;
        _control = control;
        _stacked = stacked;
        Children.Add(control);
    }

    private double ControlWidth => _control.Width;
    private double ControlHeight => _control.Height;

    private bool StacksAt(double width) => _stacked || width - ControlWidth - 48 < MinTextWidth;

    private double TextWidthAt(double width) => StacksAt(width) ? width - 32 : width - ControlWidth - 48;

    /// <summary>The title and description's heights at this width, as wrapped.</summary>
    private (double Title, double Description) TextHeights(double width)
    {
        double textWidth = TextWidthAt(width);
        return (Math.Max(20, Draw.TextHeight(_title, Draw.Font(), 14, textWidth)),
            Math.Max(18, Draw.TextHeight(_description, Draw.Font(), 12, textWidth)));
    }

    /// <summary>The title and description together, with the gap between them.</summary>
    private double TextBlock(double width)
    {
        var (title, description) = TextHeights(width);
        return title + 2 + description;
    }

    public override double HeightFor(double width) => StacksAt(width)
        ? 14 + TextBlock(width) + 8 + ControlHeight + 22
        : Math.Max(70, TextBlock(width) + 30);

    public override double MinimumWidth => ControlWidth + 32;

    protected override Size ArrangeOverride(Size size)
    {
        var position = StacksAt(size.Width)
            ? new Point(16, 14 + TextBlock(size.Width) + 8)
            : new Point(size.Width - ControlWidth - 16, (size.Height - ControlHeight) / 2);
        _control.Arrange(new Rect(position, new Size(ControlWidth, ControlHeight)));
        return size;
    }

    public override void Render(DrawingContext g)
    {
        var size = Bounds.Size;
        Fill(g, new Rect(size), 6, Palette.Surface);
        double textWidth = TextWidthAt(size.Width);
        var (title, description) = TextHeights(size.Width);
        // Beside the control, the text is centred on the card; above it, it starts at the top.
        double top = StacksAt(size.Width) ? 14 : Math.Max(14, (size.Height - (title + 2 + description)) / 2);
        Draw.Wrapped(g, _title, Draw.Font(), 14, IsEffectivelyEnabled ? Palette.Text : Palette.SubtleText, new Rect(16, top, textWidth, title));
        Draw.Wrapped(g, _description, Draw.Font(), 12, Palette.SubtleText, new Rect(16, top + title + 2, textWidth, description));
    }
}

/// <summary>A Windows 11 style on/off switch.</summary>
internal sealed class ToggleSwitch : ThemedControl
{
    private bool _checked;
    private readonly bool _compact;

    public event EventHandler? CheckedChanged;

    public ToggleSwitch()
    {
        Cursor = new Cursor(StandardCursorType.Hand);
        Focusable = true;
        Width = 84;
        Height = 22;
    }

    /// <summary>Just a smaller track, without the On/Off text, for switches set among other figures.</summary>
    public bool Compact
    {
        get => _compact;
        init
        {
            _compact = value;
            (Width, Height) = value ? (32, 18) : (84, 22);
        }
    }

    protected override Size? FixedSize => new Size(Width, Height);

    public bool Checked
    {
        get => _checked;
        set
        {
            if (_checked == value)
                return;
            _checked = value;
            InvalidateVisual();
            CheckedChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (e.InitialPressMouseButton != MouseButton.Left)
            return;
        Focus();
        Checked = !Checked;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.Space)
            Checked = !Checked;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsEffectivelyEnabledProperty)
            InvalidateVisual();
    }

    public override void Render(DrawingContext g)
    {
        double width = Bounds.Width, height = Bounds.Height;
        bool enabled = IsEffectivelyEnabled;
        // Label on the left, switch on the right, as in Windows' Settings.
        double trackWidth = Compact ? width - 2 : 40;
        if (!Compact)
            Draw.Text(g, Checked ? "On" : "Off", Draw.Font(), 13, enabled ? Palette.Text : Palette.SubtleText,
                new Rect(0, 0, width - trackWidth - 10, height), TextAlignment.Right);

        // Disabled, it keeps its position but loses its colour, so it doesn't look like it can be changed.
        var track = new Rect(width - trackWidth - 1, 1, trackWidth, height - 2);
        double radius = track.Height / 2;
        if (Checked)
            Fill(g, track, radius, enabled ? Palette.Accent : Palette.Border);
        else
            Outline(g, track.Deflate(0.5), radius, enabled ? Palette.SubtleText : Palette.Border, 1);

        // The knob's inset scales with the track, so the compact switch keeps the same proportions.
        double inset = Compact ? 3 : 4;
        double knob = Checked ? track.Height - inset * 2 : track.Height - (inset * 2 + 2);
        double x = Checked ? track.Right - inset - knob : track.X + inset + 1;
        var knobColor = Checked ? (Palette.Light ? Colors.White : Palette.Background) : enabled ? Palette.SubtleText : Palette.Border;
        Fill(g, new Rect(x, track.Y + (track.Height - knob) / 2, knob, knob), knob / 2, knobColor);
        DrawFocus(g, track, radius);
    }
}

/// <summary>
/// Options side by side like <see cref="SegmentedControl"/>, but any number can be on: each one toggles on its
/// own. Arrow keys move between them, Space or Enter toggles.
/// </summary>
internal sealed class ChipGroup : ThemedControl
{
    private readonly string[] _options;
    private readonly bool[] _on;
    private int _focused;

    public event EventHandler? CheckedChanged;

    public ChipGroup(string[] options, double chipWidth)
    {
        _options = options;
        _on = new bool[options.Length];
        Cursor = new Cursor(StandardCursorType.Hand);
        Focusable = true;
        Width = chipWidth * options.Length;
        Height = 32;
    }

    protected override Size? FixedSize => new Size(Width, Height);

    public override bool OwnWidth => true;

    public bool this[int index]
    {
        get => _on[index];
        set
        {
            if (_on[index] == value)
                return;
            _on[index] = value;
            InvalidateVisual();
            CheckedChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private int IndexAt(Point p) => Math.Clamp((int)(p.X / Math.Max(1, Bounds.Width / _options.Length)), 0, _options.Length - 1);

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (e.InitialPressMouseButton != MouseButton.Left)
            return;
        Focus();
        _focused = IndexAt(e.GetPosition(this));
        this[_focused] = !this[_focused];
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.Left)
            _focused = Math.Max(0, _focused - 1);
        else if (e.Key == Key.Right)
            _focused = Math.Min(_options.Length - 1, _focused + 1);
        else if (e.Key is Key.Space or Key.Enter)
            this[_focused] = !this[_focused];
        else
            return;
        e.Handled = true;
        InvalidateVisual();
    }

    public override void Render(DrawingContext g)
    {
        var all = new Rect(0.5, 0.5, Bounds.Width - 1, Bounds.Height - 1);
        Fill(g, all, 5, Palette.Background);
        Outline(g, all, 5, Palette.Border, 1);

        double chip = Bounds.Width / _options.Length;
        for (int i = 0; i < _options.Length; i++)
        {
            var r = new Rect(i * chip + 3, 3, chip - 6, Bounds.Height - 6);
            bool hovered = Hover is Point p && IndexAt(p) == i;
            if (_on[i])
                Fill(g, r, 3, Palette.Accent);
            else if (hovered)
                Fill(g, r, 3, Palette.Hover);
            var text = _on[i] ? (Palette.Light ? Colors.White : Palette.Background) : Palette.SubtleText;
            Draw.Text(g, _options[i], Draw.Font(), 13, text, r, TextAlignment.Center);
            if (i == _focused)
                DrawFocus(g, r, 3);
        }
    }
}

/// <summary>A row of mutually exclusive choices, like a radio group drawn as one pill.</summary>
internal sealed class SegmentedControl : ThemedControl
{
    private readonly string[] _options;
    private int _selected;

    public event EventHandler? SelectedChanged;

    public SegmentedControl(string[] options, double segmentWidth)
    {
        _options = options;
        Cursor = new Cursor(StandardCursorType.Hand);
        Focusable = true;
        Width = segmentWidth * options.Length;
        Height = 32;
    }

    protected override Size? FixedSize => new Size(Width, Height);

    public override bool OwnWidth => true;

    public int SelectedIndex
    {
        get => _selected;
        set
        {
            if (_selected == value)
                return;
            _selected = value;
            InvalidateVisual();
            SelectedChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private int IndexAt(Point p) => Math.Clamp((int)(p.X / Math.Max(1, Bounds.Width / _options.Length)), 0, _options.Length - 1);

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (e.InitialPressMouseButton != MouseButton.Left)
            return;
        Focus();
        SelectedIndex = IndexAt(e.GetPosition(this));
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.Left)
            SelectedIndex = Math.Max(0, SelectedIndex - 1);
        else if (e.Key == Key.Right)
            SelectedIndex = Math.Min(_options.Length - 1, SelectedIndex + 1);
        else
            return;
        e.Handled = true;
    }

    public override void Render(DrawingContext g)
    {
        var all = new Rect(0.5, 0.5, Bounds.Width - 1, Bounds.Height - 1);
        Fill(g, all, 5, Palette.Background);
        Outline(g, all, 5, Palette.Border, 1);

        double segment = Bounds.Width / _options.Length;
        for (int i = 0; i < _options.Length; i++)
        {
            var r = new Rect(i * segment + 3, 3, segment - 6, Bounds.Height - 6);
            bool hovered = Hover is Point p && IndexAt(p) == i;
            if (i == SelectedIndex)
                Fill(g, r, 3, Palette.Accent);
            else if (hovered)
                Fill(g, r, 3, Palette.Hover);
            var text = i == SelectedIndex ? (Palette.Light ? Colors.White : Palette.Background) : Palette.Text;
            Draw.Text(g, _options[i], Draw.Font(), 13, text, r, TextAlignment.Center);
        }
        DrawFocus(g, all, 5);
    }
}

/// <summary>A value with − and + buttons, e.g. a battery warning level.</summary>
internal sealed class Stepper : ThemedControl
{
    private readonly string _label;
    private int _value;
    private readonly bool _compact;

    public event EventHandler? ValueChanged;

    public Stepper(string label, int min, int max, int step)
    {
        _label = label;
        Minimum = min;
        Maximum = max;
        Step = step;
        Focusable = true;
        Resize();
    }

    /// <summary>A smaller box, for steppers set among other figures rather than in a settings row.</summary>
    public bool Compact
    {
        get => _compact;
        init
        {
            _compact = value;
            Resize();
        }
    }

    private double BoxWidth => Compact ? 84 : 110;
    private double ButtonWidth => Compact ? 24 : 32;

    private void Resize() => (Width, Height) = (BoxWidth + (_label.Length == 0 ? 0 : 80), Compact ? 26 : 32);

    protected override Size? FixedSize => new Size(Width, Height);

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
            InvalidateVisual();
            ValueChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    // Layout: label, then [ − | value | + ] on the right.
    private Rect Box => new(Bounds.Width - BoxWidth, 0.5, BoxWidth - 1, Bounds.Height - 1);
    private Rect Minus => new(Box.X, Box.Y, ButtonWidth, Box.Height);
    private Rect Plus => new(Box.Right - ButtonWidth, Box.Y, ButtonWidth, Box.Height);

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (e.InitialPressMouseButton != MouseButton.Left)
            return;
        Focus();
        var point = e.GetPosition(this);
        if (Minus.Contains(point))
            Value -= Step;
        else if (Plus.Contains(point))
            Value += Step;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key is Key.Left or Key.Down)
            Value -= Step;
        else if (e.Key is Key.Right or Key.Up)
            Value += Step;
        else
            return;
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var point = e.GetPosition(this);
        Cursor = IsEffectivelyEnabled && (Minus.Contains(point) || Plus.Contains(point)) ? new Cursor(StandardCursorType.Hand) : Cursor.Default;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsEffectivelyEnabledProperty)
            InvalidateVisual();
    }

    public override void Render(DrawingContext g)
    {
        bool enabled = IsEffectivelyEnabled;
        if (_label.Length > 0)
            Draw.Text(g, _label, Draw.Font(), 13, Palette.Text, new Rect(0, 0, Box.X - 8, Bounds.Height));

        // Disabled: greyed out and inert, but still showing the level it'll use when switched back on.
        Fill(g, Box, 5, enabled ? Palette.Background : Palette.Surface);
        Outline(g, Box, 5, enabled ? Palette.Border : Palette.Separator, 1);
        foreach (var (r, canStep) in new[] { (Minus, Value > Lowest), (Plus, Value < Maximum) })
        {
            if (enabled && canStep && Hover is Point p && r.Contains(p))
                Fill(g, r.Deflate(3), 3, Palette.Hover);
        }

        Color Sign(bool canStep) => enabled && canStep ? Palette.Text : Palette.Separator;
        double signSize = Compact ? 14 : 16;
        Draw.Text(g, "−", Draw.Font(), signSize, Sign(Value > Lowest), Minus, TextAlignment.Center);
        Draw.Text(g, "+", Draw.Font(), signSize, Sign(Value < Maximum), Plus, TextAlignment.Center);
        Draw.Text(g, IsOff ? "Off" : $"{Value}%", Draw.Font(semibold: true), Compact ? 12 : 13, enabled ? Palette.Text : Palette.SubtleText,
            new Rect(Minus.Right, Box.Y, Plus.X - Minus.Right, Box.Height), TextAlignment.Center);
        DrawFocus(g, Box, 5);
    }
}

/// <summary>A warning level with its own on/off switch; the level is greyed out and locked while off.</summary>
internal sealed class SwitchedStepper : ThemedPanel
{
    public SwitchedStepper(int min, int max, int step)
    {
        Stepper = new Stepper("", min, max, step);
        Children.Add(Stepper);
        Children.Add(Switch);
        Switch.CheckedChanged += (_, _) => Stepper.IsEnabled = Switch.Checked;
        Width = 110 + 16 + 84;
        Height = 32;
    }

    public Stepper Stepper { get; }
    public ToggleSwitch Switch { get; } = new();

    public override double HeightFor(double width) => Height;

    protected override Size MeasureOverride(Size available)
    {
        foreach (var child in Children)
            child.Measure(Size.Infinity);
        return new Size(Width, Height);
    }

    protected override Size ArrangeOverride(Size size)
    {
        Stepper.Arrange(new Rect(0, (size.Height - Stepper.Height) / 2, Stepper.Width, Stepper.Height));
        Switch.Arrange(new Rect(size.Width - Switch.Width, (size.Height - Switch.Height) / 2, Switch.Width, Switch.Height));
        return size;
    }
}

/// <summary>
/// A themed dropdown: a box showing the current choice, opening the choices as a list styled like the tray
/// menu.
/// </summary>
internal sealed class DropDown : ThemedControl
{
    private IReadOnlyList<string> _items = [];
    private int _selected;
    private ListPopup? _open;

    public event EventHandler? SelectedChanged;

    public DropDown(double width)
    {
        Cursor = new Cursor(StandardCursorType.Hand);
        Focusable = true;
        Width = width;
        Height = 32;
    }

    protected override Size? FixedSize => new Size(Width, Height);

    public override bool OwnWidth => true;

    /// <summary>Replaces the choices without raising <see cref="SelectedChanged"/>.</summary>
    public void SetItems(IReadOnlyList<string> items, int selected)
    {
        _items = items;
        _selected = Math.Clamp(selected, 0, Math.Max(0, items.Count - 1));
        InvalidateVisual();
    }

    public int SelectedIndex => _selected;

    private void Open()
    {
        if (_open is { IsClosing: false })
        {
            _open.Dismiss(); // a second click on the box closes the list
            return;
        }
        _open = ListPopup.Open(this, _items, _selected, Palette, index =>
        {
            if (index == _selected)
                return;
            _selected = index;
            InvalidateVisual();
            SelectedChanged?.Invoke(this, EventArgs.Empty);
        });
        if (_open is not null)
            _open.Closed += (_, _) => _open = null;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (e.InitialPressMouseButton != MouseButton.Left)
            return;
        Focus();
        Open();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key is Key.Space or Key.Enter or Key.Down)
        {
            Open();
            e.Handled = true;
        }
    }

    public override void Render(DrawingContext g)
    {
        var box = new Rect(0.5, 0.5, Bounds.Width - 1, Bounds.Height - 1);
        Fill(g, box, 5, Hover is not null ? Palette.Hover : Palette.Background);
        Outline(g, box, 5, Palette.Border, 1);
        Draw.Text(g, _items.Count > 0 ? _items[_selected] : "", Draw.Font(), 13, Palette.Text, new Rect(12, 0, Bounds.Width - 44, Bounds.Height));
        // Segoe MDL2 Assets' chevron, as in Windows' own dropdowns.
        Draw.Text(g, "", new Typeface("Segoe MDL2 Assets"), 10, Palette.SubtleText, new Rect(Bounds.Width - 32, 0, 20, Bounds.Height),
            TextAlignment.Center);
        DrawFocus(g, box, 5);
    }
}

/// <summary>A compact text button with a hover wash, e.g. Remove, Undo, Restore.</summary>
internal sealed class ActionButton : ThemedControl
{
    private string _text;

    public event EventHandler? Click;

    public ActionButton(string text)
    {
        _text = text;
        Cursor = new Cursor(StandardCursorType.Hand);
        Focusable = true;
        Resize();
    }

    public string Text
    {
        get => _text;
        set
        {
            _text = value;
            Resize();
            InvalidateVisual();
        }
    }

    private void Resize() => (Width, Height) = (Math.Max(72, _text.Length * 8 + 24), 30);

    protected override Size? FixedSize => new Size(Width, Height);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsEffectivelyEnabledProperty)
        {
            Cursor = IsEffectivelyEnabled ? new Cursor(StandardCursorType.Hand) : Cursor.Default;
            InvalidateVisual();
        }
    }

    // Take focus when clicked, like any button, so focus doesn't return somewhere else when a list or dialog
    // this opened closes.
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (IsEffectivelyEnabled)
            Focus();
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (IsEffectivelyEnabled && e.InitialPressMouseButton == MouseButton.Left && new Rect(Bounds.Size).Contains(e.GetPosition(this)))
            Click?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (IsEffectivelyEnabled && e.Key is Key.Space or Key.Enter)
        {
            Click?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
        }
    }

    public override void Render(DrawingContext g)
    {
        bool enabled = IsEffectivelyEnabled;
        var box = new Rect(0.5, 0.5, Bounds.Width - 1, Bounds.Height - 1);
        Fill(g, box, 5, enabled && Hover is not null ? Palette.Hover : Palette.Background);
        Outline(g, box, 5, enabled ? Palette.Border : Palette.Separator, 1);
        Draw.Text(g, _text, Draw.Font(), 13, enabled ? Palette.Text : Palette.SubtleText, new Rect(Bounds.Size), TextAlignment.Center);
        DrawFocus(g, box, 5);
    }
}

/// <summary>
/// "Removed X · Undo" after removing a device, shown for a few seconds. Removing only hides, so undo just
/// unhides; the bar is the quick way back, and Restore removed is the lasting one.
/// </summary>
internal sealed class UndoBar : ThemedPanel
{
    private static readonly TimeSpan ShownFor = TimeSpan.FromSeconds(10);
    private readonly ActionButton _undo = new("Undo");
    private readonly DispatcherTimer _timer = new() { Interval = ShownFor };
    private Action? _onUndo;
    private string _text = "";

    public UndoBar()
    {
        Children.Add(_undo);
        _undo.Click += (_, _) =>
        {
            _onUndo?.Invoke();
            Dismiss();
        };
        _timer.Tick += (_, _) => Dismiss();
    }

    /// <summary>Raised when the bar appears or goes, so the page can make or close the room for it.</summary>
    public event EventHandler? ShownChanged;

    public bool Showing { get; private set; }

    public override double HeightFor(double width) => Math.Max(48, Draw.TextHeight(_text, Draw.Font(), 13, width - _undo.Width - 50) + 24);

    public void Show(string message, Action onUndo)
    {
        _text = message;
        _onUndo = onUndo;
        Showing = true;
        _timer.Stop();
        _timer.Start();
        InvalidateMeasure();
        InvalidateVisual();
        ShownChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Dismiss()
    {
        if (!Showing)
            return;
        _timer.Stop();
        _onUndo = null;
        Showing = false;
        ShownChanged?.Invoke(this, EventArgs.Empty);
    }

    protected override Size ArrangeOverride(Size size)
    {
        _undo.Arrange(new Rect(size.Width - _undo.Width - 10, (size.Height - _undo.Height) / 2, _undo.Width, _undo.Height));
        return size;
    }

    public override void Render(DrawingContext g)
    {
        var size = Bounds.Size;
        Fill(g, new Rect(size), 6, Palette.Surface);
        Fill(g, new Rect(0, 10, 3, size.Height - 20), 1.5, Palette.Accent);
        Draw.Wrapped(g, _text, Draw.Font(), 13, Palette.Text, new Rect(16, 0, size.Width - _undo.Width - 10 - 24 - 16, size.Height));
    }
}

/// <summary>
/// A themed list that drops down under a control: at most <see cref="VisibleRows"/> rows, scrolling for the
/// rest with the wheel, the arrow keys, Page Up/Down and Home/End, with a slim scroll bar to show where you
/// are. Picks with a click or Enter; Esc or clicking elsewhere closes it.
/// </summary>
internal sealed class ListPopup : PopupWindow
{
    public const int VisibleRows = 4;
    private const double RowHeight = 32, Inset = 4;

    // The list closes when it loses focus, which a click on its own button causes too. Remember which button's
    // list closed and when, so that click closes the list rather than reopening it.
    private static Control? s_lastAnchor;
    private static DateTime s_lastClosed;
    private static readonly TimeSpan ReopenGuard = TimeSpan.FromMilliseconds(300);

    private readonly Control _anchor;
    private readonly ListView _view;
    private readonly Action<int> _onPick;
    private Window? _owner;

    private ListPopup(Control anchor, IReadOnlyList<string> items, int checkedIndex, Action<int> onPick, Palette palette)
        : base(palette, Appearance.Corners.RoundSmall)
    {
        _anchor = anchor;
        _onPick = onPick;
        _view = new ListView(items, checkedIndex, palette);
        _view.Picked += Pick;
        Content = _view;
    }

    /// <summary>
    /// Opens the list under <paramref name="anchor"/>, at least as wide as it, or over it when there isn't room
    /// below on the screen; null when this click is the one that just closed the anchor's list.
    /// </summary>
    public static ListPopup? Open(Control anchor, IReadOnlyList<string> items, int checkedIndex, Palette palette, Action<int> onPick)
    {
        if (s_lastAnchor == anchor && DateTime.UtcNow - s_lastClosed < ReopenGuard)
            return null;
        var popup = new ListPopup(anchor, items, checkedIndex, onPick, palette);
        double textWidth = items.Count == 0 ? 0 : items.Max(i => Draw.Width(i, Draw.Font(), 13));
        popup.Width = Math.Max(anchor.Bounds.Width, textWidth + 56);
        popup.Height = Math.Min(VisibleRows, Math.Max(1, items.Count)) * RowHeight + Inset * 2;
        if (TopLevel.GetTopLevel(anchor) is Window owner)
        {
            popup._owner = owner;
            var below = anchor.PointToScreen(new Point(0, anchor.Bounds.Height + 2));
            var above = anchor.PointToScreen(new Point(0, -2));
            if (owner.Screens.ScreenFromPoint(below) is { } screen)
            {
                var area = screen.WorkingArea;
                int width = (int)Math.Ceiling(popup.Width * screen.Scaling), height = (int)Math.Ceiling(popup.Height * screen.Scaling);
                int y = below.Y + height <= area.Bottom || above.Y - height < area.Y ? below.Y : above.Y - height;
                int x = Math.Max(area.X, Math.Min(below.X, area.Right - width));
                popup.Position = new PixelPoint(x, Math.Max(area.Y, Math.Min(y, area.Bottom - height)));
            }
            else
            {
                popup.Position = below;
            }
            popup.Show(owner);
        }
        else
        {
            popup.Show();
        }
        popup.Activate();
        popup._view.Focus();
        return popup;
    }

    private void Pick(int index)
    {
        Dismiss();
        _onPick(index);
    }

    protected override void OnClosed(EventArgs e)
    {
        s_lastAnchor = _anchor;
        s_lastClosed = DateTime.UtcNow;
        base.OnClosed(e);
        // Hand focus back to the window the list belongs to, rather than leaving Windows to choose one.
        _owner?.Activate();
    }

    private sealed class ListView : Control
    {
        private readonly IReadOnlyList<string> _items;
        private readonly int _checked;
        private readonly Palette _palette;
        private int _top, _focus, _hover = -1, _pressed = -1;
        private double _wheel;

        public event Action<int>? Picked;

        public ListView(IReadOnlyList<string> items, int checkedIndex, Palette palette)
        {
            _items = items;
            _checked = checkedIndex;
            _palette = palette;
            _focus = Math.Clamp(checkedIndex, 0, Math.Max(0, items.Count - 1));
            Focusable = true;
            ScrollIntoView(_focus);
        }

        private int Rows => Math.Min(VisibleRows, _items.Count);
        private int MaxTop => Math.Max(0, _items.Count - VisibleRows);

        private void ScrollTo(int top)
        {
            _top = Math.Clamp(top, 0, MaxTop);
            InvalidateVisual();
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
            int row = (int)((p.Y - Inset) / RowHeight);
            return p.Y < Inset || row >= Rows ? -1 : _top + row;
        }

        protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
        {
            base.OnPointerWheelChanged(e);
            // Touchpads send fractions of a notch; gather them into whole rows.
            _wheel += e.Delta.Y;
            int rows = (int)_wheel;
            _wheel -= rows;
            if (rows != 0)
                ScrollTo(_top - rows);
            e.Handled = true;
        }

        protected override void OnPointerMoved(PointerEventArgs e)
        {
            base.OnPointerMoved(e);
            int row = RowAt(e.GetPosition(this));
            if (row != _hover)
            {
                _hover = row;
                InvalidateVisual();
            }
        }

        protected override void OnPointerExited(PointerEventArgs e)
        {
            base.OnPointerExited(e);
            _hover = -1;
            InvalidateVisual();
        }

        // A click is a press and release on the same row; dragging off the row cancels it.
        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            base.OnPointerPressed(e);
            _pressed = e.GetCurrentPoint(this).Properties.IsLeftButtonPressed ? RowAt(e.GetPosition(this)) : -1;
        }

        protected override void OnPointerReleased(PointerReleasedEventArgs e)
        {
            base.OnPointerReleased(e);
            int row = RowAt(e.GetPosition(this));
            if (e.InitialPressMouseButton == MouseButton.Left && row >= 0 && row == _pressed && row < _items.Count)
                Picked?.Invoke(row);
            _pressed = -1;
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            int last = _items.Count - 1;
            int focus = e.Key switch
            {
                Key.Down => Math.Min(last, _focus + 1),
                Key.Up => Math.Max(0, _focus - 1),
                Key.PageDown => Math.Min(last, _focus + VisibleRows),
                Key.PageUp => Math.Max(0, _focus - VisibleRows),
                Key.End => last,
                Key.Home => 0,
                _ => -1,
            };
            if (focus >= 0)
            {
                _focus = focus;
                _hover = -1;
                ScrollIntoView(focus);
                InvalidateVisual();
                e.Handled = true;
                return;
            }
            if (e.Key is Key.Enter or Key.Space && _items.Count > 0)
            {
                Picked?.Invoke(_focus);
                e.Handled = true;
                return;
            }
            base.OnKeyDown(e);
        }

        public override void Render(DrawingContext g)
        {
            double width = Bounds.Width;
            g.FillRectangle(Draw.Brush(_palette.Background), new Rect(Bounds.Size));
            bool scrolls = _items.Count > VisibleRows;
            double barSpace = scrolls ? 10 : 0;
            for (int row = 0; row < Rows; row++)
            {
                int index = _top + row;
                var r = new Rect(Inset, Inset + row * RowHeight, width - Inset * 2 - barSpace, RowHeight);
                if (index == _hover || (_hover < 0 && index == _focus))
                    Draw.RoundedRect(g, _palette.Hover, r.Deflate(new Thickness(0, 2)), 4);
                if (index == _checked)
                {
                    // A tick for the current choice, as in the tray menu.
                    double cx = r.X + 14, cy = r.Y + r.Height / 2, s = 5;
                    var pen = new Pen(Draw.Brush(_palette.Text), 1.6, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
                    g.DrawGeometry(null, pen, new PolylineGeometry([new Point(cx - s, cy), new Point(cx - s / 3, cy + s * 0.65), new Point(cx + s, cy - s * 0.7)], false));
                }
                Draw.Text(g, _items[index], Draw.Font(), 13, _palette.Text, new Rect(r.X + 30, r.Y, r.Width - 38, r.Height));
            }

            if (scrolls)
            {
                // A slim bar showing which part of the list is in view.
                double trackTop = Inset + 2, trackHeight = Bounds.Height - trackTop * 2;
                double thumbHeight = Math.Max(16, trackHeight * VisibleRows / _items.Count);
                double thumbTop = trackTop + (trackHeight - thumbHeight) * _top / MaxTop;
                Draw.RoundedRect(g, _palette.SubtleText, new Rect(width - Inset - 5, thumbTop, 4, thumbHeight), 2);
            }
        }
    }
}
