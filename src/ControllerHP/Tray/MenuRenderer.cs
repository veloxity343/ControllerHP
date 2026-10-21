namespace ControllerHP.Tray;

/// <summary>
/// Draws a menu (and its submenus) in the given palette with Windows 11's rounded corners, instead of
/// WinForms' fixed light style.
/// </summary>
internal sealed class MenuRenderer(Palette palette) : ToolStripRenderer
{
    /// <summary>
    /// Styles a menu and all its submenus. Call after (re)building the items; it creates each window's
    /// handle so the corners are rounded before it's first shown.
    /// </summary>
    public static void Apply(ToolStripDropDown menu, Palette palette)
    {
        menu.Renderer = new MenuRenderer(palette);
        menu.BackColor = palette.Background;
        menu.ForeColor = palette.Text;
        Theme.StyleWindow(menu.Handle, Theme.Corners.RoundSmall, palette.Border);
        foreach (var item in menu.Items.OfType<ToolStripMenuItem>().Where(i => i.HasDropDownItems))
            Apply(item.DropDown, palette);
    }

    protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
    {
        using var brush = new SolidBrush(palette.Background);
        e.Graphics.FillRectangle(brush, e.AffectedBounds);
    }

    // Windows draws the border itself, following the rounded corners.
    protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e) { }

    protected override void OnRenderImageMargin(ToolStripRenderEventArgs e) { }

    protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
    {
        if (!e.Item.Selected || !e.Item.Enabled)
            return;
        var bounds = new Rectangle(4, 1, e.Item.Width - 8, e.Item.Height - 2);
        using var brush = new SolidBrush(palette.Hover);
        using var path = RoundedRect(bounds, 4);
        e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        e.Graphics.FillPath(brush, path);
    }

    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        e.TextColor = e.Item.Enabled ? palette.Text : palette.SubtleText;
        base.OnRenderItemText(e);
    }

    protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
    {
        int y = e.Item.Height / 2;
        using var pen = new Pen(palette.Separator);
        e.Graphics.DrawLine(pen, 8, y, e.Item.Width - 8, y);
    }

    protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
    {
        e.ArrowColor = palette.Text;
        base.OnRenderArrow(e);
    }

    protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
    {
        // A tick in the text colour, in place of WinForms' boxed check.
        var r = e.ImageRectangle;
        float cx = r.X + r.Width / 2f, cy = r.Y + r.Height / 2f, s = Math.Min(r.Width, r.Height) / 2.6f;
        using var pen = new Pen(palette.Text, Math.Max(1.5f, s / 3.5f))
        {
            StartCap = System.Drawing.Drawing2D.LineCap.Round,
            EndCap = System.Drawing.Drawing2D.LineCap.Round,
            LineJoin = System.Drawing.Drawing2D.LineJoin.Round,
        };
        e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        e.Graphics.DrawLines(pen, [new PointF(cx - s, cy), new PointF(cx - s / 3, cy + s * 0.65f), new PointF(cx + s, cy - s * 0.7f)]);
    }

    private static System.Drawing.Drawing2D.GraphicsPath RoundedRect(Rectangle r, int radius)
    {
        int d = radius * 2;
        var path = new System.Drawing.Drawing2D.GraphicsPath();
        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}
