using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using VideoThumbnailer.Gui.Localization;
using VideoThumbnailer.Gui.ViewModels;

namespace VideoThumbnailer.Gui.Views;

/// <summary>
/// Dialog for the capture time of one tile: slider over the whole video, a time field, steps of ±10 s / ±1 s /
/// ±1 frame and, for animated sheets, the clip length. Changes go to the live preview at once (see
/// <see cref="TileTimeViewModel.Changed"/>); the result says whether to keep them, go back to automatic or undo.
/// </summary>
public sealed class TileTimeWindow : Window
{
    public TileTimeViewModel Editor { get; }

    public TileTimeWindow(TileTimeViewModel editor)
    {
        Editor = editor;
        DataContext = editor;
        Title = editor.Title;
        Width = 520;
        SizeToContent = SizeToContent.Height;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var info = new TextBlock { Text = editor.Info, TextWrapping = TextWrapping.Wrap };
        info.Classes.Add("caption");

        var slider = new Slider
        {
            Name = "TileTimeSlider", Minimum = 0, Maximum = Math.Max(0.001, editor.Duration),
            SmallChange = 1, LargeChange = 10,
            [!RangeBase.ValueProperty] = new Binding(nameof(TileTimeViewModel.Time)) { Mode = BindingMode.TwoWay },
        };

        var timeBox = new TextBox
        {
            Name = "TileTimeText", HorizontalContentAlignment = HorizontalAlignment.Center, MinWidth = 130,
            FontFamily = new FontFamily("Cascadia Mono,Consolas,DejaVu Sans Mono,monospace"),
            [!TextBox.TextProperty] = new Binding(nameof(TileTimeViewModel.TimeText)) { Mode = BindingMode.TwoWay },
        };
        var invalid = new TextBlock { Text = Loc.T("TileTimeInvalid"), Foreground = Brushes.IndianRed, HorizontalAlignment = HorizontalAlignment.Center,
                                      [!IsVisibleProperty] = new Binding(nameof(TileTimeViewModel.TimeInvalid)) };

        Button StepButton(string text, string amount, string? tip = null)
        {
            var b = new Button
            {
                Content = text, Command = editor.StepCommand, CommandParameter = amount, MinWidth = 46,
                HorizontalContentAlignment = HorizontalAlignment.Center, Padding = new Thickness(6, 4),
            };
            if (tip is not null) ToolTip.SetTip(b, tip);
            return b;
        }
        var steps = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,Auto,Auto,*,Auto,Auto,Auto"), ColumnSpacing = 4 };
        void Put(Control c, int col) { Grid.SetColumn(c, col); steps.Children.Add(c); }
        Put(StepButton("−10 s", "-10"), 0);
        Put(StepButton("−1 s", "-1"), 1);
        Put(StepButton("◀", "-frame", Loc.T("TileTimeFrameBack")), 2);
        Put(timeBox, 3);
        Put(StepButton("▶", "frame", Loc.T("TileTimeFrameForward")), 4);
        Put(StepButton("+1 s", "1"), 5);
        Put(StepButton("+10 s", "10"), 6);

        var timeLabel = new TextBlock { Text = Loc.T("TileTimeTime") };
        timeLabel.Classes.Add("label");

        var panel = new StackPanel { Margin = new Thickness(18), Spacing = 10 };
        panel.Children.Add(info);
        panel.Children.Add(timeLabel);
        panel.Children.Add(slider);
        panel.Children.Add(steps);
        panel.Children.Add(invalid);

        if (editor.IsAnimated)
        {
            var clipLabel = new TextBlock { Text = editor.ClipLabel, VerticalAlignment = VerticalAlignment.Center };
            clipLabel.Classes.Add("label");
            var clipBox = new TextBox { Name = "TileClipText", Width = 90, [!TextBox.TextProperty] = new Binding(nameof(TileTimeViewModel.ClipText)) { Mode = BindingMode.TwoWay } };
            var clipRow = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 8 };
            Grid.SetColumn(clipBox, 1);
            clipRow.Children.Add(clipLabel);
            clipRow.Children.Add(clipBox);
            panel.Children.Add(clipRow);
        }

        var hint = new TextBlock { Text = Loc.T("TileTimeHint"), TextWrapping = TextWrapping.Wrap };
        hint.Classes.Add("hint");
        panel.Children.Add(hint);

        var auto = new Button { Name = "TileTimeAuto", Content = Loc.T("TileTimeAuto"), MinWidth = 90, HorizontalContentAlignment = HorizontalAlignment.Center };
        ToolTip.SetTip(auto, Loc.T("TileTimeAutoTip"));
        auto.Click += (_, _) => Close(TileTimeResult.Automatic);
        var ok = new Button
        {
            Name = "TileTimeOk", Content = Loc.T("Ok"), IsDefault = true, MinWidth = 90, HorizontalContentAlignment = HorizontalAlignment.Center,
            [!IsEnabledProperty] = new Binding(nameof(TileTimeViewModel.IsValid)),
        };
        ok.Classes.Add("accent");
        ok.Click += (_, _) => Close(TileTimeResult.Ok);
        var cancel = new Button { Content = Loc.T("Cancel"), IsCancel = true, MinWidth = 90, HorizontalContentAlignment = HorizontalAlignment.Center };
        cancel.Click += (_, _) => Close(TileTimeResult.Cancel);

        var buttons = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,Auto"), ColumnSpacing = 8, Margin = new Thickness(0, 4, 0, 0) };
        Grid.SetColumn(ok, 2);
        Grid.SetColumn(cancel, 3);
        buttons.Children.Add(auto);
        buttons.Children.Add(ok);
        buttons.Children.Add(cancel);
        panel.Children.Add(buttons);

        Content = panel;
        Opened += (_, _) => slider.Focus();
    }

    public static async Task<TileTimeResult> ShowAsync(Window owner, TileTimeViewModel editor) =>
        await new TileTimeWindow(editor) { Icon = owner.Icon }.ShowDialog<TileTimeResult?>(owner) ?? TileTimeResult.Cancel;
}
