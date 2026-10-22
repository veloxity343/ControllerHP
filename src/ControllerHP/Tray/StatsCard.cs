using System.Globalization;
using System.Drawing.Drawing2D;

namespace ControllerHP.Tray;

/// <summary>
/// One controller's figures under the history chart: a header keyed to its chart line, then a grid of
/// tiles (label, value, one line of context). Figures without enough history say so instead of guessing.
/// </summary>
internal sealed class StatsCard : ThemedControl
{
    private const int Columns = 3, HeaderHeight = 44, TileHeight = 78, Padding_ = 16;

    private const string HealthDisclaimer =
        "A rough guide, not a diagnosis. Health compares how long a charge lasts on battery now with this " +
        "controller's own first week of tracking, so it needs about three weeks of history. Time spent " +
        "charging, including playing on the cable, is left out, and so are charge cycles. Vibration, the " +
        "light bar, audio and how you play all change runtime, so expect it to move around. Typical ranges " +
        "are commonly reported figures, not official ones.";

    private readonly HistorySeries _series;
    private readonly ControllerStats _stats;
    private readonly List<Tile> _tiles;
    private readonly ActionButton _rename = new("Rename");
    private readonly ActionButton _remove = new("Remove");
    private bool _helpHover;

    /// <summary>The Rename button was clicked.</summary>
    public event EventHandler? RenameRequested;

    /// <summary>The Remove button was clicked.</summary>
    public event EventHandler? RemoveRequested;

    private sealed record Tile(string Label, string Value, string Detail, bool Help = false, bool Muted = false);

    public StatsCard(HistorySeries series, ControllerStats stats)
    {
        _series = series;
        _stats = stats;
        _tiles = BuildTiles();
        Controls.Add(_rename);
        Controls.Add(_remove);
        _rename.Click += (_, _) => RenameRequested?.Invoke(this, EventArgs.Empty);
        _remove.Click += (_, _) => RemoveRequested?.Invoke(this, EventArgs.Empty);
        ResizeForDpi();
    }

    protected override void OnPaletteChanged() => _rename.BackColor = _remove.BackColor = Palette.Surface;

    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);
        _rename.BackColor = _remove.BackColor = Palette.Surface;
        int top = (S(HeaderHeight) - _remove.Height) / 2 + S(4);
        _remove.Location = new Point(Width - S(Padding_) - _remove.Width, top);
        _rename.Location = new Point(_remove.Left - S(8) - _rename.Width, top);
    }

    protected override Size? LogicalSize =>
        new Size(600, HeaderHeight + (int)Math.Ceiling(_tiles.Count / (double)Columns) * TileHeight + Padding_);

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
        int width = (Width - S(Padding_) * 2) / Columns;
        int row = index / Columns, column = index % Columns;
        return new Rectangle(S(Padding_) + column * width, S(HeaderHeight) + row * S(TileHeight), width, S(TileHeight));
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

        // Header: a line key in the controller's chart colour, then its name in text ink.
        using (var key = new Pen(HistoryChart.ColorFor(_series.Slot, Palette.Light), S(2)) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            g.DrawLine(key, S(Padding_), S(HeaderHeight) / 2f, S(Padding_) + S(14), S(HeaderHeight) / 2f);
        using (var nameFont = MakeFont(14, semibold: true))
            TextRenderer.DrawText(g, _series.Name, nameFont,
                new Rectangle(S(Padding_) + S(22), 0, _rename.Left - S(Padding_) - S(30), S(HeaderHeight)), Palette.Text, TextFlags);

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
        if (hover != _helpHover)
        {
            _helpHover = hover;
            Cursor = hover ? Cursors.Help : Cursors.Default;
            Invalidate();
        }
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _helpHover = false;
        Invalidate();
    }
}
