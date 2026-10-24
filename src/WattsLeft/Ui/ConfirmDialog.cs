using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;

namespace WattsLeft.Ui;

/// <summary>
/// A small themed yes/no dialog. Cancel has focus, so pressing Enter by reflex doesn't confirm; Esc and closing
/// the window cancel too. It can also ask for a line of text, when the text box has focus and Enter confirms, and
/// carry a button of its own at the left, such as a link, which doesn't close it.
/// </summary>
internal sealed class ConfirmDialog : Window
{
    private const double DialogWidth = 420, Padding_ = 20;

    private readonly Palette _palette;
    private readonly ActionButton _confirm, _cancel;
    private readonly ActionButton? _extra;
    private readonly TextBox? _input;
    private readonly DialogBody _body;

    /// <summary>Set when the user confirms, for a dialog shown on its own, which has no dialog result.</summary>
    private bool _confirmed;

    private ConfirmDialog(string title, string message, string confirmText, Palette palette, string? text = null, int maxLength = 0,
        string cancelText = "Cancel", (string Text, Action Click)? extra = null)
    {
        _palette = palette;
        _confirm = new ActionButton(confirmText);
        _cancel = new ActionButton(cancelText);
        if (extra is { } button)
        {
            _extra = new ActionButton(button.Text);
            _extra.Click += (_, _) => button.Click();
        }
        Title = "WattsLeft";
        CanResize = false;
        CanMinimize = false;
        CanMaximize = false;
        ShowInTaskbar = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        RequestedThemeVariant = palette.Light ? ThemeVariant.Light : ThemeVariant.Dark;
        Background = Draw.Brush(palette.Background);
        Width = DialogWidth;
        SizeToContent = SizeToContent.Height;

        if (text is not null)
        {
            _input = new TextBox
            {
                Text = text,
                MaxLength = maxLength,
                FontFamily = Draw.Segoe,
                FontSize = 14,
                Background = Draw.Brush(palette.Surface),
                Foreground = Draw.Brush(palette.Text),
                BorderBrush = Draw.Brush(palette.Border),
            };
            _input.KeyDown += (_, e) =>
            {
                if (e.Key == Key.Enter)
                {
                    e.Handled = true;
                    _confirmed = true;
                    Close(true);
                }
            };
        }
        _body = new DialogBody(title, message, _input, _confirm, _cancel, _extra) { Palette = palette };
        Content = _body;
        _confirm.Click += (_, _) =>
        {
            _confirmed = true;
            Close(true);
        };
        _cancel.Click += (_, _) => Close(false);
    }

    /// <summary>Asks the question over <paramref name="owner"/>; true only if the user confirmed.</summary>
    public static async Task<bool> Ask(Window owner, string title, string message, string confirmText, Palette palette,
        string cancelText = "Cancel", (string Text, Action Click)? extra = null)
    {
        var dialog = new ConfirmDialog(title, message, confirmText, palette, cancelText: cancelText, extra: extra);
        await dialog.ShowDialog<bool>(owner);
        return dialog._confirmed;
    }

    /// <summary>
    /// Asks with no window behind it, in the middle of the screen and on the taskbar so it can't get lost; true
    /// only if the user confirmed. Closing it, or Esc, answers no.
    /// </summary>
    public static Task<bool> AskOnItsOwn(string title, string message, string confirmText, string cancelText, Palette palette,
        (string Text, Action Click)? extra = null)
    {
        var dialog = new ConfirmDialog(title, message, confirmText, palette, cancelText: cancelText, extra: extra)
        {
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            ShowInTaskbar = true,
        };
        using (var icon = typeof(ConfirmDialog).Assembly.GetManifestResourceStream("WattsLeft.app.ico")!)
            dialog.Icon = new WindowIcon(icon);
        var answer = new TaskCompletionSource<bool>();
        dialog.Closed += (_, _) => answer.TrySetResult(dialog._confirmed);
        dialog.Show();
        dialog.Activate();
        return answer.Task;
    }

    /// <summary>Asks for a line of text over <paramref name="owner"/>, starting from <paramref name="text"/>; null if cancelled.</summary>
    public static async Task<string?> AskText(Window owner, string title, string message, string text, int maxLength,
        string confirmText, Palette palette)
    {
        var dialog = new ConfirmDialog(title, message, confirmText, palette, text, maxLength);
        return await dialog.ShowDialog<bool>(owner) ? dialog._input!.Text ?? "" : null;
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        if (TryGetPlatformHandle()?.Handle is IntPtr hwnd)
            Appearance.SetTitleBar(hwnd, _palette.Light);
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
        if (e.Key == Key.Escape)
            Close(false);
    }

    /// <summary>The title and message, wrapped, then the text box if there is one, then the buttons at the right.</summary>
    private sealed class DialogBody : ThemedPanel
    {
        private readonly string _title, _message;
        private readonly TextBox? _input;
        private readonly ActionButton _confirm, _cancel;
        private readonly ActionButton? _extra;

        public DialogBody(string title, string message, TextBox? input, ActionButton confirm, ActionButton cancel, ActionButton? extra)
        {
            _title = title;
            _message = message;
            _input = input;
            _confirm = confirm;
            _cancel = cancel;
            _extra = extra;
            if (input is not null)
                Children.Add(input);
            if (extra is not null)
                Children.Add(extra);
            Children.Add(confirm);
            Children.Add(cancel);
        }

        private static double TextWidth => DialogWidth - Padding_ * 2;

        /// <summary>The title's height: one line, or more for a long device name.</summary>
        private double TitleHeight => Math.Max(28, Draw.TextHeight(_title, Draw.Font(semibold: true), 16, TextWidth) + 4);

        private double MessageHeight => Draw.TextHeight(_message, Draw.Font(), 13, TextWidth);

        private double InputTop => Padding_ + TitleHeight + 8 + MessageHeight + 12;

        public override double HeightFor(double width) =>
            Padding_ + TitleHeight + 8 + MessageHeight + (_input is null ? 0 : 12 + InputHeight) + 24 + _cancel.Height + Padding_;

        private double InputHeight => _input?.DesiredSize.Height ?? 0;

        protected override Size MeasureOverride(Size available)
        {
            foreach (var child in Children)
                child.Measure(new Size(TextWidth, double.PositiveInfinity));
            return new Size(DialogWidth, HeightFor(DialogWidth));
        }

        protected override Size ArrangeOverride(Size size)
        {
            _input?.Arrange(new Rect(Padding_, InputTop, TextWidth, InputHeight));
            double buttonsTop = size.Height - Padding_ - _cancel.Height;
            _cancel.Arrange(new Rect(size.Width - Padding_ - _cancel.Width, buttonsTop, _cancel.Width, _cancel.Height));
            _confirm.Arrange(new Rect(size.Width - Padding_ - _cancel.Width - 8 - _confirm.Width, buttonsTop, _confirm.Width, _confirm.Height));
            // Its own button, at the other end of the row.
            _extra?.Arrange(new Rect(Padding_, buttonsTop, _extra.Width, _extra.Height));
            return size;
        }

        public override void Render(DrawingContext g)
        {
            g.FillRectangle(Draw.Brush(Palette.Background), new Rect(Bounds.Size));
            // One line sits centred in its row, as before; more start at its top.
            Draw.Wrapped(g, _title, Draw.Font(semibold: true), 16, Palette.Text, new Rect(Padding_, Padding_, TextWidth, TitleHeight));
            var message = Draw.Format(_message, Draw.Font(), 13, Palette.SubtleText);
            message.MaxTextWidth = TextWidth;
            g.DrawText(message, new Point(Padding_, Padding_ + TitleHeight + 8));
        }
    }
}
