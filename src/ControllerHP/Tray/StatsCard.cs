using System.Globalization;
using System.Drawing.Drawing2D;

namespace ControllerHP.Tray;

/// <summary>
/// One controller's figures under the history chart: a header keyed to its chart line, then a grid of
/// tiles (label, value, one line of context). Figures without enough history say so instead of guessing.
/// The grid has three columns, or two or one when the card is too narrow for every tile's text.
/// </summary>
internal sealed class StatsCard : ThemedControl
{
    private const int MaxColumns = 3, HeaderHeight = 44, TileHeight = 78, Padding_ = 16;

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
    /// Its warning levels were changed: its own levels, or null to follow Settings again. Changing either
    /// stepper gives it levels of its own.
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
        Controls.Add(_rename);
        Controls.Add(_remove);
        Controls.Add(_enabled);
        Controls.Add(_low);
        Controls.Add(_critical);
        SetLevels(levels, ownLevels);
        _enabled.CheckedChanged += (_, _) =>
        {
            if (_loading)
                return;
            // Back on with both levels off would be off again straight away: start from Settings' levels.
            if (_enabled.Checked && _low.IsOff && _critical.IsOff)
                ShowLevels(UsableLevels(Settings.DefaultWarnings));
            _low.Enabled = _critical.Enabled = _enabled.Checked;
            ReportLevels();
        };
        _low.ValueChanged += (_, _) =>
        {
            // As on the Settings page, critical stays below low, and lowering low pushes it down too.
            _critical.Maximum = CriticalMaximum;
            if (!_critical.IsOff && _critical.Value > _critical.Maximum)
                _critical.Value = _critical.Maximum;
            _critical.Invalidate();
            ReportLevels();
        };
        _critical.ValueChanged += (_, _) => ReportLevels();
        _rename.Click += (_, _) => RenameRequested?.Invoke(this, EventArgs.Empty);
        _remove.Click += (_, _) => RemoveRequested?.Invoke(this, EventArgs.Empty);
        ResizeForDpi();
    }

    public string Id => _series.Id;

    /// <summary>The controller's history as last shown.</summary>
    public HistorySeries Series => _series;

    /// <summary>
    /// Shows newer figures and levels in place, rather than as a new card, so a stepper being used keeps
    /// its focus and the page doesn't jump.
    /// </summary>
    public void Update(HistorySeries series, ControllerStats stats, WarningLevels levels, bool ownLevels)
    {
        _series = series;
        _stats = stats;
        _tiles = BuildTiles();
        _grid = null;
        SetLevels(levels, ownLevels);
        PerformLayout();
        Invalidate();
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
        _low.Enabled = _critical.Enabled = on;
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
        Invalidate();
        WarningsChanged?.Invoke(this, _enabled.Checked
            ? new WarningLevels(_low.IsOff ? null : _low.Value, _critical.IsOff ? null : _critical.Value)
            : new WarningLevels(null, null));
    }

    protected override void OnPaletteChanged() =>
        _rename.BackColor = _remove.BackColor = _enabled.BackColor = _low.BackColor = _critical.BackColor = Palette.Surface;

    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);
        OnPaletteChanged();
        // The buttons stay level with the name's first line, however many lines it wraps onto.
        int top = (S(HeaderHeight) - _remove.Height) / 2 + S(4);
        _remove.Location = new Point(Width - S(Padding_) - _remove.Width, top);
        _rename.Location = new Point(_remove.Left - S(8) - _rename.Width, top);

        var area = WarningsBounds;
        using var font = MakeFont(12);
        int y = area.Y + S(WarningsStepperTop);
        int x = area.X + LabelWidth(font, "Low") + S(8);
        _low.Location = new Point(x, y);
        _critical.Location = new Point(_low.Right + S(20) + LabelWidth(font, "Critical") + S(8), y);
        // Right after the "Warnings" label, centred on its line.
        _enabled.Location = new Point(area.X + LabelWidth(font, "Warnings") + S(10), area.Y + S(6) + (S(22) - _enabled.Height) / 2);
    }

    private static int LabelWidth(Font font, string text) =>
        TextRenderer.MeasureText(text, font, Size.Empty, TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding).Width;

    /// <summary>
    /// How the card is laid out at a width: its header's height (taller while the name wraps), how many tile
    /// columns, and where the warnings go. The warnings take two tiles' room beside the last tile, or a row
    /// of their own if there isn't room; with one column they take a tile's.
    /// </summary>
    private readonly record struct Grid(int Header, int Columns, int TileWidth, int WarningsIndex, int WarningsSpan);

    private Grid? _grid;
    private int _gridWidth, _gridDpi;

    private Grid CurrentGrid
    {
        get
        {
            if (_grid is null || _gridWidth != Width || _gridDpi != DeviceDpi)
            {
                _grid = GridFor(Width);
                _gridWidth = Width;
                _gridDpi = DeviceDpi;
            }
            return _grid.Value;
        }
    }

    /// <summary>The room the name has, between the line key and the buttons.</summary>
    private int NameWidth(int width) => width - S(Padding_) * 2 - S(22) - _rename.Width - S(8) - _remove.Width - S(8);

    private int NameHeight(int width)
    {
        using var font = MakeFont(14, semibold: true);
        return TextHeight(_series.Name, font, NameWidth(width));
    }

    /// <summary>The width a tile's text needs: its label (and "?"), value and detail each on one line.</summary>
    private int TileTextWidth(Tile tile)
    {
        using var labelFont = MakeFont(12);
        using var valueFont = MakeFont(17, semibold: true);
        using var detailFont = MakeFont(11);
        return Math.Max(LabelWidth(labelFont, tile.Label) + (tile.Help ? S(22) : 0),
            Math.Max(LabelWidth(valueFont, tile.Value), LabelWidth(detailFont, tile.Detail)));
    }

    /// <summary>The width the warnings need: the label and switch, the two steppers, and where the levels come from.</summary>
    private int WarningsWidth()
    {
        using var labelFont = MakeFont(12);
        using var detailFont = MakeFont(11);
        int steppers = LabelWidth(labelFont, "Low") + S(8) + _low.Width + S(20) + LabelWidth(labelFont, "Critical") + S(8) + _critical.Width;
        int heading = LabelWidth(labelFont, "Warnings") + S(10) + _enabled.Width;
        int status = Math.Max(LabelWidth(detailFont, OwnLevelsText + ResetLink), LabelWidth(detailFont, "Inherited from Settings"));
        return Math.Max(steppers, Math.Max(heading, status));
    }

    private Grid GridFor(int width)
    {
        int header = Math.Max(S(HeaderHeight), NameHeight(width) + S(25));
        int tileText = _tiles.Count == 0 ? 0 : _tiles.Max(TileTextWidth);
        int warnings = WarningsWidth();
        int inner = width - S(Padding_) * 2;
        for (int columns = MaxColumns; columns >= 1; columns--)
        {
            int tileWidth = inner / columns;
            if (columns > 1 && tileText > tileWidth - S(12))
                continue;
            // Two tiles' room if that's enough, else the whole row.
            int span = Math.Min(2, columns);
            if (warnings > span * tileWidth - S(12))
                span = columns;
            if (columns > 1 && warnings > span * tileWidth - S(12))
                continue;
            int index = _tiles.Count % columns + span <= columns ? _tiles.Count : (_tiles.Count + columns - 1) / columns * columns;
            return new Grid(header, columns, tileWidth, index, span);
        }
        throw new InvalidOperationException("One column always fits.");
    }

    /// <summary>The warnings' row is taller than a tile's, for the switch beside its label.</summary>
    private const int WarningsExtraHeight = 14;

    // Within the warnings: the label and switch, the steppers, then where the levels come from.
    private const int WarningsStepperTop = 36, WarningsStatusTop = 70;

    private Rectangle WarningsBounds
    {
        get
        {
            var grid = CurrentGrid;
            var first = TileBounds(grid.WarningsIndex);
            return first with { Width = first.Width * grid.WarningsSpan, Height = first.Height + S(WarningsExtraHeight) };
        }
    }

    /// <summary>"Reset", after "Overridden", while it has levels of its own.</summary>
    private Rectangle ResetBounds
    {
        get
        {
            var area = WarningsBounds;
            using var font = MakeFont(11);
            int x = area.X + LabelWidth(font, OwnLevelsText);
            return new Rectangle(x, area.Y + S(WarningsStatusTop), LabelWidth(font, ResetLink) + S(2), S(18));
        }
    }

    private const string OwnLevelsText = "Overridden · ";
    private const string ResetLink = "Reset";

    protected override Size? LogicalSize => new Size(600, 400);

    public override int HeightFor(int width)
    {
        var grid = GridFor(width);
        int rows = (grid.WarningsIndex + grid.WarningsSpan + grid.Columns - 1) / grid.Columns;
        return grid.Header + rows * S(TileHeight) + S(WarningsExtraHeight) + S(Padding_);
    }

    /// <summary>One column wide: the widest tile or the warnings, or the header's buttons beside some of the name.</summary>
    public override int MinimumWidth
    {
        get
        {
            int tiles = Math.Max(_tiles.Count == 0 ? 0 : _tiles.Max(TileTextWidth), WarningsWidth()) + S(12);
            int header = S(22) + S(80) + S(8) + _rename.Width + S(8) + _remove.Width;
            return Math.Max(tiles, header) + S(Padding_) * 2;
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
        tiles.Add(new("Charge cycles", s.ChargeCycles.ToString("0.0", CultureInfo.CurrentCulture),
            "Not wear by itself"));

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

    private Rectangle TileBounds(int index)
    {
        var grid = CurrentGrid;
        int row = index / grid.Columns, column = index % grid.Columns;
        return new Rectangle(S(Padding_) + column * grid.TileWidth, grid.Header + row * S(TileHeight), grid.TileWidth, S(TileHeight));
    }

    /// <summary>The "?" beside a tile's label.</summary>
    private Rectangle HelpBounds(int index)
    {
        var tile = TileBounds(index);
        using var font = MakeFont(12);
        int labelWidth = TextRenderer.MeasureText(_tiles[index].Label, font, Size.Empty, TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding).Width;
        return new Rectangle(tile.X + labelWidth + S(6), tile.Y + S(8), S(16), S(16));
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        Fill(g, new RectangleF(0, 0, Width, Height), S(6), Palette.Surface);

        // Header: a line key in the controller's chart colour, then its name in text ink, wrapping if it must.
        using (var nameFont = MakeFont(14, semibold: true))
        {
            int header = CurrentGrid.Header, nameHeight = NameHeight(Width);
            float keyY = Math.Max(0, (header - nameHeight) / 2) + Math.Min(nameHeight, nameFont.Height) / 2f;
            using (var key = new Pen(HistoryChart.ColorFor(_series.Slot, Palette.Light), S(2)) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                g.DrawLine(key, S(Padding_), keyY, S(Padding_) + S(14), keyY);
            DrawWrapped(g, _series.Name, nameFont, new Rectangle(S(Padding_) + S(22), 0, NameWidth(Width), header), Palette.Text);
        }

        using var labelFont = MakeFont(12);
        using var valueFont = MakeFont(17, semibold: true);
        using var detailFont = MakeFont(11);
        for (int i = 0; i < _tiles.Count; i++)
        {
            var tile = _tiles[i];
            var r = TileBounds(i);
            int textWidth = r.Width - S(12);
            TextRenderer.DrawText(g, tile.Label, labelFont, new Rectangle(r.X, r.Y + S(6), textWidth, S(20)), Palette.SubtleText, TextFlags);
            TextRenderer.DrawText(g, tile.Value, valueFont, new Rectangle(r.X, r.Y + S(26), textWidth, S(26)),
                tile.Muted ? Palette.SubtleText : Palette.Text, TextFlags);
            TextRenderer.DrawText(g, tile.Detail, detailFont, new Rectangle(r.X, r.Y + S(52), textWidth, S(18)), Palette.SubtleText, TextFlags);
            if (tile.Help)
                DrawHelpIcon(g, HelpBounds(i));
        }

        // Warnings: a label like the tiles', "Low" and "Critical" beside their steppers, then where the levels come from.
        var area = WarningsBounds;
        TextRenderer.DrawText(g, "Warnings", labelFont, new Rectangle(area.X, area.Y + S(6), area.Width, S(22)), Palette.SubtleText, TextFlags);
        foreach (var (label, stepper) in new[] { ("Low", _low), ("Critical", _critical) })
        {
            int width = LabelWidth(labelFont, label);
            TextRenderer.DrawText(g, label, labelFont, new Rectangle(stepper.Left - S(8) - width, stepper.Top, width + S(2), stepper.Height),
                stepper.Enabled ? Palette.Text : Palette.SubtleText, TextFlags);
        }
        if (_ownLevels)
        {
            TextRenderer.DrawText(g, OwnLevelsText, detailFont, new Rectangle(area.X, area.Y + S(WarningsStatusTop), area.Width, S(18)), Palette.SubtleText, TextFlags);
            using var linkFont = MakeFont(11);
            using var link = new Font(linkFont, _resetHover ? FontStyle.Underline : FontStyle.Regular);
            TextRenderer.DrawText(g, ResetLink, link, ResetBounds, Palette.Accent, TextFlags);
        }
        else
        {
            TextRenderer.DrawText(g, "Inherited from Settings", detailFont, new Rectangle(area.X, area.Y + S(WarningsStatusTop), area.Width, S(18)), Palette.SubtleText, TextFlags);
        }

        if (_helpHover)
            DrawDisclaimer(g, HelpBounds(_tiles.FindIndex(t => t.Help)));
    }

    private void DrawHelpIcon(Graphics g, Rectangle r)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using (var pen = new Pen(_helpHover ? Palette.Text : Palette.SubtleText, S(1.2f)))
            g.DrawEllipse(pen, r.X + S(1), r.Y + S(1), r.Width - S(2), r.Height - S(2));
        using var font = MakeFont(11, semibold: true);
        TextRenderer.DrawText(g, "?", font, r, _helpHover ? Palette.Text : Palette.SubtleText,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
    }

    /// <summary>The health disclaimer, shown while hovering the "?"; drawn below it, or above if it won't fit.</summary>
    private void DrawDisclaimer(Graphics g, Rectangle anchor)
    {
        using var font = MakeFont(12);
        int width = Math.Min(S(340), Width - S(16));
        var flags = TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding;
        int textHeight = TextRenderer.MeasureText(g, HealthDisclaimer, font, new Size(width - S(24), 0), flags).Height;
        int height = textHeight + S(20);
        int x = Math.Clamp(anchor.X - S(8), S(8), Width - width - S(8));
        int y = anchor.Bottom + S(6) + height <= Height ? anchor.Bottom + S(6) : anchor.Top - S(6) - height;
        var box = new Rectangle(x, Math.Max(0, y), width, height);
        Fill(g, box, S(5), Palette.Background);
        Outline(g, box, S(5), Palette.Border, 1);
        TextRenderer.DrawText(g, HealthDisclaimer, font, Rectangle.Inflate(box, -S(12), -S(10)), Palette.Text, flags);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        int help = _tiles.FindIndex(t => t.Help);
        bool hover = help >= 0 && Rectangle.Inflate(HelpBounds(help), S(4), S(4)).Contains(e.Location);
        bool link = _ownLevels && ResetBounds.Contains(e.Location);
        if (hover != _helpHover || link != _resetHover)
        {
            _helpHover = hover;
            _resetHover = link;
            Cursor = hover ? Cursors.Help : link ? Cursors.Hand : Cursors.Default;
            // The disclaimer can reach down over the steppers, which would draw on top of it.
            _low.Visible = _critical.Visible = _enabled.Visible = !hover;
            Invalidate();
        }
    }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);
        if (e.Button == MouseButtons.Left && _ownLevels && ResetBounds.Contains(e.Location))
        {
            _ownLevels = false;
            _resetHover = false;
            Cursor = Cursors.Default;
            Invalidate();
            WarningsChanged?.Invoke(this, null);
        }
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _helpHover = _resetHover = false;
        _low.Visible = _critical.Visible = _enabled.Visible = true;
        Invalidate();
    }
}
