using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using VideoThumbnailer.Core.Output;
using VideoThumbnailer.Gui.Localization;
using VideoThumbnailer.Gui.ViewModels;

namespace VideoThumbnailer.Gui.Views;

/// <summary>
/// Sheet viewer (#39): the sheet fills the window; the tile under the mouse is outlined, a click starts the video at
/// its time in the chosen player. "To trash…" moves the video (optionally with the sheet files) to the trash.
/// </summary>
public sealed class ViewerWindow : Window
{
    public ViewerViewModel Viewer { get; }
    private readonly Image _image;
    private readonly Border _highlight;

    public ViewerWindow(ViewerViewModel viewer)
    {
        Viewer = viewer;
        DataContext = viewer;
        Title = viewer.Title;
        Width = 1100;
        Height = 780;
        MinWidth = 480;
        MinHeight = 320;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        // --- top bar: video, player, trash
        var video = new TextBlock { VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, FontWeight = FontWeight.SemiBold,
                                    [!TextBlock.TextProperty] = new Binding(nameof(ViewerViewModel.VideoText)) };
        ToolTip.SetTip(video, viewer.SidecarPath);
        var playerLabel = new TextBlock { Text = Loc.T("ViewerPlayer"), VerticalAlignment = VerticalAlignment.Center };
        playerLabel.Classes.Add("caption");
        var player = new ComboBox
        {
            Name = "ViewerPlayer", ItemsSource = ViewerViewModel.Players, MinWidth = 150,
            DisplayMemberBinding = new Binding(nameof(Choice.Label)),
            SelectedValueBinding = new Binding(nameof(Choice.Value)),
            [!SelectingItemsControl.SelectedValueProperty] = new Binding(nameof(ViewerViewModel.Player)) { Mode = BindingMode.TwoWay },
        };
        ToolTip.SetTip(player, Loc.T("ViewerPlayerTip"));
        var command = new TextBox
        {
            Name = "ViewerPlayerCommand", MinWidth = 220, Watermark = Loc.T("ViewerPlayerCommandWatermark"),
            [!TextBox.TextProperty] = new Binding(nameof(ViewerViewModel.PlayerCommand)) { Mode = BindingMode.TwoWay },
            [!IsVisibleProperty] = new Binding(nameof(ViewerViewModel.IsCustomPlayer)),
        };
        ToolTip.SetTip(command, Loc.T("ViewerPlayerCommandTip"));
        var trash = new Button { Name = "ViewerTrash", Content = Loc.T("ViewerTrash"), [!IsEnabledProperty] = new Binding(nameof(ViewerViewModel.CanDelete)) };
        ToolTip.SetTip(trash, Loc.T("ViewerTrashTip", Input.Shortcuts.Display("RemoveSelected", Loc.Instance.Code)));
        trash.Click += async (_, _) => await DeleteAsync();

        var top = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto,Auto,Auto"), ColumnSpacing = 8, Margin = new Thickness(14, 10) };
        void Put(Grid g, Control c, int col) { Grid.SetColumn(c, col); g.Children.Add(c); }
        Put(top, video, 0);
        Put(top, playerLabel, 1);
        Put(top, player, 2);
        Put(top, command, 3);
        Put(top, trash, 4);
        DockPanel.SetDock(top, Dock.Top);

        // --- bottom: status and tile under the mouse
        var status = new TextBlock { TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center,
                                     [!TextBlock.TextProperty] = new Binding(nameof(ViewerViewModel.StatusText)) };
        status.Classes.Add("caption");
        var hover = new TextBlock { VerticalAlignment = VerticalAlignment.Center, FontFamily = new FontFamily("Cascadia Mono,Consolas,DejaVu Sans Mono,monospace"),
                                    [!TextBlock.TextProperty] = new Binding(nameof(ViewerViewModel.HoverText)) };
        hover.Classes.Add("caption");
        var bottom = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 12, Margin = new Thickness(14, 6, 14, 10) };
        Put(bottom, status, 0);
        Put(bottom, hover, 1);
        DockPanel.SetDock(bottom, Dock.Bottom);

        // --- the sheet with a frame around the tile under the mouse
        _image = new Image { Name = "ViewerImage", Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly, Cursor = new Cursor(StandardCursorType.Hand),
                             [!Image.SourceProperty] = new Binding(nameof(ViewerViewModel.Image)) };
        _highlight = new Border { BorderBrush = new SolidColorBrush(Color.FromRgb(0xF5, 0xA6, 0x23)), BorderThickness = new Thickness(3),
                                  IsHitTestVisible = false, IsVisible = false };
        var overlay = new Canvas { IsHitTestVisible = false };
        overlay.Children.Add(_highlight);
        var stack = new Panel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(14, 0) };
        stack.Children.Add(_image);
        stack.Children.Add(overlay);

        _image.PointerMoved += (_, e) => UpdateHover(e.GetPosition(_image));
        _image.PointerExited += (_, _) => { _highlight.IsVisible = false; Viewer.Hover(null); };
        _image.PointerPressed += (_, e) =>
        {
            if (!e.GetCurrentPoint(_image).Properties.IsLeftButtonPressed) return;
            if (TileAt(e.GetPosition(_image)) is { } tile) { e.Handled = true; Viewer.Play(tile); }
        };

        var root = new DockPanel();
        root.Bind(Panel.BackgroundProperty, root.GetResourceObservable("TableBrush"));
        root.Children.Add(top);
        root.Children.Add(bottom);
        root.Children.Add(stack);
        Content = root;

        KeyBindings.Add(new KeyBinding { Gesture = new KeyGesture(Key.Escape), Command = new Relay(Close) });
        KeyBindings.Add(new KeyBinding { Gesture = new KeyGesture(Key.Delete), Command = new Relay(() => _ = DeleteAsync()) });
    }

    private SidecarTile? TileAt(Point p) =>
        _image.Bounds.Width <= 0 || _image.Bounds.Height <= 0 ? null : Viewer.TileAt(p.X / _image.Bounds.Width, p.Y / _image.Bounds.Height);

    private void UpdateHover(Point p)
    {
        var tile = TileAt(p);
        Viewer.Hover(tile);
        if (tile is null || Viewer.Sidecar.Sheet.Width <= 0) { _highlight.IsVisible = false; return; }
        var scale = _image.Bounds.Width / Viewer.Sidecar.Sheet.Width;
        Canvas.SetLeft(_highlight, tile.X * scale);
        Canvas.SetTop(_highlight, tile.Y * scale);
        _highlight.Width = Math.Max(4, tile.Width * scale);
        _highlight.Height = Math.Max(4, tile.Height * scale);
        _highlight.IsVisible = true;
    }

    /// <summary>Asks, then moves the video (and, if ticked, the sheet files) to the trash.</summary>
    private async Task DeleteAsync()
    {
        if (!Viewer.CanDelete || Viewer.VideoPath is not { } video) return;
        var (ok, withSheet) = await TrashDialog.ShowAsync(this, Path.GetFileName(video), Viewer.SheetFiles().Count);
        if (ok) Viewer.Delete(withSheet);
    }

    /// <summary>Opens a viewer next to the main window. Errors (no or broken sidecar) are shown by the caller.</summary>
    public static void Show(Window owner, ViewerViewModel viewer) => new ViewerWindow(viewer) { Icon = owner.Icon }.Show(owner);

    private sealed class Relay(Action action) : System.Windows.Input.ICommand
    {
        public event EventHandler? CanExecuteChanged { add { } remove { } }
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => action();
    }

    /// <summary>"Move to trash?" with a check box for the sheet files.</summary>
    private sealed class TrashDialog : Window
    {
        private TrashDialog(string fileName, int sheetFiles)
        {
            Title = Loc.T("ViewerTrash");
            Width = 460;
            SizeToContent = SizeToContent.Height;
            CanResize = false;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            var text = new TextBlock { Text = Loc.T("ViewerTrashQuestion", fileName), TextWrapping = TextWrapping.Wrap };
            var check = new CheckBox { Name = "ViewerTrashWithSheet", Content = Loc.T("ViewerTrashWithSheet", sheetFiles), IsChecked = false, IsEnabled = sheetFiles > 0 };
            var hint = new TextBlock { Text = Loc.T("ViewerTrashHint"), TextWrapping = TextWrapping.Wrap };
            hint.Classes.Add("hint");
            var yes = new Button { Content = Loc.T("ViewerTrashYes"), MinWidth = 110, HorizontalContentAlignment = HorizontalAlignment.Center };
            yes.Classes.Add("accent");
            yes.Click += (_, _) => Close((true, check.IsChecked == true));
            var no = new Button { Content = Loc.T("Cancel"), IsCancel = true, IsDefault = true, MinWidth = 90, HorizontalContentAlignment = HorizontalAlignment.Center };
            no.Click += (_, _) => Close((false, false));
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right };
            buttons.Children.Add(yes);
            buttons.Children.Add(no);
            var panel = new StackPanel { Margin = new Thickness(18), Spacing = 12 };
            panel.Children.Add(text);
            panel.Children.Add(check);
            panel.Children.Add(hint);
            panel.Children.Add(buttons);
            Content = panel;
        }

        public static async Task<(bool Ok, bool WithSheet)> ShowAsync(Window owner, string fileName, int sheetFiles) =>
            await new TrashDialog(fileName, sheetFiles) { Icon = owner.Icon }.ShowDialog<(bool, bool)?>(owner) ?? (false, false);
    }
}
