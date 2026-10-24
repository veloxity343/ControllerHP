using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;

namespace WattsLeft.Ui;

/// <summary>An item in <see cref="TrayMenu"/>: a command, a tickable setting, a submenu, a line of text, or a separator.</summary>
internal sealed record MenuEntry(string Text, Action? Click = null, bool Enabled = true, bool? Checked = null,
    IReadOnlyList<MenuEntry>? Submenu = null)
{
    public static readonly MenuEntry Separator = new("");

    public bool IsSeparator => ReferenceEquals(this, Separator);

    /// <summary>Whether it can be highlighted and chosen.</summary>
    public bool Selectable => !IsSeparator && Enabled;
}

/// <summary>
/// The tray icon's right-click menu, drawn in the taskbar's light or dark colours with Windows 11's rounded
/// corners. A submenu opens beside its item as a menu of its own.
/// </summary>
internal sealed class TrayMenu : PopupWindow
{
    private readonly Palette _palette;
    private readonly MenuPanel _panel;
    private readonly TrayMenu? _parent;
    private TrayMenu? _child;
    private int _childIndex = -1;

    public TrayMenu(IReadOnlyList<MenuEntry> entries, Palette palette, TrayMenu? parent = null)
        : base(palette, Appearance.Corners.RoundSmall)
    {
        _palette = palette;
        _parent = parent;
        _panel = new MenuPanel(entries, palette);
        _panel.Hovered += OnHovered;
        _panel.Chosen += Choose;
        Content = _panel;
        Width = _panel.MenuWidth;
        Height = _panel.MenuHeight;
    }

    // With its submenu open, a menu hands focus to it; it closes with the rest on a click outside.
    protected override bool CloseOnDeactivate => _child is null || _child.IsClosing;

    /// <summary>Opens at <paramref name="point"/> (physical pixels): beside the cursor, above it if it won't fit below.</summary>
    public void ShowAt(PixelPoint point)
    {
        var screen = Screens.ScreenFromPoint(point) ?? Screens.Primary;
        double scale = screen?.Scaling ?? 1;
        int width = (int)Math.Round(Width * scale), height = (int)Math.Round(Height * scale);
        var area = screen?.Bounds ?? new PixelRect(point, new PixelSize(width, height));
        int x = point.X + width > area.Right ? point.X - width : point.X;
        int y = point.Y + height > (screen?.WorkingArea.Bottom ?? area.Bottom) ? point.Y - height : point.Y;
        Position = new PixelPoint(Math.Max(area.X, x), Math.Max(area.Y, y));
        Show();
        Activate();
    }

    /// <summary>Opens beside an item of its parent, at <paramref name="itemTop"/> (physical pixels), on whichever side fits.</summary>
    private void ShowBeside(PixelRect parentBounds, int itemTop)
    {
        var screen = Screens.ScreenFromPoint(parentBounds.TopRight) ?? Screens.Primary;
        double scale = screen?.Scaling ?? 1;
        int width = (int)Math.Round(Width * scale), height = (int)Math.Round(Height * scale);
        var area = screen?.WorkingArea ?? parentBounds;
        int x = parentBounds.Right + width <= area.Right ? parentBounds.Right : parentBounds.X - width;
        int y = Math.Min(itemTop - (int)Math.Round(MenuPanel.Padding * scale), area.Bottom - height);
        Position = new PixelPoint(x, Math.Max(area.Y, y));
        Show();
    }

    private PixelRect ScreenBounds()
    {
        double scale = RenderScaling;
        return new PixelRect(Position, new PixelSize((int)Math.Round(Width * scale), (int)Math.Round(Height * scale)));
    }

    private void OnHovered(int index)
    {
        if (index == _childIndex)
            return;
        CloseChild();
        if (index < 0 || _panel.Entries[index] is not { Submenu: { } submenu, Enabled: true })
            return;
        _childIndex = index;
        _child = new TrayMenu(submenu, _palette, this);
        _child.Closed += (sender, _) =>
        {
            if (ReferenceEquals(sender, _child))
            {
                _child = null;
                _childIndex = -1;
            }
        };
        _child.ShowBeside(ScreenBounds(), Position.Y + (int)Math.Round(_panel.ItemTop(index) * RenderScaling));
    }

    private void CloseChild()
    {
        _child?.Dismiss();
        _child = null;
        _childIndex = -1;
    }

    private void Choose(int index)
    {
        var entry = _panel.Entries[index];
        if (entry.Submenu is not null)
        {
            OnHovered(index);
            _child?.Activate();
            _child?._panel.Highlight(0);
            return;
        }
        Root.DismissAll();
        if (entry.Click is { } click)
            Dispatcher.UIThread.Post(click);
    }

    private TrayMenu Root => _parent?.Root ?? this;

    private void DismissAll()
    {
        _child?.DismissAll();
        Dismiss();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Down:
                _panel.MoveHighlight(1);
                break;
            case Key.Up:
                _panel.MoveHighlight(-1);
                break;
            case Key.Enter or Key.Space when _panel.HighlightedIndex >= 0:
                Choose(_panel.HighlightedIndex);
                break;
            case Key.Right when _panel.HighlightedIndex >= 0 && _panel.Entries[_panel.HighlightedIndex].Submenu is not null:
                Choose(_panel.HighlightedIndex);
                break;
            case Key.Left when _parent is not null:
                Dismiss();
                _parent.Activate();
                break;
            default:
                base.OnKeyDown(e);
                return;
        }
        e.Handled = true;
    }
}

/// <summary>A menu's items, drawn like Windows' own menus: a tick column, the text, and an arrow for submenus.</summary>
internal sealed class MenuPanel : Control
{
    public const double Padding = 3;
    private const double ItemHeight = 22, SeparatorHeight = 7, TextLeft = 36, TextRight = 40, MinWidth_ = 140, FontSize = 12;

    private readonly Palette _palette;
    public IReadOnlyList<MenuEntry> Entries { get; }
    public int HighlightedIndex { get; private set; } = -1;

    /// <summary>The pointer moved onto an item (its index), or off them all (-1).</summary>
    public event Action<int>? Hovered;

    /// <summary>An item was clicked, or Enter was pressed on it.</summary>
    public event Action<int>? Chosen;

    public MenuPanel(IReadOnlyList<MenuEntry> entries, Palette palette)
    {
        Entries = entries;
        _palette = palette;
        Focusable = true;
    }

    public double MenuWidth => Math.Max(MinWidth_,
        TextLeft + Entries.Where(e => !e.IsSeparator).Select(e => Draw.Width(e.Text, Draw.Font(), FontSize)).DefaultIfEmpty(0).Max() + TextRight);

    public double MenuHeight => Padding * 2 + Entries.Sum(e => e.IsSeparator ? SeparatorHeight : ItemHeight);

    public double ItemTop(int index) => Padding + Entries.Take(index).Sum(e => e.IsSeparator ? SeparatorHeight : ItemHeight);

    private int IndexAt(Point point)
    {
        double y = Padding;
        for (int i = 0; i < Entries.Count; i++)
        {
            double height = Entries[i].IsSeparator ? SeparatorHeight : ItemHeight;
            if (point.Y >= y && point.Y < y + height)
                return Entries[i].Selectable ? i : -1;
            y += height;
        }
        return -1;
    }

    public void Highlight(int index)
    {
        if (index >= 0 && !Entries[index].Selectable)
            index = Entries.ToList().FindIndex(index, e => e.Selectable);
        HighlightedIndex = index;
        InvalidateVisual();
    }

    public void MoveHighlight(int step)
    {
        int count = Entries.Count, index = HighlightedIndex;
        for (int i = 0; i < count; i++)
        {
            index = ((index < 0 && step < 0 ? 0 : index) + step + count) % count;
            if (Entries[index].Selectable)
            {
                HighlightedIndex = index;
                InvalidateVisual();
                Hovered?.Invoke(index);
                return;
            }
        }
    }

    public override void Render(DrawingContext g)
    {
        double width = Bounds.Width, y = Padding;
        g.FillRectangle(Draw.Brush(_palette.Background), new Rect(Bounds.Size));
        for (int i = 0; i < Entries.Count; i++)
        {
            var entry = Entries[i];
            if (entry.IsSeparator)
            {
                double line = Math.Round(y + SeparatorHeight / 2) + 0.5;
                g.DrawLine(new Pen(Draw.Brush(_palette.Separator), 1), new Point(8, line), new Point(width - 8, line));
                y += SeparatorHeight;
                continue;
            }
            if (i == HighlightedIndex && entry.Enabled)
                Draw.RoundedRect(g, _palette.Hover, new Rect(4, y + 1, width - 8, ItemHeight - 2), 4);
            var text = entry.Enabled ? _palette.Text : _palette.SubtleText;
            Draw.Text(g, entry.Text, Draw.Font(), FontSize, text, new Rect(TextLeft, y, width - TextLeft - TextRight + 16, ItemHeight));
            if (entry.Checked == true)
                DrawTick(g, new Rect(10, y + 3, 16, 16), text);
            if (entry.Submenu is not null)
                DrawArrow(g, new Point(width - 14, y + ItemHeight / 2), text);
            y += ItemHeight;
        }
    }

    /// <summary>A tick in the text colour, as the current menu draws in place of a boxed check.</summary>
    private static void DrawTick(DrawingContext g, Rect r, Color color)
    {
        double cx = r.X + r.Width / 2, cy = r.Y + r.Height / 2, s = Math.Min(r.Width, r.Height) / 2.6;
        var pen = new Pen(Draw.Brush(color), Math.Max(1.5, s / 3.5), lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
        g.DrawGeometry(null, pen, new PolylineGeometry([new Point(cx - s, cy), new Point(cx - s / 3, cy + s * 0.65), new Point(cx + s, cy - s * 0.7)], false));
    }

    /// <summary>The small right-pointing triangle that marks a submenu.</summary>
    private static void DrawArrow(DrawingContext g, Point centre, Color color) =>
        g.DrawGeometry(Draw.Brush(color), null, new PolylineGeometry(
            [new Point(centre.X - 2, centre.Y - 4), new Point(centre.X + 2, centre.Y), new Point(centre.X - 2, centre.Y + 4)], true));

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        int index = IndexAt(e.GetPosition(this));
        if (index != HighlightedIndex)
        {
            HighlightedIndex = index;
            InvalidateVisual();
            Hovered?.Invoke(index);
        }
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        // A submenu's item stays highlighted while the pointer is in the submenu.
        if (HighlightedIndex >= 0 && Entries[HighlightedIndex].Submenu is null)
        {
            HighlightedIndex = -1;
            InvalidateVisual();
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        int index = IndexAt(e.GetPosition(this));
        if (index >= 0)
            Chosen?.Invoke(index);
    }
}
