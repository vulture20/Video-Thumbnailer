using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using VideoThumbnailer.Core.Settings;
using VideoThumbnailer.Gui.Localization;
using VideoThumbnailer.Gui.ViewModels;

namespace VideoThumbnailer.Gui.Views;

public partial class MainWindow : Window, IUiServices
{
    public MainWindow()
    {
        InitializeComponent();
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(DragDrop.DropEvent, OnDrop);
        PropertyChanged += (_, e) =>
        {
            if (e.Property == ClientSizeProperty) (DataContext as MainViewModel)?.OnWindowWidthChanged(ClientSize.Width);
        };
        DataContextChanged += (_, _) => (DataContext as MainViewModel)?.OnWindowWidthChanged(ClientSize.Width);
        Activated += (_, _) => (DataContext as MainViewModel)?.OnWindowActivated();
        Opened += async (_, _) =>
        {
            ReportScreen();
            Screens.Changed += (_, _) => ReportScreen();
            if (DataContext is MainViewModel vm) await vm.OnOpenedAsync();
        };
        PositionChanged += (_, _) => ReportScreen();
        ScalingChanged += (_, _) => ReportScreen();
        Closing += (_, _) => (DataContext as MainViewModel)?.OnClosing();
    }

    private MainViewModel? Vm => DataContext as MainViewModel;

    /// <summary>Usable area (physical pixels, without task bar) of the screen the window is on, for "fit to screen".</summary>
    private void ReportScreen()
    {
        try
        {
            var screen = Screens.ScreenFromWindow(this) ?? Screens.Primary;
            if (screen is null) return;
            var area = screen.WorkingArea;
            if (area.Width >= 100 && area.Height >= 100) Vm?.OnScreenChanged(new Core.Layout.ScreenSize(area.Width, area.Height));
        }
        catch (Exception ex) { Debug.WriteLine(ex); }
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = e.DataTransfer.TryGetFiles() is { Length: > 0 } || DroppedUrlText(e) is not null ? DragDropEffects.Copy : DragDropEffects.None;
    }

    /// <summary>Text dragged from a browser's address bar or a link, if it is a web address.</summary>
    private static string? DroppedUrlText(DragEventArgs e)
    {
        var text = e.DataTransfer.TryGetText();
        return text is not null && text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Any(Core.Input.VideoSource.IsUrl) ? text : null;
    }

    private void OnDrop(object? sender, DragEventArgs e)
    {
        var paths = e.DataTransfer.TryGetFiles()?.Select(f => f.TryGetLocalPath()).OfType<string>().ToList();
        if (paths is { Count: > 0 }) Vm?.AddPaths(paths);
        else if (DroppedUrlText(e) is { } text) Vm?.AddUrls(text);
    }

    private void OnQueueSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (Vm is null) return;
        foreach (var r in e.RemovedItems.OfType<JobViewModel>()) Vm.SelectedJobs.Remove(r);
        foreach (var a in e.AddedItems.OfType<JobViewModel>()) Vm.SelectedJobs.Add(a);
        // The preview follows the most recently selected video.
        if (e.AddedItems.OfType<JobViewModel>().LastOrDefault() is { } latest) Vm.PreviewJob = latest;
        else if (Vm.SelectedJobs.Count > 0) Vm.PreviewJob = Vm.SelectedJobs[^1];
    }

    /// <summary>Click on a tile of the preview: choose its capture time.</summary>
    private async void OnPreviewPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (Vm is not { CanEditTiles: true } vm || sender is not Image img || img.Bounds.Width <= 0 || img.Bounds.Height <= 0) return;
        if (!e.GetCurrentPoint(img).Properties.IsLeftButtonPressed) return;
        var p = e.GetPosition(img);
        e.Handled = true;
        await vm.EditTileAtAsync(p.X / img.Bounds.Width, p.Y / img.Bounds.Height);
    }

    private void OnQueueDoubleTapped(object? sender, TappedEventArgs e)
    {
        if ((e.Source as Control)?.DataContext is JobViewModel job) Vm?.OpenOutputCommand.Execute(job);
    }

    // ------------------------------------------------------------------ IUiServices

    public async Task<IReadOnlyList<string>> PickVideoFilesAsync()
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = Loc.T("DlgAddVideos"),
            AllowMultiple = true,
            FileTypeFilter =
            [
                new FilePickerFileType(Loc.T("DlgVideos")) { Patterns = Core.Input.VideoScanner.DefaultExtensions.Select(e => "*" + e).ToList() },
                FilePickerFileTypes.All,
            ],
        });
        return files.Select(f => f.TryGetLocalPath()).OfType<string>().ToList();
    }

    public async Task<string?> PickFolderAsync(string title)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = title, AllowMultiple = false });
        return folders.Count > 0 ? folders[0].TryGetLocalPath() : null;
    }

    public async Task<string?> PickPresetFileAsync(bool save, string? suggestedName = null)
    {
        var type = new FilePickerFileType(Loc.T("DlgPresetFileType")) { Patterns = ["*" + PresetStore.FileExtension, "*.json"] };
        if (save)
        {
            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = Loc.T("DlgExportPreset"),
                SuggestedFileName = suggestedName,
                FileTypeChoices = [type],
            });
            return file?.TryGetLocalPath();
        }
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = Loc.T("DlgImportPreset"),
            FileTypeFilter = [type, FilePickerFileTypes.All],
        });
        return files.Count > 0 ? files[0].TryGetLocalPath() : null;
    }

    public async Task<string?> PickImageFileAsync(string? current)
    {
        IStorageFolder? start = null;
        try
        {
            var dir = string.IsNullOrWhiteSpace(current) ? null : Path.GetDirectoryName(current);
            if (dir is not null && Directory.Exists(dir)) start = await StorageProvider.TryGetFolderFromPathAsync(dir);
        }
        catch { /* start in the default folder */ }
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = Loc.T("BackgroundImagePick"),
            SuggestedStartLocation = start,
            FileTypeFilter =
            [
                new FilePickerFileType(Loc.T("DlgImages")) { Patterns = ["*.jpg", "*.jpeg", "*.png", "*.webp", "*.bmp", "*.gif"] },
                FilePickerFileTypes.All,
            ],
        });
        return files.Count > 0 ? files[0].TryGetLocalPath() : null;
    }

    public async Task<string?> PromptTextAsync(string title, string message, string initial)
    {
        var r = await DialogWindow.ShowAsync(this, title, message, Loc.T("Ok"), Loc.T("Cancel"), initial);
        return r.Ok ? r.Text : null;
    }

    public async Task<bool> ConfirmAsync(string title, string message, string? yes = null, string? no = null) =>
        (await DialogWindow.ShowAsync(this, title, message, yes ?? Loc.T("Yes"), no ?? Loc.T("No"), null)).Ok;

    public async Task ShowMessageAsync(string title, string message) =>
        await DialogWindow.ShowAsync(this, title, message, Loc.T("Ok"), null, null);

    public Task<string?> PickFontAsync(string? current) => FontPickerWindow.ShowAsync(this, current);

    public Task<TileTimeResult> EditTileTimeAsync(TileTimeViewModel editor) => TileTimeWindow.ShowAsync(this, editor);

    public void OpenPath(string path)
    {
        try
        {
            if (Core.Input.VideoSource.IsUrl(path))
                _ = Launcher.LaunchUriAsync(new Uri(path));
            else if (File.Exists(path) || Directory.Exists(path))
                _ = Launcher.LaunchFileInfoAsync(new FileInfo(path));
        }
        catch (Exception ex) { Debug.WriteLine(ex); }
    }
}
