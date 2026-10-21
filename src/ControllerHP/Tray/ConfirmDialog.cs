namespace ControllerHP.Tray;

/// <summary>
/// A small themed yes/no dialog, since WinForms' MessageBox ignores dark mode. Cancel has focus, so
/// pressing Enter by reflex doesn't confirm; Esc and closing the window cancel too.
/// </summary>
internal sealed class ConfirmDialog : Form
{
    private const int LogicalWidth = 420, Padding_ = 20;

    private readonly Palette _palette;
    private readonly string _title, _message;
    private readonly ActionButton _confirm, _cancel = new("Cancel");

    private ConfirmDialog(string title, string message, string confirmText, Palette palette)
    {
        _title = title;
        _message = message;
        _palette = palette;
        _confirm = new ActionButton(confirmText);

        Text = "ControllerHP";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        AutoScaleMode = AutoScaleMode.None;
        KeyPreview = true;
        DoubleBuffered = true;
        BackColor = palette.Background;

        _ = Handle;
        Theme.SetTitleBar(Handle, palette.Light);
        foreach (var button in new[] { _confirm, _cancel })
        {
            button.Palette = palette;
            button.BackColor = palette.Background;
            Controls.Add(button);
        }
        _confirm.Click += (_, _) => Close(DialogResult.OK);
        _cancel.Click += (_, _) => Close(DialogResult.Cancel);

        // Height follows the wrapped message.
        using var font = MessageFont();
        int textHeight = TextRenderer.MeasureText(message, font, new Size(S(LogicalWidth - Padding_ * 2), 0),
            TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix).Height;
        ClientSize = new Size(S(LogicalWidth), S(Padding_) + S(28) + S(8) + textHeight + S(24) + _cancel.Height + S(Padding_));
        _cancel.Location = new Point(ClientSize.Width - S(Padding_) - _cancel.Width, ClientSize.Height - S(Padding_) - _cancel.Height);
        _confirm.Location = new Point(_cancel.Left - S(8) - _confirm.Width, _cancel.Top);
    }

    /// <summary>Asks the question over <paramref name="owner"/>; true only if the user confirmed.</summary>
    public static bool Ask(IWin32Window owner, string title, string message, string confirmText, Palette palette)
    {
        using var dialog = new ConfirmDialog(title, message, confirmText, palette);
        return dialog.ShowDialog(owner) == DialogResult.OK;
    }

    private int S(float logicalPixels) => (int)Math.Round(logicalPixels * DeviceDpi / 96f);

    private Font MessageFont() => new("Segoe UI", 13f * DeviceDpi / 96f, GraphicsUnit.Pixel);

    private void Close(DialogResult result)
    {
        DialogResult = result;
        Close();
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        _cancel.Focus();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyCode == Keys.Escape)
            Close(DialogResult.Cancel);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        int width = ClientSize.Width - S(Padding_) * 2;
        using (var titleFont = new Font("Segoe UI Semibold", 16f * DeviceDpi / 96f, GraphicsUnit.Pixel))
            TextRenderer.DrawText(g, _title, titleFont, new Rectangle(S(Padding_), S(Padding_), width, S(28)), _palette.Text,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        using var font = MessageFont();
        TextRenderer.DrawText(g, _message, font,
            new Rectangle(S(Padding_), S(Padding_) + S(28) + S(8), width, _cancel.Top - S(24) - S(Padding_) - S(36)),
            _palette.SubtleText, TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
    }
}
