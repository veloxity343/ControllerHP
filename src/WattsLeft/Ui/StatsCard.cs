using System.Globalization;
using Avalonia;
using Avalonia.Input;
using Avalonia.Media;

namespace WattsLeft.Ui;

/// <summary>
/// One device's figures under the history chart: a header keyed to its chart line, then a grid of tiles
/// (label, value, one line of context). Figures without enough history say so instead of guessing. The grid
/// has three columns, or two or one when the card is too narrow for every tile's text.
/// </summary>
internal sealed class StatsCard : ThemedPanel
{
    private const int MaxColumns = 3;
    private const double HeaderHeight = 44, TileHeight = 78, Padding_ = 16;

    private const string HealthDisclaimer =
        "A rough guide, not a diagnosis. Health compares how long a charge lasts on battery now with this " +
        "device's own first week of tracking, so it needs about three weeks of history. Time spent " +
        "charging, including playing on the cable, is left out, and so are charge cycles. Vibration, the " +
        "light bar, audio and how you play all change runtime, so expect it to move around. Typical ranges " +
        "are commonly reported figures, not official ones.";

    private HistorySeries _series;
    private ControllerStats _stats;
    private List<Tile> _tiles;
    private readonly ActionButton _rename = new("Rename");
    private readonly ActionButton _remove = new("Remove");
    private bool _helpHover;

    // Its battery warnings, in the space beside the last tile: low and critical, each able to go off.
    private readonly Stepper _low = new("", Settings.MinWarning + Settings.WarningStep, Settings.MaxWarning, Settings.WarningStep) { CanTurnOff = true, Compact = true };
    private readonly Stepper _critical = new("", Settings.MinWarning, Settings.MaxWarning - Settings.WarningStep, Settings.WarningStep) { CanTurnOff = true, Compact = true };
    /// <summary>Both warnings at once: off overrides Settings with no warnings for it.</summary>
    private readonly ToggleSwitch _enabled = new() { Compact = true };
    /// <summary>Whether it has warning levels of its own, rather than following Settings.</summary>
    private bool _ownLevels;
    /// <summary>Set while the steppers are filled in, so they don't report it as a change.</summary>
    private bool _loading;
    private bool _resetHover;

    /// <summary>
    /// Its warning levels were changed: its own levels, or null to follow Settings again. Changing either stepper
    /// gives it levels of its own.
    /// </summary>
    public event EventHandler<WarningLevels?>? WarningsChanged;

    /// <summary>The Rename button was clicked.</summary>
    public event EventHandler? RenameRequested;

    /// <summary>The Remove button was clicked.</summary>
    public event EventHandler? RemoveRequested;

    private sealed record Tile(string Label, string Value, string Detail, bool Help = false, bool Muted = false);

    public StatsCard(HistorySeries series, ControllerStats stats, WarningLevels levels, bool ownLevels)
    {
        _series = series;
        _stats = stats;
        _tiles = BuildTiles();
        foreach (var child in new Avalonia.Controls.Control[] { _rename, _remove, _enabled, _low, _critical })
            Children.Add(child);
        SetLevels(levels, ownLevels);
        _enabled.CheckedChanged += (_, _) =>
        {
            if (_loading)
                return;
            // Back on with both levels off would be off again straight away: start from Settings' levels.
            if (_enabled.Checked && _low.IsOff && _critical.IsOff)
                ShowLevels(UsableLevels(Settings.DefaultWarnings));
            _low.IsEnabled = _critical.IsEnabled = _enabled.Checked;
            ReportLevels();
        };
        _low.ValueChanged += (_, _) =>
        {
            // As on the Settings page, critical stays below low, and lowering low pushes it down too.
            _critical.Maximum = CriticalMaximum;
            if (!_critical.IsOff && _critical.Value > _critical.Maximum)
                _critical.Value = _critical.Maximum;
            _critical.InvalidateVisual();
            ReportLevels();
        };
        _critical.ValueChanged += (_, _) => ReportLevels();
        _rename.Click += (_, _) => RenameRequested?.Invoke(this, EventArgs.Empty);
        _remove.Click += (_, _) => RemoveRequested?.Invoke(this, EventArgs.Empty);
    }

    public string Id => _series.Id;

    /// <summary>The device's history as last shown.</summary>
    public HistorySeries Series => _series;

    /// <summary>
    /// Shows newer figures and levels in place, rather than as a new card, so a stepper being used keeps its
    /// focus and the page doesn't jump.
    /// </summary>
    public void Update(HistorySeries series, ControllerStats stats, WarningLevels levels, bool ownLevels)
    {
        _series = series;
        _stats = stats;
        _tiles = BuildTiles();
        _grid = null;
        SetLevels(levels, ownLevels);
        InvalidateMeasure();
        InvalidateArrange();
        InvalidateVisual();
    }

    private int CriticalMaximum => _low.IsOff ? Settings.MaxWarning - Settings.WarningStep : _low.Value - Settings.WarningStep;

    private void SetLevels(WarningLevels levels, bool ownLevels)
    {
        _ownLevels = ownLevels;
        bool on = levels.Low is not null || levels.Critical is not null;
        // Switched off, the steppers keep showing levels, greyed, ready for switching back on: the ones they
        // had, or else Settings'.
        if (on)
            ShowLevels(levels);
        else if (_low.IsOff && _critical.IsOff)
            ShowLevels(UsableLevels(Settings.DefaultWarnings));
        _loading = true;
        _enabled.Checked = on;
        _loading = false;
        _low.IsEnabled = _critical.IsEnabled = on;
    }

    private void ShowLevels(WarningLevels levels)
    {
        _loading = true;
        _low.Value = levels.Low ?? _low.OffValue;
        _critical.Maximum = CriticalMaximum;
        _critical.Value = levels.Critical ?? _critical.OffValue;
        _loading = false;
    }

    /// <summary>Levels with at least one warning on: these, or the usual 20% and 10% if both are off.</summary>
    private static WarningLevels UsableLevels(WarningLevels levels) =>
        levels.Low is null && levels.Critical is null ? new(20, 10) : levels;

    private void ReportLevels()
    {
        if (_loading)
            return;
        _ownLevels = true;
        InvalidateVisual();
        WarningsChanged?.Invoke(this, _enabled.Checked
            ? new WarningLevels(_low.IsOff ? null : _low.Value, _critical.IsOff ? null : _critical.Value)
            : new WarningLevels(null, null));
    }

    protected override Size ArrangeOverride(Size size)
    {
        // The buttons stay level with the name's first line, however many lines it wraps onto.
        double top = (HeaderHeight - _remove.Height) / 2 + 4;
        Place(_remove, size.Width - Padding_ - _remove.Width, top);
        Place(_rename, size.Width - Padding_ - _remove.Width - 8 - _rename.Width, top);

        var area = WarningsBounds(size.Width);
        double y = area.Y + WarningsStepperTop;
        double x = area.X + LabelWidth("Low", 12) + 8;
        Place(_low, x, y);
        Place(_critical, x + _low.Width + 20 + LabelWidth("Critical", 12) + 8, y);
        // Right after the "Warnings" label, centred on its line.
        Place(_enabled, area.X + LabelWidth("Warnings", 12) + 10, area.Y + 6 + (22 - _enabled.Height) / 2);
        return size;
    }

    private static void Place(Avalonia.Controls.Control control, double x, double y) =>
        control.Arrange(new Rect(x, y, control.Width, control.Height));

    private static double LabelWidth(string text, double size, bool semibold = false) => Draw.Width(text, Draw.Font(semibold), size);

    /// <summary>
    /// How the card is laid out at a width: its header's height (taller while the name wraps), how many tile
    /// columns, and where the warnings go. The warnings take two tiles' room beside the last tile, or a row of
    /// their own if there isn't room; with one column they take a tile's.
    /// </summary>
    private readonly record struct Grid(double Header, int Columns, double TileWidth, int WarningsIndex, int WarningsSpan);

    private Grid? _grid;
    private double _gridWidth;

    private Grid GridAt(double width)
    {
        if (_grid is null || _gridWidth != width)
        {
            _grid = GridFor(width);
            _gridWidth = width;
        }
        return _grid.Value;
    }

    /// <summary>The room the name has, between the line key and the buttons.</summary>
    private double NameWidth(double width) => width - Padding_ * 2 - 22 - _rename.Width - 8 - _remove.Width - 8;

    private double NameHeight(double width) => Draw.TextHeight(_series.Name, Draw.Font(semibold: true), 14, NameWidth(width));

    /// <summary>The width a tile's text needs: its label (and "?"), value and detail each on one line.</summary>
    private static double TileTextWidth(Tile tile) =>
        Math.Max(LabelWidth(tile.Label, 12) + (tile.Help ? 22 : 0), Math.Max(LabelWidth(tile.Value, 17, semibold: true), LabelWidth(tile.Detail, 11)));

    /// <summary>The width the warnings need: the label and switch, the two steppers, and where the levels come from.</summary>
    private double WarningsWidth()
    {
        double steppers = LabelWidth("Low", 12) + 8 + _low.Width + 20 + LabelWidth("Critical", 12) + 8 + _critical.Width;
        double heading = LabelWidth("Warnings", 12) + 10 + _enabled.Width;
        double status = Math.Max(LabelWidth(OwnLevelsText + ResetLink, 11), LabelWidth("Inherited from Settings", 11));
        return Math.Max(steppers, Math.Max(heading, status));
    }

    private Grid GridFor(double width)
    {
        double header = Math.Max(HeaderHeight, NameHeight(width) + 25);
        double tileText = _tiles.Count == 0 ? 0 : _tiles.Max(TileTextWidth);
        double warnings = WarningsWidth();
        double inner = width - Padding_ * 2;
        for (int columns = MaxColumns; columns >= 1; columns--)
        {
            double tileWidth = Math.Floor(inner / columns);
            if (columns > 1 && tileText > tileWidth - 12)
                continue;
            // Two tiles' room if that's enough, else the whole row.
            int span = Math.Min(2, columns);
            if (warnings > span * tileWidth - 12)
                span = columns;
            if (columns > 1 && warnings > span * tileWidth - 12)
                continue;
            int index = _tiles.Count % columns + span <= columns ? _tiles.Count : (_tiles.Count + columns - 1) / columns * columns;
            return new Grid(header, columns, tileWidth, index, span);
        }
        throw new InvalidOperationException("One column always fits.");
    }

    /// <summary>The warnings' row is taller than a tile's, for the switch beside its label.</summary>
    private const double WarningsExtraHeight = 14;

    // Within the warnings: the label and switch, the steppers, then where the levels come from.
    private const double WarningsStepperTop = 36, WarningsStatusTop = 70;

    private Rect WarningsBounds(double width)
    {
        var grid = GridAt(width);
        var first = TileBounds(grid.WarningsIndex, width);
        return first.WithWidth(first.Width * grid.WarningsSpan).WithHeight(first.Height + WarningsExtraHeight);
    }

    /// <summary>"Reset", after "Overridden", while it has levels of its own.</summary>
    private Rect ResetBounds
    {
        get
        {
            var area = WarningsBounds(Bounds.Width);
            return new Rect(area.X + LabelWidth(OwnLevelsText, 11), area.Y + WarningsStatusTop, LabelWidth(ResetLink, 11) + 2, 18);
        }
    }

    private const string OwnLevelsText = "Overridden · ";
    private const string ResetLink = "Reset";

    public override double HeightFor(double width)
    {
        var grid = GridAt(width);
        int rows = (grid.WarningsIndex + grid.WarningsSpan + grid.Columns - 1) / grid.Columns;
        return grid.Header + rows * TileHeight + WarningsExtraHeight + Padding_;
    }

    /// <summary>One column wide: the widest tile or the warnings, or the header's buttons beside some of the name.</summary>
    public override double MinimumWidth
    {
        get
        {
            double tiles = Math.Max(_tiles.Count == 0 ? 0 : _tiles.Max(TileTextWidth), WarningsWidth()) + 12;
            double header = 22 + 80 + 8 + _rename.Width + 8 + _remove.Width;
            return Math.Max(tiles, header) + Padding_ * 2;
        }
    }

    private List<Tile> BuildTiles()
    {
        var s = _stats;
        var tiles = new List<Tile>();
        const string needsData = "Needs more data";

        // Predictions: time to full while charging, otherwise time to the countdown level.
        if (!_series.Connected)
            tiles.Add(new("Status", "Disconnected", "Predictions resume when it's back"));
        else if (s.Charging)
            tiles.Add(s.UntilFull is TimeSpan full
                ? new("Until full", Duration(full), "From recent charges")
                : new("Until full", needsData, "Shows after a charge is tracked", Muted: true));
        else
        {
            string label = s.CountdownLevel > 0 ? $"Until {s.CountdownLevel}%" : "Until empty";
            tiles.Add(s.UntilLow is TimeSpan low
                ? new(label, Duration(low), "At recent drain")
                : new(label, needsData, "Use it on battery for an hour or so", Muted: true));
        }

        // Health: against its own first week once there's a baseline, otherwise runtime vs the model's typical.
        string typical = s.TypicalHours is (int lo, int hi) ? $"Per charge, vs {lo}–{hi} h" : "Per charge";
        if (s.HealthPercent is int health && s.RuntimePerCharge is TimeSpan now && s.BaselineRuntime is TimeSpan then)
            tiles.Add(new("Battery health", $"{health}%", $"{Hours(now)} per charge, was {Hours(then)}", Help: true));
        else if (s.RuntimePerCharge is TimeSpan runtime)
            tiles.Add(new("Battery health", $"≈ {Hours(runtime)}", typical, Help: true));
        else
            tiles.Add(new("Battery health", needsData, "Use it on battery for an hour or so", Help: true, Muted: true));

        tiles.Add(s.DrainPerHour is double drain
            ? new("Average drain", $"{drain:0.#}% / h", "While on battery")
            : new("Average drain", needsData, "Use it on battery for an hour or so", Muted: true));

        tiles.Add(new("Play time", Duration(s.PlayTime, zero: "None"), $"Today {Duration(s.PlayTimeToday, zero: "none")}"));
        tiles.Add(new("Charge cycles", s.ChargeCycles.ToString("0.0", CultureInfo.CurrentCulture), "Not wear by itself"));

        if (s.Charging)
            tiles.Add(new("Last charged", "Charging now", s.LastChargedTo is int level ? $"At {level}%" : ""));
        else if (s.LastCharged is DateTime when)
            tiles.Add(new("Last charged", When(when), s.LastChargedTo is int to ? $"To {to}%" : ""));
        else
            tiles.Add(new("Last charged", "Not seen yet", "Since tracking began", Muted: true));

        tiles.Add(new($"Drops to {s.LowLevel}%", s.LowCount.ToString(CultureInfo.CurrentCulture),
            s.LowCount == 1 ? "Time in this range" : "Times in this range"));
        return tiles;
    }

    private static string Duration(TimeSpan span, string zero = "0 min") => BatteryStats.FormatDuration(span, zero);

    /// <summary>A runtime in hours, e.g. "9 h" or "8.5 h".</summary>
    private static string Hours(TimeSpan span) => $"{Math.Round(span.TotalHours * 2) / 2:0.#} h";

    private static string When(DateTime time)
    {
        string clock = time.ToString("t", CultureInfo.CurrentCulture);
        var day = time.Date;
        if (day == DateTime.Today)
            return $"Today, {clock}";
        if (day == DateTime.Today.AddDays(-1))
            return $"Yesterday, {clock}";
        return $"{time.ToString("ddd d MMM", CultureInfo.CurrentCulture)}, {clock}";
    }

    private Rect TileBounds(int index, double width)
    {
        var grid = GridAt(width);
        int row = index / grid.Columns, column = index % grid.Columns;
        return new Rect(Padding_ + column * grid.TileWidth, grid.Header + row * TileHeight, grid.TileWidth, TileHeight);
    }

    /// <summary>The "?" beside a tile's label.</summary>
    private Rect HelpBounds(int index)
    {
        var tile = TileBounds(index, Bounds.Width);
        return new Rect(tile.X + LabelWidth(_tiles[index].Label, 12) + 6, tile.Y + 8, 16, 16);
    }

    public override void Render(DrawingContext g)
    {
        var size = Bounds.Size;
        Fill(g, new Rect(size), 6, Palette.Surface);

        // Header: a line key in the device's chart colour, then its name in text ink, wrapping if it must.
        var nameFont = Draw.Font(semibold: true);
        double header = GridAt(size.Width).Header, nameHeight = NameHeight(size.Width);
        double lineHeight = Draw.TextHeight("A", nameFont, 14, 1000);
        double keyY = Math.Max(0, (header - nameHeight) / 2) + Math.Min(nameHeight, lineHeight) / 2;
        g.DrawLine(new Pen(Draw.Brush(HistoryChart.ColorFor(_series.Slot, Palette.Light)), 2, lineCap: PenLineCap.Round),
            new Point(Padding_, keyY), new Point(Padding_ + 14, keyY));
        Draw.Wrapped(g, _series.Name, nameFont, 14, Palette.Text, new Rect(Padding_ + 22, 0, NameWidth(size.Width), header));

        for (int i = 0; i < _tiles.Count; i++)
        {
            var tile = _tiles[i];
            var r = TileBounds(i, size.Width);
            double textWidth = r.Width - 12;
            Draw.Text(g, tile.Label, Draw.Font(), 12, Palette.SubtleText, new Rect(r.X, r.Y + 6, textWidth, 20));
            Draw.Text(g, tile.Value, Draw.Font(semibold: true), 17, tile.Muted ? Palette.SubtleText : Palette.Text, new Rect(r.X, r.Y + 26, textWidth, 26));
            Draw.Text(g, tile.Detail, Draw.Font(), 11, Palette.SubtleText, new Rect(r.X, r.Y + 52, textWidth, 18));
            if (tile.Help)
                DrawHelpIcon(g, HelpBounds(i));
        }

        // Warnings: a label like the tiles', "Low" and "Critical" beside their steppers, then where the levels come from.
        var area = WarningsBounds(size.Width);
        Draw.Text(g, "Warnings", Draw.Font(), 12, Palette.SubtleText, new Rect(area.X, area.Y + 6, area.Width, 22));
        foreach (var (label, stepper) in new[] { ("Low", _low), ("Critical", _critical) })
        {
            double width = LabelWidth(label, 12);
            var bounds = stepper.Bounds;
            Draw.Text(g, label, Draw.Font(), 12, stepper.IsEnabled ? Palette.Text : Palette.SubtleText,
                new Rect(bounds.X - 8 - width, bounds.Y, width + 2, bounds.Height));
        }
        if (_ownLevels)
        {
            Draw.Text(g, OwnLevelsText, Draw.Font(), 11, Palette.SubtleText, new Rect(area.X, area.Y + WarningsStatusTop, area.Width, 18));
            var link = Draw.Format(ResetLink, Draw.Font(), 11, Palette.Accent);
            if (_resetHover)
                link.SetTextDecorations(TextDecorations.Underline);
            var reset = ResetBounds;
            g.DrawText(link, new Point(reset.X, reset.Y + (reset.Height - link.Height) / 2));
        }
        else
        {
            Draw.Text(g, "Inherited from Settings", Draw.Font(), 11, Palette.SubtleText, new Rect(area.X, area.Y + WarningsStatusTop, area.Width, 18));
        }

        if (_helpHover)
            DrawDisclaimer(g, HelpBounds(_tiles.FindIndex(t => t.Help)));
    }

    private void DrawHelpIcon(DrawingContext g, Rect r)
    {
        var color = _helpHover ? Palette.Text : Palette.SubtleText;
        g.DrawEllipse(null, new Pen(Draw.Brush(color), 1.2), r.Center, r.Width / 2 - 1, r.Height / 2 - 1);
        Draw.Text(g, "?", Draw.Font(semibold: true), 11, color, r, TextAlignment.Center);
    }

    /// <summary>The health disclaimer, shown while hovering the "?"; drawn below it, or above if it won't fit.</summary>
    private void DrawDisclaimer(DrawingContext g, Rect anchor)
    {
        double width = Math.Min(340, Bounds.Width - 16);
        double textHeight = Draw.TextHeight(HealthDisclaimer, Draw.Font(), 12, width - 24);
        double height = textHeight + 20;
        double x = Math.Clamp(anchor.X - 8, 8, Bounds.Width - width - 8);
        double y = anchor.Bottom + 6 + height <= Bounds.Height ? anchor.Bottom + 6 : anchor.Top - 6 - height;
        var box = new Rect(x, Math.Max(0, y), width, height);
        Fill(g, box, 5, Palette.Background);
        g.DrawRectangle(null, new Pen(Draw.Brush(Palette.Border), 1), new RoundedRect(box.Deflate(0.5), 5));
        var text = Draw.Format(HealthDisclaimer, Draw.Font(), 12, Palette.Text);
        text.MaxTextWidth = width - 24;
        g.DrawText(text, new Point(box.X + 12, box.Y + 10));
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var position = e.GetPosition(this);
        int help = _tiles.FindIndex(t => t.Help);
        bool hover = help >= 0 && HelpBounds(help).Inflate(4).Contains(position);
        bool link = _ownLevels && ResetBounds.Contains(position);
        if (hover != _helpHover || link != _resetHover)
        {
            _helpHover = hover;
            _resetHover = link;
            Cursor = hover ? new Cursor(StandardCursorType.Help) : link ? new Cursor(StandardCursorType.Hand) : Cursor.Default;
            // The disclaimer can reach down over the steppers, which would draw on top of it.
            _low.IsVisible = _critical.IsVisible = _enabled.IsVisible = !hover;
            InvalidateVisual();
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (e.InitialPressMouseButton == MouseButton.Left && _ownLevels && ResetBounds.Contains(e.GetPosition(this)))
        {
            _ownLevels = false;
            _resetHover = false;
            Cursor = Cursor.Default;
            InvalidateVisual();
            WarningsChanged?.Invoke(this, null);
        }
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _helpHover = _resetHover = false;
        _low.IsVisible = _critical.IsVisible = _enabled.IsVisible = true;
        InvalidateVisual();
    }
}
