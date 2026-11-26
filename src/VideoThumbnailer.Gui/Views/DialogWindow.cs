using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace VideoThumbnailer.Gui.Views;

/// <summary>Small modal dialog: message, optional text input, one or two buttons.</summary>
public sealed class DialogWindow : Window
{
    public sealed record Result(bool Ok, string? Text);

    private DialogWindow(string title, string message, string ok, string? cancel, string? input)
    {
        Title = title;
        Width = 460;
        SizeToContent = SizeToContent.Height;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var panel = new StackPanel { Margin = new Thickness(18), Spacing = 12 };
        panel.Children.Add(new SelectableTextBlock { Text = message, TextWrapping = Avalonia.Media.TextWrapping.Wrap });

        TextBox? box = null;
        if (input is not null)
        {
            box = new TextBox { Text = input };
            panel.Children.Add(box);
            Opened += (_, _) => { box.Focus(); box.SelectAll(); };
        }

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 8 };
        var okButton = new Button { Content = ok, IsDefault = true, MinWidth = 90, HorizontalContentAlignment = HorizontalAlignment.Center };
        okButton.Classes.Add("accent");
        okButton.Click += (_, _) => Close(new Result(true, box?.Text));
        buttons.Children.Add(okButton);
        if (cancel is not null)
        {
            var cancelButton = new Button { Content = cancel, IsCancel = true, MinWidth = 90, HorizontalContentAlignment = HorizontalAlignment.Center };
            cancelButton.Click += (_, _) => Close(new Result(false, null));
            buttons.Children.Add(cancelButton);
        }
        panel.Children.Add(buttons);
        Content = panel;
    }

    public static async Task<Result> ShowAsync(Window owner, string title, string message, string ok, string? cancel, string? input)
    {
        var dlg = new DialogWindow(title, message, ok, cancel, input);
        return await dlg.ShowDialog<Result?>(owner) ?? new Result(false, null);
    }
}
