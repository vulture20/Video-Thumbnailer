using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using VideoThumbnailer.Gui.Controls;
using VideoThumbnailer.Gui.Localization;
using VideoThumbnailer.Gui.ViewModels;

namespace VideoThumbnailer.Gui.Views;

/// <summary>
/// Grid editor (#36): columns, rows, name; the raster to draw on; "Fill", "Clear"; "Save as grid" stores a
/// ".vtgrid" and applies it, "Apply" only applies it to the current settings.
/// </summary>
public sealed class GridEditorWindow : Window
{
    public GridEditorViewModel Editor { get; }

    public GridEditorWindow(GridEditorViewModel editor)
    {
        Editor = editor;
        DataContext = editor;
        Title = Loc.T("GridEditorTitle");
        Width = 760;
        Height = 620;
        MinWidth = 520;
        MinHeight = 440;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        TextBlock Label(string key) { var t = new TextBlock { Text = Loc.T(key), VerticalAlignment = VerticalAlignment.Center }; t.Classes.Add("label"); return t; }
        var cols = new NumericUpDown { Name = "GridEditorColumns", Minimum = 1, Maximum = GridEditorViewModel.MaxColumns, Increment = 1, FormatString = "0", Width = 120,
                                       [!NumericUpDown.ValueProperty] = new Binding(nameof(GridEditorViewModel.Columns)) { Mode = BindingMode.TwoWay } };
        var rows = new NumericUpDown { Name = "GridEditorRows", Minimum = 1, Maximum = GridEditorViewModel.MaxRows, Increment = 1, FormatString = "0", Width = 120,
                                       [!NumericUpDown.ValueProperty] = new Binding(nameof(GridEditorViewModel.Rows)) { Mode = BindingMode.TwoWay } };
        var name = new TextBox { Name = "GridEditorName", [!TextBox.TextProperty] = new Binding(nameof(GridEditorViewModel.Name)) { Mode = BindingMode.TwoWay } };
        var description = new TextBox { Watermark = Loc.T("GridEditorDescription"),
                                        [!TextBox.TextProperty] = new Binding(nameof(GridEditorViewModel.Description)) { Mode = BindingMode.TwoWay } };

        var top = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,Auto,Auto,Auto,Auto,*"), RowDefinitions = new RowDefinitions("Auto,Auto"),
                             ColumnSpacing = 8, RowSpacing = 6, Margin = new Thickness(16, 14, 16, 6) };
        void Put(Control c, int col, int row = 0, int span = 1) { Grid.SetColumn(c, col); Grid.SetRow(c, row); Grid.SetColumnSpan(c, span); top.Children.Add(c); }
        Put(Label("Columns"), 0);
        Put(cols, 1);
        Put(Label("Rows"), 2);
        Put(rows, 3);
        Put(Label("GridEditorName"), 4);
        Put(name, 5);
        Put(description, 4, 1, 2);
        var fill = new Button { Content = Loc.T("GridEditorFill"), Command = editor.FillCommand };
        ToolTip.SetTip(fill, Loc.T("GridEditorFillTip"));
        var clear = new Button { Content = Loc.T("GridEditorClear"), Command = editor.ClearCommand };
        var tools = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        tools.Children.Add(fill);
        tools.Children.Add(clear);
        Put(tools, 0, 1, 4);
        DockPanel.SetDock(top, Dock.Top);

        var hint = new TextBlock { Text = Loc.T("GridEditorHint"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(16, 0, 16, 4) };
        hint.Classes.Add("hint");
        DockPanel.SetDock(hint, Dock.Top);

        var info = new TextBlock { VerticalAlignment = VerticalAlignment.Center, [!TextBlock.TextProperty] = new Binding(nameof(GridEditorViewModel.InfoText)) };
        info.Classes.Add("caption");
        var delete = new Button { Name = "GridEditorDelete", Content = Loc.T("GridEditorDelete"), Command = editor.DeleteOwnCommand };
        ToolTip.SetTip(delete, Loc.T("GridEditorDeleteTip"));
        var save = new Button { Name = "GridEditorSave", Content = Loc.T("GridEditorSave"), [!IsEnabledProperty] = new Binding(nameof(GridEditorViewModel.IsValid)) };
        ToolTip.SetTip(save, Loc.T("GridEditorSaveTip"));
        var apply = new Button { Name = "GridEditorApply", Content = Loc.T("GridEditorApply"), IsDefault = true, [!IsEnabledProperty] = new Binding(nameof(GridEditorViewModel.IsValid)) };
        apply.Classes.Add("accent");
        ToolTip.SetTip(apply, Loc.T("GridEditorApplyTip"));
        var cancel = new Button { Content = Loc.T("Cancel"), IsCancel = true };
        var error = new TextBlock { Foreground = Brushes.IndianRed, TextWrapping = TextWrapping.Wrap, IsVisible = false };

        save.Click += (_, _) =>
        {
            if (editor.SaveOwn() is { } problem) { error.Text = problem; error.IsVisible = true; return; }
            editor.SaveAsOwn = true;
            Close(true);
        };
        apply.Click += (_, _) => Close(true);
        cancel.Click += (_, _) => Close(false);

        var bottom = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto,Auto,Auto"), RowDefinitions = new RowDefinitions("Auto,Auto"),
                                ColumnSpacing = 8, Margin = new Thickness(16, 6, 16, 14) };
        void PutB(Control c, int col, int row = 0) { Grid.SetColumn(c, col); Grid.SetRow(c, row); bottom.Children.Add(c); }
        PutB(info, 0);
        PutB(delete, 1);
        PutB(save, 2);
        PutB(apply, 3);
        PutB(cancel, 4);
        Grid.SetColumnSpan(error, 5);
        PutB(error, 0, 1);
        DockPanel.SetDock(bottom, Dock.Bottom);

        var canvas = new GridEditorCanvas(editor) { Name = "GridEditorCanvas", Margin = new Thickness(16, 8) };

        var root = new DockPanel();
        root.Children.Add(top);
        root.Children.Add(hint);
        root.Children.Add(bottom);
        root.Children.Add(canvas);
        Content = root;
    }

    /// <summary>True = apply the grid (and it may have been saved as own grid, see <see cref="GridEditorViewModel.SaveAsOwn"/>).</summary>
    public static async Task<bool> ShowAsync(Window owner, GridEditorViewModel editor) =>
        await new GridEditorWindow(editor) { Icon = owner.Icon }.ShowDialog<bool?>(owner) == true;
}
