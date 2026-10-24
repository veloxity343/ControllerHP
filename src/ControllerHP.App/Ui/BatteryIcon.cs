using System.Globalization;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Media.Imaging;
using ControllerHP.Battery;

namespace ControllerHP.Ui;

/// <summary>
/// Draws the device's shape with a battery bar beside it, in a colour that tracks the charge: along the
/// bottom for wide devices like controllers, up the right for tall ones like mice. Devices without a shape
/// of their own get a Bluetooth sign. A bolt over the bottom-right corner marks charging.
/// </summary>
internal static class BatteryIcon
{
    // Colour stops for the fill; values between stops are blended so the colour shifts smoothly.
    private static readonly (int Percent, Color Color)[] ColorStops =
    [
        (0, Color.FromRgb(229, 57, 53)),    // red
        (20, Color.FromRgb(229, 57, 53)),   // red
        (50, Color.FromRgb(250, 204, 21)),  // yellow
        (75, Color.FromRgb(52, 199, 89)),   // green
        (100, Color.FromRgb(52, 199, 89)),  // green
    ];

    private static readonly Color Halo = Color.FromArgb(200, 0, 0, 0);

    /// <summary>The icon at <paramref name="size"/> pixels square, for a light or dark background.</summary>
    public static RenderTargetBitmap Render(ControllerBattery battery, bool lightBackground, int size)
    {
        var bitmap = new RenderTargetBitmap(new PixelSize(size, size), new Vector(96, 96));
        using (var g = bitmap.CreateDrawingContext())
        {
            // Everything below is laid out on a 16x16 grid and scaled to the real size.
            using (g.PushTransform(Matrix.CreateScale(size / 16.0, size / 16.0)))
                Draw(g, battery, lightBackground ? Color.FromRgb(32, 32, 32) : Colors.White, pixelArt: size == 16);
        }
        return bitmap;
    }

    public static Color FillColor(int percent)
    {
        percent = Math.Clamp(percent, 0, 100);
        for (int i = 1; i < ColorStops.Length; i++)
        {
            var (hiPercent, hi) = ColorStops[i];
            if (percent > hiPercent)
                continue;
            var (loPercent, lo) = ColorStops[i - 1];
            float t = hiPercent == loPercent ? 0 : (percent - loPercent) / (float)(hiPercent - loPercent);
            return Color.FromRgb((byte)(lo.R + (hi.R - lo.R) * t), (byte)(lo.G + (hi.G - lo.G) * t), (byte)(lo.B + (hi.B - lo.B) * t));
        }
        return ColorStops[^1].Color;
    }

    // Layout on the 16x16 grid. Wide devices sit above a thin bar along the bottom, tall ones beside a thin
    // bar up the right. At 16 px the bar is 2 pixels, so it stays crisp; larger sizes draw it 1.5 units.
    private sealed record Layout(Rect Device, Rect Bar, Rect PixelBar, bool Upright);

    private static readonly Layout Wide = new(
        Device: new(0, 1, 16, 12), Bar: new(1, 14.5, 14, 1.5), PixelBar: new(1, 14, 14, 2), Upright: false);

    private static readonly Layout Tall = new(
        Device: new(0, 0, 13, 16), Bar: new(14.5, 1, 1.5, 14), PixelBar: new(14, 1, 2, 14), Upright: true);

    /// <summary>The bolt or "?" sits over the bottom-right corner, across the bar and the device.</summary>
    private static readonly Rect Badge = new(11, 9, 5, 7);

    private static Layout LayoutFor(DeviceKind kind) => kind is DeviceKind.Mouse or DeviceKind.Other ? Tall : Wide;

    private static IBrush Brush(Color color) => new ImmutableSolidColorBrush(color);

    /// <param name="pixelArt">At 16 px, draw from hand-placed pixels, which stay crisp where a smoothed shape
    /// would blur.</param>
    private static void Draw(DrawingContext g, ControllerBattery battery, Color foreground, bool pixelArt)
    {
        int? percent = battery.EffectivePercent;
        bool wired = battery.Power == ChargeState.Wired;
        bool charging = battery.Power == ChargeState.Charging;
        string? badge = wired || charging ? "bolt" : percent is null ? "?" : null;
        var layout = LayoutFor(battery.Kind);

        // Hand-placed pixels and the bar's edges are drawn without smoothing, so they land on whole pixels.
        using (g.PushRenderOptions(new RenderOptions { EdgeMode = pixelArt ? EdgeMode.Aliased : EdgeMode.Antialias }))
        {
            if (pixelArt && Pixels.TryGetValue(battery.Kind, out var pixels))
                DrawPixels(g, pixels, layout.Device, foreground);
            else
                DrawGlyph(g, battery.Kind, layout.Device, foreground);

            var bar = pixelArt ? layout.PixelBar : layout.Bar;
            g.FillRectangle(Brush(Color.FromArgb(70, foreground.R, foreground.G, foreground.B)), bar);
            if (wired)
            {
                // Powered by the cable, with no battery: a full bar in the device's own colour.
                g.FillRectangle(Brush(foreground), bar);
            }
            else if (percent is int p)
            {
                // Whole pixels at 16 px, so the end of the fill is sharp; a sliver stays near 0% so "empty"
                // still reads as red.
                double length = (layout.Upright ? bar.Height : bar.Width) * Math.Clamp(p, 0, 100) / 100.0;
                length = Math.Max(1, pixelArt ? Math.Round(length) : length);
                var fill = layout.Upright
                    ? new Rect(bar.X, bar.Bottom - length, bar.Width, length)
                    : new Rect(bar.X, bar.Y, length, bar.Height);
                g.FillRectangle(Brush(FillColor(p)), fill);
            }

            if (badge == "bolt")
            {
                if (pixelArt)
                    DrawPixels(g, BoltPixels, Badge, Colors.White, Halo);
                else
                    DrawBolt(g, Badge);
            }
            else if (badge == "?")
            {
                var outline = Brightness(foreground) > 0.5 ? Halo : Color.FromArgb(220, 255, 255, 255);
                if (pixelArt)
                    DrawPixels(g, QuestionPixels, Badge, foreground, outline);
                else
                    DrawBadge(g, "?", Badge, foreground, outline);
            }
        }
    }

    /// <summary>A colour's lightness, as GDI+'s GetBrightness gives it: halfway between its strongest and weakest channel.</summary>
    private static double Brightness(Color c) => (Math.Max(c.R, Math.Max(c.G, c.B)) + Math.Min(c.R, Math.Min(c.G, c.B))) / 510.0;

    /// <summary>The smooth shape, enlarged to fill <paramref name="area"/>.</summary>
    private static void DrawGlyph(DrawingContext g, DeviceKind kind, Rect area, Color foreground)
    {
        var (glyph, glyphBounds) = Glyph(kind);
        // The strokes aren't in the path: the Bluetooth sign is all strokes, and the headset's band arches
        // above its ear cups.
        var bounds = kind switch
        {
            DeviceKind.Other => new Rect(5.6, 4.1, 4.4, 7.8),
            DeviceKind.Headset => new Rect(3.6, 3.9, 8.8, 7.7),
            _ => glyphBounds,
        };
        double scale = Math.Min((area.Width - 1) / bounds.Width, (area.Height - 1) / bounds.Height);
        var transform = Matrix.CreateTranslation(-(bounds.X + bounds.Width / 2), -(bounds.Y + bounds.Height / 2))
            * Matrix.CreateScale(scale, scale)
            * Matrix.CreateTranslation(area.X + area.Width / 2, area.Y + area.Height / 2);
        using (g.PushTransform(transform))
        {
            if (glyph is not null)
                g.DrawGeometry(Brush(foreground), null, glyph);
            if (kind is DeviceKind.Headset or DeviceKind.Other)
                DrawStrokes(g, kind, foreground);
        }
    }

    // Each device drawn pixel by pixel for 16 px: X is filled. They're centred in their layout's space.
    private static readonly Dictionary<DeviceKind, string[]> Pixels = new()
    {
        [DeviceKind.Gamepad] =
        [
            "...XXXXXXXX...",
            ".XXXXXXXXXXXX.",
            ".XX.XXXXXX.XX.",
            "XX...XXXX.X.XX",
            "XXX.XXXXXX.XXX",
            "XXXXXXXXXXXXXX",
            "XXXXXXXXXXXXXX",
            "XXXXX....XXXXX",
            "XXXX......XXXX",
            ".XX........XX.",
        ],
        [DeviceKind.Mouse] =
        [
            "..XX.XX..",
            ".XXX.XXX.",
            "XXXX.XXXX",
            "XXXX.XXXX",
            "XXXX.XXXX",
            "XXXXXXXXX",
            "XXXXXXXXX",
            "XXXXXXXXX",
            "XXXXXXXXX",
            "XXXXXXXXX",
            "XXXXXXXXX",
            ".XXXXXXX.",
            "..XXXXX..",
        ],
        [DeviceKind.Keyboard] =
        [
            ".XXXXXXXXXXX.",
            "XX.X.X.X.X.XX",
            "XXXXXXXXXXXXX",
            "XX.X.X.X.X.XX",
            "XXXXXXXXXXXXX",
            "XXX.......XXX",
            "XXXXXXXXXXXXX",
            ".XXXXXXXXXXX.",
        ],
        [DeviceKind.Headset] =
        [
            "....XXXXXX....",
            "..XX......XX..",
            ".X..........X.",
            ".X..........X.",
            "X............X",
            "X............X",
            "XXX........XXX",
            "XXX........XXX",
            "XXX........XXX",
            "XXX........XXX",
            "XXX........XXX",
            ".XX........XX.",
        ],
        [DeviceKind.Other] =
        [
            "....X....",
            "....XX...",
            "....X.X..",
            ".X..X..X.",
            "..X.X.X..",
            "...XXX...",
            "....X....",
            "...XXX...",
            "..X.X.X..",
            ".X..X..X.",
            "....X.X..",
            "....XX...",
            "....X....",
        ],
    };

    private static readonly string[] BoltPixels =
    [
        "...XX",
        "..XX.",
        ".XX..",
        "XXXXX",
        "..XX.",
        ".XX..",
        "XX...",
    ];

    private static readonly string[] QuestionPixels =
    [
        ".XXX.",
        "X...X",
        "....X",
        "..XX.",
        "..X..",
        ".....",
        "..X..",
    ];

    /// <summary>
    /// Draws <paramref name="rows"/> centred in <paramref name="area"/>, on whole pixels. With an
    /// <paramref name="outline"/>, the pixels around the shape are filled with it first, so it stands
    /// apart from whatever is under it.
    /// </summary>
    private static void DrawPixels(DrawingContext g, string[] rows, Rect area, Color color, Color? outline = null)
    {
        int width = rows[0].Length, height = rows.Length;
        int left = (int)(area.X + (area.Width - width) / 2), top = (int)(area.Y + (area.Height - height) / 2);
        bool Filled(int x, int y) => y >= 0 && y < height && x >= 0 && x < width && rows[y][x] == 'X';

        if (outline is Color around)
        {
            var halo = Brush(around);
            for (int y = -1; y <= height; y++)
                for (int x = -1; x <= width; x++)
                    if (!Filled(x, y) && (Filled(x - 1, y) || Filled(x + 1, y) || Filled(x, y - 1) || Filled(x, y + 1)))
                        g.FillRectangle(halo, new Rect(left + x, top + y, 1, 1));
        }
        var brush = Brush(color);
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                if (Filled(x, y))
                    g.FillRectangle(brush, new Rect(left + x, top + y, 1, 1));
    }

    /// <summary>
    /// The device's smooth silhouette, for sizes above 16 px, and its outline's bounds. Inner shapes are separate
    /// figures, filled even-odd, so they're cut out of it.
    /// </summary>
    private static (Geometry? Glyph, Rect Bounds) Glyph(DeviceKind kind)
    {
        var group = new GeometryGroup { FillRule = FillRule.EvenOdd };
        switch (kind)
        {
            case DeviceKind.Gamepad:
            {
                const double top = 5.0, bottom = 11.1, waist = 8.8;
                (Point Start, Point C1, Point C2, Point End)[] curves =
                [
                    (new(10.0, top), new(11.8, top), new(12.6, 6.1), new(12.9, 7.8)),
                    (new(12.9, 7.8), new(13.3, 9.7), new(13.0, bottom), new(11.8, bottom)),
                    (new(11.8, bottom), new(10.9, bottom), new(10.4, 9.9), new(9.7, waist)),
                    (new(6.3, waist), new(5.6, 9.9), new(5.1, bottom), new(4.2, bottom)),
                    (new(4.2, bottom), new(3.0, bottom), new(2.7, 9.7), new(3.1, 7.8)),
                    (new(3.1, 7.8), new(3.4, 6.1), new(4.2, top), new(6.0, top)),
                ];
                var body = new StreamGeometry();
                using (var c = body.Open())
                {
                    c.BeginFigure(new Point(6.0, top), true);
                    c.LineTo(new Point(10.0, top));
                    c.CubicBezierTo(curves[0].C1, curves[0].C2, curves[0].End);
                    c.CubicBezierTo(curves[1].C1, curves[1].C2, curves[1].End);
                    c.CubicBezierTo(curves[2].C1, curves[2].C2, curves[2].End);
                    c.LineTo(new Point(6.3, waist));
                    c.CubicBezierTo(curves[3].C1, curves[3].C2, curves[3].End);
                    c.CubicBezierTo(curves[4].C1, curves[4].C2, curves[4].End);
                    c.CubicBezierTo(curves[5].C1, curves[5].C2, curves[5].End);
                    c.EndFigure(true);
                }
                group.Children.Add(body);
                // The d-pad and a button.
                group.Children.Add(new RectangleGeometry(new Rect(4.3, 6.5, 2.4, 0.9)));
                group.Children.Add(new RectangleGeometry(new Rect(5.05, 5.75, 0.9, 0.75)));
                group.Children.Add(new RectangleGeometry(new Rect(5.05, 7.4, 0.9, 0.75)));
                group.Children.Add(new EllipseGeometry(new Rect(9.4, 6.2, 1.6, 1.6)));
                return (group, CurveBounds(curves));
            }
            case DeviceKind.Mouse:
            {
                var outline = new Rect(5.4, 3.9, 5.2, 8.2);
                group.Children.Add(RoundedRect(outline, 2.6));
                // The split between the buttons.
                group.Children.Add(new RectangleGeometry(new Rect(7.6, 4.7, 0.8, 2.3)));
                return (group, outline);
            }
            case DeviceKind.Keyboard:
            {
                var outline = new Rect(3.4, 5.3, 9.2, 5.4);
                group.Children.Add(RoundedRect(outline, 1));
                foreach (double x in new[] { 4.4, 6.3, 8.2, 10.1 })
                    group.Children.Add(new RectangleGeometry(new Rect(x, 6.3, 1.5, 1.2)));
                group.Children.Add(new RectangleGeometry(new Rect(5.4, 8.5, 5.2, 1.1)));
                return (group, outline);
            }
            case DeviceKind.Headset:
                // The ear cups; the band is drawn as a stroke.
                group.Children.Add(RoundedRect(new Rect(3.6, 8.0, 2.4, 3.6), 0.9));
                group.Children.Add(RoundedRect(new Rect(10.0, 8.0, 2.4, 3.6), 0.9));
                return (group, default);
            default:
                return (null, default);
        }
    }

    /// <summary>
    /// The bounds of a run of cubic curves as GDI+ measured them for the icons until now: around every point
    /// that defines them, control points included, so a little wider than the outline itself. The shape is
    /// scaled to fit these, so measuring them the same way keeps the controller the size it was.
    /// </summary>
    private static Rect CurveBounds((Point Start, Point C1, Point C2, Point End)[] curves)
    {
        var points = curves.SelectMany(c => new[] { c.Start, c.C1, c.C2, c.End }).ToList();
        double left = points.Min(p => p.X), top = points.Min(p => p.Y);
        return new Rect(left, top, points.Max(p => p.X) - left, points.Max(p => p.Y) - top);
    }

    /// <summary>The parts of a shape that are lines rather than solid: the headset's band, the Bluetooth sign.</summary>
    private static void DrawStrokes(DrawingContext g, DeviceKind kind, Color foreground)
    {
        var pen = new Pen(Brush(foreground), 1.1, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
        var stroke = new StreamGeometry();
        using (var c = stroke.Open())
        {
            if (kind == DeviceKind.Headset)
            {
                // The top half of a circle 7.4 across, from its left to its right.
                c.BeginFigure(new Point(4.3, 8.1), false);
                c.ArcTo(new Point(11.7, 8.1), new Size(3.7, 3.7), 0, false, SweepDirection.Clockwise);
                c.EndFigure(false);
            }
            else
            {
                Point[] sign = [new(5.6, 6.2), new(10, 9.9), new(8, 11.9), new(8, 4.1), new(10, 6.1), new(5.6, 9.8)];
                c.BeginFigure(sign[0], false);
                foreach (var point in sign[1..])
                    c.LineTo(point);
                c.EndFigure(false);
            }
        }
        g.DrawGeometry(null, pen, stroke);
    }

    /// <summary>A bolt filling <paramref name="area"/>, haloed so it stands out over the device and the bar.</summary>
    private static void DrawBolt(DrawingContext g, Rect area)
    {
        double x = area.X, y = area.Y, w = area.Width, h = area.Height;
        Point[] points =
        [
            new(x + w * 0.75, y), new(x, y + h * 0.58), new(x + w * 0.45, y + h * 0.58),
            new(x + w * 0.25, y + h), new(x + w, y + h * 0.42), new(x + w * 0.55, y + h * 0.42),
        ];
        var bolt = new PolylineGeometry(points, true);
        g.DrawGeometry(null, new Pen(Brush(Halo), 1.3, lineJoin: PenLineJoin.Round), bolt);
        g.DrawGeometry(Brushes.White, null, bolt);
    }

    /// <summary>A small bold mark filling <paramref name="area"/>, such as "?" while the level isn't known yet.</summary>
    private static void DrawBadge(DrawingContext g, string text, Rect area, Color color, Color outline)
    {
        var formatted = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            new Typeface("Segoe UI", FontStyle.Normal, FontWeight.Bold), 10, Brushes.Black);
        if (formatted.BuildGeometry(new Point(0, 0)) is not { } path)
            return;
        var bounds = path.Bounds;
        double scale = Math.Min(area.Width / bounds.Width, area.Height / bounds.Height);
        var transform = Matrix.CreateTranslation(-(bounds.X + bounds.Width / 2), -(bounds.Y + bounds.Height / 2))
            * Matrix.CreateScale(scale, scale)
            * Matrix.CreateTranslation(area.X + area.Width / 2, area.Y + area.Height / 2);
        using (g.PushTransform(transform))
        {
            // The outline's width is in the icon's units, so undo the scale for it.
            g.DrawGeometry(null, new Pen(Brush(outline), 1.4 / scale, lineJoin: PenLineJoin.Round), path);
            g.DrawGeometry(Brush(color), null, path);
        }
    }

    private static Geometry RoundedRect(Rect r, double radius)
    {
        double d = Math.Min(radius, Math.Min(r.Width, r.Height) / 2);
        return new RectangleGeometry(r, d, d);
    }
}
