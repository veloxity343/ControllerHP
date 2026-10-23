using System.Drawing.Drawing2D;
using System.Globalization;

namespace ControllerHP.Tray;

/// <summary>
/// Each controller's battery over time: one 2px line per controller on a single 0-100% axis, joining
/// readings with straight lines (a battery drains continuously; readings sample it), held flat from the
/// last reading to now, with gaps while disconnected. A legend
/// sits above; connected controllers get an end-dot and their current value at the right. Hovering
/// shows a crosshair snapped to the nearest reading and every controller's value at that moment.
/// </summary>
internal sealed class HistoryChart : ThemedControl
{
    // Categorical slots in fixed order, chosen to stay distinguishable with colour blindness and checked
    // against the settings window's card surfaces: white in light mode, #323232 in dark. Light aqua and
    // yellow sit below 3:1 contrast on white, so identity never rests on colour alone: the legend and
    // end labels name every line.
    private static readonly Color[] LightSlots =
        [.. new[] { "#2a78d6", "#eb6834", "#1baf7a", "#eda100", "#e87ba4", "#008300", "#6250d6", "#e34948" }.Select(ColorTranslator.FromHtml)];
    private static readonly Color[] DarkSlots =
        [.. new[] { "#3987e5", "#d95926", "#199e70", "#c98500", "#d55181", "#008300", "#9085e9", "#e66767" }.Select(ColorTranslator.FromHtml)];

    public const string NoHistoryMessage =
        "No battery history yet. Readings are recorded from now on, whenever a device's battery changes.";

    /// <summary>Shown in place of the chart when there's nothing to draw.</summary>
    public string EmptyMessage { get; set; } = NoHistoryMessage;

    private IReadOnlyList<HistorySeries> _series = [];
    /// <summary>The series drawn: all but those left out, which only appear, dimmed, in the legend.</summary>
    private IReadOnlyList<HistorySeries> _drawn = [];
    private IReadOnlySet<string> _leftOut = new HashSet<string>();
    /// <summary>Each legend entry's area, for clicking it to leave that controller out or bring it back.</summary>
    private readonly List<(Rectangle Bounds, string Id)> _legendHits = [];

    /// <summary>A legend entry was clicked: the controller's id, to leave out of the chart or bring back.</summary>
    public event EventHandler<string>? LegendClicked;

    public const string AllLeftOutMessage = "Every device is left out of the chart. Click a name above to show it again.";
    /// <summary>Every reading time in range plus now, sorted, so the crosshair can snap without re-sorting per move.</summary>
    private DateTime[] _snapTimes = [];
    private DateTime _start, _end;
    private DateTime? _hoverTime;

    public HistoryChart()
    {
        ResizeForDpi();
    }

    protected override Size? LogicalSize => new Size(600, 380);

    /// <summary>Taller by a row for each extra row the legend wraps onto, so the plot keeps its height.</summary>
    public override int HeightFor(int width) => S(380) + (LegendLayout(width).Rows - 1) * S(LegendRow);

    public override int MinimumWidth => S(320);

    public void SetData(IReadOnlyList<HistorySeries> series, DateTime start, DateTime end, IReadOnlySet<string>? leftOut = null)
    {
        // Eight slots and no more: a ninth controller would need a generated colour nobody could tell apart.
        _series = series.Where(s => s.Slot < LightSlots.Length).ToList();
        _leftOut = leftOut ?? new HashSet<string>();
        _drawn = _series.Where(s => !_leftOut.Contains(s.Id)).ToList();
        _start = start;
        _end = end;
        _snapTimes = [.. _drawn.SelectMany(s => s.Points).Select(p => p.Time).Append(end)
            .Where(t => t >= start && t <= end).Distinct().Order()];
        _hoverTime = null;
        _legend = null;
        Invalidate();
    }

    private Color SeriesColor(HistorySeries s) => ColorFor(s.Slot, Palette.Light);

    /// <summary>A controller's line colour, for keying other views (the stats cards) to the chart.</summary>
    public static Color ColorFor(int slot, bool light) => (light ? LightSlots : DarkSlots)[slot % LightSlots.Length];

    private const int LegendRow = 28;

    /// <summary>
    /// Where each legend entry goes at this width: left to right, wrapping onto another row when the next one
    /// won't fit, and each name cut short only if it's wider than a whole row.
    /// </summary>
    private (List<(HistorySeries Series, int X, int Y, int TextWidth)> Entries, int Rows) LegendLayout(int width)
    {
        using var font = MakeFont(12);
        var entries = new List<(HistorySeries, int, int, int)>();
        int left = S(16), right = width - S(16), x = left, y = S(16), rows = 1;
        foreach (var s in _series)
        {
            // Measured without EndEllipsis, which would measure the name already cut down to fit.
            int textWidth = TextRenderer.MeasureText(s.Name, font, Size.Empty, TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding).Width;
            textWidth = Math.Min(textWidth, right - left - S(22));
            if (x > left && x + S(20) + textWidth > right)
            {
                x = left;
                y += S(LegendRow);
                rows++;
            }
            entries.Add((s, x, y, textWidth));
            x += S(20) + textWidth + S(20);
        }
        return (entries, rows);
    }

    // Layout, in device pixels.
    private int LegendHeight => Legend.Rows * S(LegendRow);

    /// <summary>The legend at the current width, kept until the data, size or DPI changes: the plot's bounds,
    /// read for every point drawn, depend on it.</summary>
    private (List<(HistorySeries Series, int X, int Y, int TextWidth)> Entries, int Rows) Legend
    {
        get
        {
            if (_legend is null || _legendWidth != Width || _legendDpi != DeviceDpi)
            {
                _legend = LegendLayout(Width);
                _legendWidth = Width;
                _legendDpi = DeviceDpi;
            }
            return _legend.Value;
        }
    }

    private (List<(HistorySeries Series, int X, int Y, int TextWidth)> Entries, int Rows)? _legend;
    private int _legendWidth, _legendDpi;
    private Rectangle Plot => Rectangle.FromLTRB(S(52), S(16) + LegendHeight + S(12), Width - S(64), Height - S(36));

    private float X(DateTime t) => Plot.Left + (float)((t - _start).TotalSeconds / (_end - _start).TotalSeconds) * Plot.Width;
    private float Y(int percent) => Plot.Bottom - percent / 100f * Plot.Height;
    private DateTime TimeAt(float x) => _start + TimeSpan.FromSeconds((x - Plot.Left) / Plot.Width * (_end - _start).TotalSeconds);

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        Fill(g, new RectangleF(0, 0, Width, Height), S(6), Palette.Surface);
        if (_series.Count == 0)
        {
            using var font = MakeFont(13);
            TextRenderer.DrawText(g, EmptyMessage, font, Rectangle.Inflate(ClientRectangle, -S(24), -S(24)), Palette.SubtleText,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
            return;
        }

        DrawLegend(g);
        DrawAxes(g);
        if (_drawn.Count == 0)
        {
            using var font = MakeFont(13);
            TextRenderer.DrawText(g, AllLeftOutMessage, font, Rectangle.Inflate(Plot, -S(24), -S(24)), Palette.SubtleText,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
            return;
        }
        g.SmoothingMode = SmoothingMode.AntiAlias;
        foreach (var s in _drawn)
            DrawSeries(g, s);
        DrawEndLabels(g);
        if (_hoverTime is DateTime t)
            DrawCrosshair(g, t);
    }

    private void DrawLegend(Graphics g)
    {
        using var font = MakeFont(12);
        _legendHits.Clear();
        foreach (var (s, x, y, textWidth) in Legend.Entries)
        {
            // A short line key mirrors the line mark; the name stays in text ink. Left out of the chart, the
            // key is a hollow outline and the name fades, but it stays here to click back in.
            bool leftOut = _leftOut.Contains(s.Id);
            float keyY = y + S(LegendRow) / 2f;
            // Halfway from the legend's grey to the background: plainly dimmed, still readable.
            var faded = Color.FromArgb((Palette.SubtleText.R + Palette.Surface.R) / 2, (Palette.SubtleText.G + Palette.Surface.G) / 2,
                (Palette.SubtleText.B + Palette.Surface.B) / 2);
            if (leftOut)
            {
                using var pen = new Pen(faded, S(1));
                g.DrawRectangle(pen, x, keyY - S(2), S(14), S(4));
            }
            else
            {
                using var pen = new Pen(SeriesColor(s), S(2)) { StartCap = LineCap.Round, EndCap = LineCap.Round };
                g.DrawLine(pen, x, keyY, x + S(14), keyY);
            }
            TextRenderer.DrawText(g, s.Name, font, new Rectangle(x + S(20), y, textWidth + S(2), S(LegendRow)),
                leftOut ? faded : Palette.SubtleText, TextFlags);
            _legendHits.Add((new Rectangle(x - S(4), y, S(20) + textWidth + S(8), S(LegendRow)), s.Id));
        }
    }

    private void DrawAxes(Graphics g)
    {
        var plot = Plot;
        using var font = MakeFont(11);
        using var grid = new Pen(Palette.Separator, 1);
        using var baseline = new Pen(Palette.Border, 1);
        foreach (int percent in new[] { 0, 25, 50, 75, 100 })
        {
            int y = (int)Math.Round(Y(percent));
            g.DrawLine(percent == 0 ? baseline : grid, plot.Left, y, plot.Right, y);
            TextRenderer.DrawText(g, $"{percent}%", font, new Rectangle(0, y - S(10), plot.Left - S(8), S(20)), Palette.SubtleText,
                TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }

        var (step, format) = TickSpacing();
        // First tick on a round boundary at or after the start: the hour for hourly steps, midnight for days.
        var tick = step >= TimeSpan.FromDays(1)
            ? _start.Date.AddDays(1)
            : _start.Date.AddHours(Math.Ceiling((_start - _start.Date).TotalHours / step.TotalHours) * step.TotalHours);
        float lastRight = float.MinValue;
        for (; tick <= _end; tick += step)
        {
            float x = X(tick);
            g.DrawLine(baseline, x, plot.Bottom, x, plot.Bottom + S(4));
            string label = tick.ToString(format, CultureInfo.CurrentCulture);
            int width = TextRenderer.MeasureText(g, label, font).Width;
            // A label hanging past the plot's ends would collide with the 0% label or the end values; one
            // running into the label before it (a narrow window) is skipped, leaving its tick.
            if (x - width / 2f < plot.Left - S(4) || x + width / 2f > plot.Right + S(4) || x - width / 2f < lastRight + S(8))
                continue;
            lastRight = x + width / 2f;
            TextRenderer.DrawText(g, label, font, new Rectangle((int)(x - width / 2f), plot.Bottom + S(6), width, S(18)), Palette.SubtleText,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPadding);
        }
    }

    private (TimeSpan Step, string Format) TickSpacing()
    {
        var span = _end - _start;
        string time = CultureInfo.CurrentCulture.DateTimeFormat.ShortTimePattern;
        return span <= TimeSpan.FromHours(2.5) ? (TimeSpan.FromMinutes(30), time)
            : span <= TimeSpan.FromHours(6.5) ? (TimeSpan.FromHours(1), time)
            : span <= TimeSpan.FromHours(25) ? (TimeSpan.FromHours(4), time)
            : (TimeSpan.FromDays(1), "ddd d");
    }

    /// <summary>Joins each run of readings, and holds the last one flat to now if still connected.</summary>
    private void DrawSeries(Graphics g, HistorySeries s)
    {
        using var pen = new Pen(SeriesColor(s), S(2)) { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round };
        using var brush = new SolidBrush(SeriesColor(s));
        var run = new List<PointF>();
        void Flush()
        {
            // A run too short to show as a line (a brief connection) still gets a mark, so no reading is invisible.
            if (run.Count > 0 && run.Max(p => p.X) - run.Min(p => p.X) < S(3))
            {
                float r = S(2.5f);
                g.FillEllipse(brush, run[0].X - r, run[0].Y - r, r * 2, r * 2);
            }
            else if (run.Count >= 2)
                g.DrawLines(pen, run.ToArray());
            run.Clear();
        }

        var points = s.Points;
        for (int i = 0; i < points.Count; i++)
        {
            var p = points[i];
            if (p.Percent is not int percent)
            {
                Flush();
                continue;
            }
            run.Add(new PointF(X(p.Time), Y(percent)));
            // The latest reading stands until now; a reading followed by a disconnection ends there.
            if (i + 1 == points.Count)
                run.Add(new PointF(X(_end), Y(percent)));
        }
        Flush();

        if (s.Connected && points[^1].Percent is int last)
        {
            // End-dot with a ring in the surface colour, so it stays clear of other lines.
            float cx = X(_end), cy = Y(last), r = S(4), ring = S(2);
            using (var surface = new SolidBrush(Palette.Surface))
                g.FillEllipse(surface, cx - r - ring, cy - r - ring, (r + ring) * 2, (r + ring) * 2);
            using var dot = new SolidBrush(SeriesColor(s));
            g.FillEllipse(dot, cx - r, cy - r, r * 2, r * 2);
        }
    }

    /// <summary>
    /// The current value beside each connected line's end. Labels that would collide are left to the
    /// legend and tooltip rather than nudged away from their lines.
    /// </summary>
    private void DrawEndLabels(Graphics g)
    {
        using var font = MakeFont(12, semibold: true);
        var placed = new List<float>();
        foreach (var s in _drawn.Where(s => s.Connected))
        {
            int percent = s.Points[^1].Percent!.Value;
            float y = Y(percent);
            if (placed.Any(other => Math.Abs(other - y) < S(16)))
                continue;
            placed.Add(y);
            TextRenderer.DrawText(g, $"{percent}%", font, new Rectangle((int)X(_end) + S(10), (int)y - S(10), S(50), S(20)), Palette.Text,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }
    }

    private static HistoryPoint? ValueAt(HistorySeries s, DateTime t)
    {
        HistoryPoint? current = null;
        foreach (var p in s.Points)
        {
            if (p.Time > t)
                break;
            current = p;
        }
        return current;
    }

    private void DrawCrosshair(Graphics g, DateTime t)
    {
        var plot = Plot;
        float x = X(t);
        using (var line = new Pen(Palette.SubtleText, 1))
            g.DrawLine(line, x, plot.Top, x, plot.Bottom);

        // One tooltip for every controller at this moment: value first and strong, name after.
        var rows = _drawn
            .Select(s => (s, point: ValueAt(s, t)))
            .Where(r => r.point is not null)
            .Select(r => (r.s, text: r.point!.Value.Percent is int p ? $"{p}%{(r.point.Value.Charging ? " charging" : "")}" : "—"))
            .ToList();
        if (rows.Count == 0)
            return;

        using var valueFont = MakeFont(12, semibold: true);
        using var nameFont = MakeFont(12);
        string header = t.ToString(_end - _start > TimeSpan.FromHours(25) ? "ddd d, t" : "t", CultureInfo.CurrentCulture);
        int rowHeight = S(20), pad = S(10);
        int valueWidth = rows.Max(r => TextRenderer.MeasureText(g, r.text, valueFont).Width);
        int nameWidth = rows.Max(r => TextRenderer.MeasureText(g, r.s.Name, nameFont).Width);
        int width = pad + S(16) + valueWidth + S(8) + nameWidth + pad;
        int height = pad + rowHeight * (rows.Count + 1) + pad - S(4);
        int left = x + S(12) + width <= Width - S(4) ? (int)x + S(12) : (int)x - S(12) - width;
        var box = new Rectangle(left, plot.Top, width, height);
        Fill(g, box, S(5), Palette.Background);
        Outline(g, box, S(5), Palette.Border, 1);

        TextRenderer.DrawText(g, header, nameFont, new Rectangle(box.X + pad, box.Y + pad - S(2), width - pad * 2, rowHeight),
            Palette.SubtleText, TextFlags);
        int y = box.Y + pad - S(2) + rowHeight;
        foreach (var (s, text) in rows)
        {
            using (var key = new Pen(SeriesColor(s), S(2)) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                g.DrawLine(key, box.X + pad, y + rowHeight / 2f, box.X + pad + S(10), y + rowHeight / 2f);
            TextRenderer.DrawText(g, text, valueFont, new Rectangle(box.X + pad + S(16), y, valueWidth, rowHeight), Palette.Text, TextFlags);
            TextRenderer.DrawText(g, s.Name, nameFont,
                new Rectangle(box.X + pad + S(16) + valueWidth + S(8), y, nameWidth + S(2), rowHeight), Palette.SubtleText, TextFlags);
            y += rowHeight;
        }
    }

    /// <summary>The crosshair snaps to the nearest reading, so readers aim at a time, not at a 2px line.</summary>
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        DateTime? snapped = null;
        if (_snapTimes.Length > 0 && Plot.Contains(e.Location))
        {
            var target = TimeAt(e.X);
            int i = Array.BinarySearch(_snapTimes, target);
            if (i >= 0)
                snapped = _snapTimes[i];
            else
            {
                // ~i is the first time after the target; the nearest is it or the one before.
                int after = ~i;
                snapped = after == 0 ? _snapTimes[0]
                    : after == _snapTimes.Length ? _snapTimes[^1]
                    : target - _snapTimes[after - 1] <= _snapTimes[after] - target ? _snapTimes[after - 1] : _snapTimes[after];
            }
        }
        if (snapped != _hoverTime)
        {
            _hoverTime = snapped;
            Invalidate();
        }
        Cursor = _legendHits.Any(h => h.Bounds.Contains(e.Location)) ? Cursors.Hand : Cursors.Default;
    }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);
        if (e.Button == MouseButtons.Left && _legendHits.FirstOrDefault(h => h.Bounds.Contains(e.Location)).Id is string id)
            LegendClicked?.Invoke(this, id);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _hoverTime = null;
        Invalidate();
    }
}
