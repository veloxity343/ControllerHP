using System.Globalization;
using Avalonia;
using Avalonia.Input;
using Avalonia.Media;

namespace ControllerHP.Ui;

/// <summary>
/// Each device's battery over time: one 2px line per device on a single 0-100% axis, joining readings with
/// straight lines (a battery drains continuously; readings sample it), held flat from the last reading to
/// now, with gaps while disconnected. A legend sits above; connected devices get an end-dot and their
/// current value at the right. Hovering shows a crosshair snapped to the nearest reading and every device's
/// value at that moment.
/// </summary>
internal sealed class HistoryChart : ThemedControl
{
    // Categorical slots in fixed order, chosen to stay distinguishable with colour blindness and checked
    // against the settings window's card surfaces: white in light mode, #323232 in dark. Light aqua and
    // yellow sit below 3:1 contrast on white, so identity never rests on colour alone: the legend and
    // end labels name every line.
    private static readonly Color[] LightSlots =
        [.. new[] { "#2a78d6", "#eb6834", "#1baf7a", "#eda100", "#e87ba4", "#008300", "#6250d6", "#e34948" }.Select(Color.Parse)];
    private static readonly Color[] DarkSlots =
        [.. new[] { "#3987e5", "#d95926", "#199e70", "#c98500", "#d55181", "#008300", "#9085e9", "#e66767" }.Select(Color.Parse)];

    public const string NoHistoryMessage =
        "No battery history yet. Readings are recorded from now on, whenever a device's battery changes.";

    public const string AllLeftOutMessage = "Every device is left out of the chart. Click a name above to show it again.";

    /// <summary>Shown in place of the chart when there's nothing to draw.</summary>
    public string EmptyMessage { get; set; } = NoHistoryMessage;

    private IReadOnlyList<HistorySeries> _series = [];
    /// <summary>The series drawn: all but those left out, which only appear, dimmed, in the legend.</summary>
    private IReadOnlyList<HistorySeries> _drawn = [];
    private IReadOnlySet<string> _leftOut = new HashSet<string>();
    /// <summary>Each legend entry's area, for clicking it to leave that device out or bring it back.</summary>
    private readonly List<(Rect Bounds, string Id)> _legendHits = [];
    /// <summary>Every reading time in range plus now, sorted, so the crosshair can snap without re-sorting per move.</summary>
    private DateTime[] _snapTimes = [];
    private DateTime _start, _end;
    private DateTime? _hoverTime;

    /// <summary>A legend entry was clicked: the device's id, to leave out of the chart or bring back.</summary>
    public event EventHandler<string>? LegendClicked;

    private const double LegendRow = 28;

    /// <summary>Taller by a row for each extra row the legend wraps onto, so the plot keeps its height.</summary>
    public override double HeightFor(double width) => 380 + (LegendLayout(width).Rows - 1) * LegendRow;

    public override double MinimumWidth => 320;

    public void SetData(IReadOnlyList<HistorySeries> series, DateTime start, DateTime end, IReadOnlySet<string>? leftOut = null)
    {
        // Eight slots and no more: a ninth device would need a generated colour nobody could tell apart.
        _series = series.Where(s => s.Slot < LightSlots.Length).ToList();
        _leftOut = leftOut ?? new HashSet<string>();
        _drawn = _series.Where(s => !_leftOut.Contains(s.Id)).ToList();
        _start = start;
        _end = end;
        _snapTimes = [.. _drawn.SelectMany(s => s.Points).Select(p => p.Time).Append(end)
            .Where(t => t >= start && t <= end).Distinct().Order()];
        _hoverTime = null;
        _legend = null;
        InvalidateMeasure();
        InvalidateVisual();
    }

    private Color SeriesColor(HistorySeries s) => ColorFor(s.Slot, Palette.Light);

    /// <summary>A device's line colour, for keying other views (the stats cards) to the chart.</summary>
    public static Color ColorFor(int slot, bool light) => (light ? LightSlots : DarkSlots)[slot % LightSlots.Length];

    /// <summary>
    /// Where each legend entry goes at this width: left to right, wrapping onto another row when the next one
    /// won't fit, and each name cut short only if it's wider than a whole row.
    /// </summary>
    private (List<(HistorySeries Series, double X, double Y, double TextWidth)> Entries, int Rows) LegendLayout(double width)
    {
        var entries = new List<(HistorySeries, double, double, double)>();
        double left = 16, right = width - 16, x = left, y = 16;
        int rows = 1;
        foreach (var s in _series)
        {
            double textWidth = Math.Min(Draw.Width(s.Name, Draw.Font(), 12), right - left - 22);
            if (x > left && x + 20 + textWidth > right)
            {
                x = left;
                y += LegendRow;
                rows++;
            }
            entries.Add((s, x, y, textWidth));
            x += 20 + textWidth + 20;
        }
        return (entries, rows);
    }

    /// <summary>The legend at the current width, kept until the data or size changes: the plot's bounds, read
    /// for every point drawn, depend on it.</summary>
    private (List<(HistorySeries Series, double X, double Y, double TextWidth)> Entries, int Rows) Legend
    {
        get
        {
            if (_legend is null || _legendWidth != Bounds.Width)
            {
                _legend = LegendLayout(Bounds.Width);
                _legendWidth = Bounds.Width;
            }
            return _legend.Value;
        }
    }

    private (List<(HistorySeries Series, double X, double Y, double TextWidth)> Entries, int Rows)? _legend;
    private double _legendWidth;

    // Layout, in logical pixels.
    private double LegendHeight => Legend.Rows * LegendRow;
    private Rect Plot => new(52, 16 + LegendHeight + 12, Math.Max(1, Bounds.Width - 64 - 52), Math.Max(1, Bounds.Height - 36 - (16 + LegendHeight + 12)));

    private double X(DateTime t) => Plot.Left + (t - _start).TotalSeconds / (_end - _start).TotalSeconds * Plot.Width;
    private double Y(int percent) => Plot.Bottom - percent / 100.0 * Plot.Height;
    private DateTime TimeAt(double x) => _start + TimeSpan.FromSeconds((x - Plot.Left) / Plot.Width * (_end - _start).TotalSeconds);

    public override void Render(DrawingContext g)
    {
        Fill(g, new Rect(Bounds.Size), 6, Palette.Surface);
        if (_series.Count == 0)
        {
            Centred(g, EmptyMessage, new Rect(Bounds.Size).Deflate(24));
            return;
        }

        DrawLegend(g);
        DrawAxes(g);
        if (_drawn.Count == 0)
        {
            Centred(g, AllLeftOutMessage, Plot.Deflate(24));
            return;
        }
        foreach (var s in _drawn)
            DrawSeries(g, s);
        DrawEndLabels(g);
        if (_hoverTime is DateTime t)
            DrawCrosshair(g, t);
    }

    /// <summary>A message centred in <paramref name="area"/>, wrapped.</summary>
    private void Centred(DrawingContext g, string text, Rect area)
    {
        var formatted = Draw.Format(text, Draw.Font(), 13, Palette.SubtleText);
        formatted.MaxTextWidth = Math.Max(1, area.Width);
        formatted.TextAlignment = TextAlignment.Center;
        g.DrawText(formatted, new Point(area.X, area.Y + Math.Max(0, (area.Height - formatted.Height) / 2)));
    }

    private void DrawLegend(DrawingContext g)
    {
        _legendHits.Clear();
        foreach (var (s, x, y, textWidth) in Legend.Entries)
        {
            // A short line key mirrors the line mark; the name stays in text ink. Left out of the chart, the key
            // is a hollow outline and the name fades, but it stays here to click back in.
            bool leftOut = _leftOut.Contains(s.Id);
            double keyY = y + LegendRow / 2;
            // Halfway from the legend's grey to the background: plainly dimmed, still readable.
            var faded = Color.FromRgb((byte)((Palette.SubtleText.R + Palette.Surface.R) / 2), (byte)((Palette.SubtleText.G + Palette.Surface.G) / 2),
                (byte)((Palette.SubtleText.B + Palette.Surface.B) / 2));
            if (leftOut)
                g.DrawRectangle(null, new Pen(Draw.Brush(faded), 1), new Rect(x + 0.5, keyY - 2 + 0.5, 14, 4));
            else
                g.DrawLine(new Pen(Draw.Brush(SeriesColor(s)), 2, lineCap: PenLineCap.Round), new Point(x, keyY), new Point(x + 14, keyY));
            Draw.Text(g, s.Name, Draw.Font(), 12, leftOut ? faded : Palette.SubtleText, new Rect(x + 20, y, textWidth + 2, LegendRow));
            _legendHits.Add((new Rect(x - 4, y, 20 + textWidth + 8, LegendRow), s.Id));
        }
    }

    private void DrawAxes(DrawingContext g)
    {
        var plot = Plot;
        var grid = new Pen(Draw.Brush(Palette.Separator), 1);
        var baseline = new Pen(Draw.Brush(Palette.Border), 1);
        foreach (int percent in new[] { 0, 25, 50, 75, 100 })
        {
            // Half-pixel offsets keep 1px lines crisp.
            double y = Math.Round(Y(percent)) + 0.5;
            g.DrawLine(percent == 0 ? baseline : grid, new Point(plot.Left, y), new Point(plot.Right, y));
            Draw.Text(g, $"{percent}%", Draw.Font(), 11, Palette.SubtleText, new Rect(0, y - 10, plot.Left - 8, 20), TextAlignment.Right);
        }

        var (step, format) = TickSpacing();
        // First tick on a round boundary at or after the start: the hour for hourly steps, midnight for days.
        var tick = step >= TimeSpan.FromDays(1)
            ? _start.Date.AddDays(1)
            : _start.Date.AddHours(Math.Ceiling((_start - _start.Date).TotalHours / step.TotalHours) * step.TotalHours);
        double lastRight = double.MinValue;
        for (; tick <= _end; tick += step)
        {
            double x = Math.Round(X(tick)) + 0.5;
            g.DrawLine(baseline, new Point(x, plot.Bottom), new Point(x, plot.Bottom + 4));
            string label = tick.ToString(format, CultureInfo.CurrentCulture);
            double width = Draw.Width(label, Draw.Font(), 11);
            // A label hanging past the plot's ends would collide with the 0% label or the end values; one
            // running into the label before it (a narrow window) is skipped, leaving its tick.
            if (x - width / 2 < plot.Left - 4 || x + width / 2 > plot.Right + 4 || x - width / 2 < lastRight + 8)
                continue;
            lastRight = x + width / 2;
            Draw.Text(g, label, Draw.Font(), 11, Palette.SubtleText, new Rect(x - width / 2 - 1, plot.Bottom + 6, width + 2, 18), TextAlignment.Center);
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
    private void DrawSeries(DrawingContext g, HistorySeries s)
    {
        var color = Draw.Brush(SeriesColor(s));
        var pen = new Pen(color, 2, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
        var run = new List<Point>();
        void Flush()
        {
            // A run too short to show as a line (a brief connection) still gets a mark, so no reading is invisible.
            if (run.Count > 0 && run.Max(p => p.X) - run.Min(p => p.X) < 3)
                g.DrawEllipse(color, null, run[0], 2.5, 2.5);
            else if (run.Count >= 2)
                g.DrawGeometry(null, pen, new PolylineGeometry(run, false));
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
            run.Add(new Point(X(p.Time), Y(percent)));
            // The latest reading stands until now; a reading followed by a disconnection ends there.
            if (i + 1 == points.Count)
                run.Add(new Point(X(_end), Y(percent)));
        }
        Flush();

        if (s.Connected && points[^1].Percent is int last)
        {
            // End-dot with a ring in the surface colour, so it stays clear of other lines.
            var centre = new Point(X(_end), Y(last));
            g.DrawEllipse(Draw.Brush(Palette.Surface), null, centre, 6, 6);
            g.DrawEllipse(color, null, centre, 4, 4);
        }
    }

    /// <summary>
    /// The current value beside each connected line's end. Labels that would collide are left to the legend
    /// and tooltip rather than nudged away from their lines.
    /// </summary>
    private void DrawEndLabels(DrawingContext g)
    {
        var placed = new List<double>();
        foreach (var s in _drawn.Where(s => s.Connected))
        {
            int percent = s.Points[^1].Percent!.Value;
            double y = Y(percent);
            if (placed.Any(other => Math.Abs(other - y) < 16))
                continue;
            placed.Add(y);
            Draw.Text(g, $"{percent}%", Draw.Font(semibold: true), 12, Palette.Text, new Rect(X(_end) + 10, y - 10, 50, 20));
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

    private void DrawCrosshair(DrawingContext g, DateTime t)
    {
        var plot = Plot;
        double x = Math.Round(X(t)) + 0.5;
        g.DrawLine(new Pen(Draw.Brush(Palette.SubtleText), 1), new Point(x, plot.Top), new Point(x, plot.Bottom));

        // One tooltip for every device at this moment: value first and strong, name after.
        var rows = _drawn
            .Select(s => (s, point: ValueAt(s, t)))
            .Where(r => r.point is not null)
            .Select(r => (r.s, text: r.point!.Value.Percent is int p ? $"{p}%{(r.point.Value.Charging ? " charging" : "")}" : "—"))
            .ToList();
        if (rows.Count == 0)
            return;

        var valueFont = Draw.Font(semibold: true);
        string header = t.ToString(_end - _start > TimeSpan.FromHours(25) ? "ddd d, t" : "t", CultureInfo.CurrentCulture);
        const double rowHeight = 20, pad = 10;
        double valueWidth = rows.Max(r => Draw.Width(r.text, valueFont, 12));
        double nameWidth = rows.Max(r => Draw.Width(r.s.Name, Draw.Font(), 12));
        double width = pad + 16 + valueWidth + 8 + nameWidth + pad;
        double height = pad + rowHeight * (rows.Count + 1) + pad - 4;
        double left = x + 12 + width <= Bounds.Width - 4 ? x + 12 : x - 12 - width;
        var box = new Rect(left, plot.Top, width, height);
        Fill(g, box, 5, Palette.Background);
        Outline(g, box.Deflate(0.5), 5, Palette.Border, 1);

        Draw.Text(g, header, Draw.Font(), 12, Palette.SubtleText, new Rect(box.X + pad, box.Y + pad - 2, width - pad * 2, rowHeight));
        double y = box.Y + pad - 2 + rowHeight;
        foreach (var (s, text) in rows)
        {
            g.DrawLine(new Pen(Draw.Brush(SeriesColor(s)), 2, lineCap: PenLineCap.Round),
                new Point(box.X + pad, y + rowHeight / 2), new Point(box.X + pad + 10, y + rowHeight / 2));
            Draw.Text(g, text, valueFont, 12, Palette.Text, new Rect(box.X + pad + 16, y, valueWidth + 1, rowHeight));
            Draw.Text(g, s.Name, Draw.Font(), 12, Palette.SubtleText, new Rect(box.X + pad + 16 + valueWidth + 8, y, nameWidth + 2, rowHeight));
            y += rowHeight;
        }
    }

    /// <summary>The crosshair snaps to the nearest reading, so readers aim at a time, not at a 2px line.</summary>
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var position = e.GetPosition(this);
        DateTime? snapped = null;
        if (_snapTimes.Length > 0 && Plot.Contains(position))
        {
            var target = TimeAt(position.X);
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
            InvalidateVisual();
        }
        Cursor = _legendHits.Any(h => h.Bounds.Contains(position)) ? new Cursor(StandardCursorType.Hand) : Cursor.Default;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        var position = e.GetPosition(this);
        if (e.InitialPressMouseButton == MouseButton.Left && _legendHits.FirstOrDefault(h => h.Bounds.Contains(position)).Id is string id)
            LegendClicked?.Invoke(this, id);
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _hoverTime = null;
        InvalidateVisual();
    }
}
