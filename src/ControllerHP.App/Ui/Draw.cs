using System.Globalization;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace ControllerHP.Ui;

/// <summary>Drawing helpers shared by the custom-drawn controls, in logical pixels.</summary>
internal static class Draw
{
    public static readonly FontFamily Segoe = new("Segoe UI");

    public static Typeface Font(bool semibold = false) =>
        new(Segoe, FontStyle.Normal, semibold ? FontWeight.SemiBold : FontWeight.Normal);

    public static IBrush Brush(Color color) => new ImmutableSolidColorBrush(color);

    /// <summary>One line of text in <paramref name="area"/>, centred vertically, cut short with "…" if it won't fit.</summary>
    public static void Text(DrawingContext g, string text, Typeface font, double size, Color color, Rect area,
        TextAlignment alignment = TextAlignment.Left)
    {
        var formatted = Format(text, font, size, color);
        formatted.MaxTextWidth = Math.Max(1, area.Width);
        formatted.MaxLineCount = 1;
        formatted.Trimming = TextTrimming.CharacterEllipsis;
        formatted.TextAlignment = alignment;
        g.DrawText(formatted, new Point(area.X, area.Y + (area.Height - formatted.Height) / 2));
    }

    public static FormattedText Format(string text, Typeface font, double size, Color color) =>
        new(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, font, size, Brush(color));

    /// <summary>How wide a line of text is.</summary>
    public static double Width(string text, Typeface font, double size) => Format(text, font, size, Colors.Black).Width;

    public static void RoundedRect(DrawingContext g, Color color, Rect area, double radius) =>
        g.DrawRectangle(Brush(color), null, new RoundedRect(area, radius));
}
