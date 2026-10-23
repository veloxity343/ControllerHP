namespace ControllerHP.Tray;

/// <summary>
/// A small themed yes/no dialog, since WinForms' MessageBox ignores dark mode. Cancel has focus, so
/// pressing Enter by reflex doesn't confirm; Esc and closing the window cancel too. It can also ask for
/// a line of text, when the text box has focus and Enter confirms.
/// </summary>
internal sealed class ConfirmDialog : Form
{
    private const int LogicalWidth = 420, Padding_ = 20;

    private readonly Palette _palette;
    private readonly string _title, _message;
    private readonly ActionButton _confirm, _cancel = new("Cancel");
    private readonly TextBox? _input;
    /// <summary>The title's height: one line, or more for a long device name.</summary>
    private readonly int _titleHeight;

    private ConfirmDialog(string title, string message, string confirmText, Palette palette, string? text = null,
        int maxLength = 0)
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

        // Height follows the wrapped title and message, and the text box if there is one.
        using (var titleFont = TitleFont())
            _titleHeight = Math.Max(S(28), TextRenderer.MeasureText(title, titleFont, new Size(S(LogicalWidth - Padding_ * 2), 0),
                TitleFlags).Height + S(4));
        using var font = MessageFont();
        int textHeight = TextRenderer.MeasureText(message, font, new Size(S(LogicalWidth - Padding_ * 2), 0),
            TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix).Height;
        int inputHeight = 0;
        if (text is not null)
        {
            _input = new TextBox
            {
                Text = text,
                MaxLength = maxLength,
                Font = new Font("Segoe UI", 14f * DeviceDpi / 96f, GraphicsUnit.Pixel),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = palette.Surface,
                ForeColor = palette.Text,
                Location = new Point(S(Padding_), S(Padding_) + _titleHeight + S(8) + textHeight + S(12)),
                Width = S(LogicalWidth - Padding_ * 2),
            };
            _input.KeyDown += (_, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    e.SuppressKeyPress = true;
                    Close(DialogResult.OK);
                }
            };
            Controls.Add(_input);
            inputHeight = S(12) + _input.Height;
        }
        ClientSize = new Size(S(LogicalWidth), S(Padding_) + _titleHeight + S(8) + textHeight + inputHeight + S(24) + _cancel.Height + S(Padding_));
        _cancel.Location = new Point(ClientSize.Width - S(Padding_) - _cancel.Width, ClientSize.Height - S(Padding_) - _cancel.Height);
        _confirm.Location = new Point(_cancel.Left - S(8) - _confirm.Width, _cancel.Top);
    }

    /// <summary>Asks the question over <paramref name="owner"/>; true only if the user confirmed.</summary>
    public static bool Ask(IWin32Window owner, string title, string message, string confirmText, Palette palette)
    {
        using var dialog = new ConfirmDialog(title, message, confirmText, palette);
        return dialog.ShowDialog(owner) == DialogResult.OK;
    }

    /// <summary>Asks for a line of text over <paramref name="owner"/>, starting from <paramref name="text"/>; null if cancelled.</summary>
    public static string? AskText(IWin32Window owner, string title, string message, string text, int maxLength,
        string confirmText, Palette palette)
    {
        using var dialog = new ConfirmDialog(title, message, confirmText, palette, text, maxLength);
        return dialog.ShowDialog(owner) == DialogResult.OK ? dialog._input!.Text : null;
    }

    private int S(float logicalPixels) => (int)Math.Round(logicalPixels * DeviceDpi / 96f);

    private Font MessageFont() => new("Segoe UI", 13f * DeviceDpi / 96f, GraphicsUnit.Pixel);
    private Font TitleFont() => new("Segoe UI Semibold", 16f * DeviceDpi / 96f, GraphicsUnit.Pixel);

    private const TextFormatFlags TitleFlags = TextFormatFlags.Left | TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl |
        TextFormatFlags.NoPrefix;

    private void Close(DialogResult result)
    {
        DialogResult = result;
        Close();
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        if (_input is not null)
        {
            _input.Focus();
            _input.SelectAll();
        }
        else
        {
            _cancel.Focus();
        }
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
        using (var titleFont = TitleFont())
        {
            // One line sits centred in its row, as before; more start at its top.
            int textHeight = TextRenderer.MeasureText(_title, titleFont, new Size(width, 0), TitleFlags).Height;
            int top = S(Padding_) + Math.Max(0, (_titleHeight - textHeight) / 2);
            TextRenderer.DrawText(g, _title, titleFont, new Rectangle(S(Padding_), top, width, textHeight), _palette.Text, TitleFlags);
        }
        using var font = MessageFont();
        TextRenderer.DrawText(g, _message, font,
            new Rectangle(S(Padding_), S(Padding_) + _titleHeight + S(8), width, (_input?.Top - S(12) ?? _cancel.Top - S(24)) - S(Padding_) - _titleHeight - S(8)),
            _palette.SubtleText, TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
    }
}
