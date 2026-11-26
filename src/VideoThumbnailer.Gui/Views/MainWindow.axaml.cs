using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using VideoThumbnailer.Core.Settings;
using VideoThumbnailer.Gui.ViewModels;

namespace VideoThumbnailer.Gui.Views;

public partial class MainWindow : Window, IUiServices
{
    public MainWindow()
    {
        InitializeComponent();
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(DragDrop.DropEvent, OnDrop);
        Opened += async (_, _) => { if (DataContext is MainViewModel vm) await vm.OnOpenedAsync(); };
        Closing += (_, _) => (DataContext as MainViewModel)?.OnClosing();
    }

    private MainViewModel? Vm => DataContext as MainViewModel;

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = e.DataTransfer.TryGetFiles() is { Length: > 0 } ? DragDropEffects.Copy : DragDropEffects.None;
    }

    private void OnDrop(object? sender, DragEventArgs e)
    {
        var paths = e.DataTransfer.TryGetFiles()?.Select(f => f.TryGetLocalPath()).OfType<string>().ToList();
        if (paths is { Count: > 0 }) Vm?.AddPaths(paths);
    }

    private void OnQueueSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (Vm is null) return;
        foreach (var r in e.RemovedItems.OfType<JobViewModel>()) Vm.SelectedJobs.Remove(r);
        foreach (var a in e.AddedItems.OfType<JobViewModel>()) Vm.SelectedJobs.Add(a);
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
            Title = "Add videos",
            AllowMultiple = true,
            FileTypeFilter =
            [
                new FilePickerFileType("Videos") { Patterns = Core.Input.VideoScanner.DefaultExtensions.Select(e => "*" + e).ToList() },
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
        var type = new FilePickerFileType("Video-Thumbnailer preset") { Patterns = ["*" + PresetStore.FileExtension, "*.json"] };
        if (save)
        {
            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Export preset",
                SuggestedFileName = suggestedName,
                FileTypeChoices = [type],
            });
            return file?.TryGetLocalPath();
        }
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Import preset",
            FileTypeFilter = [type, FilePickerFileTypes.All],
        });
        return files.Count > 0 ? files[0].TryGetLocalPath() : null;
    }

    public async Task<string?> PromptTextAsync(string title, string message, string initial)
    {
        var r = await DialogWindow.ShowAsync(this, title, message, "OK", "Cancel", initial);
        return r.Ok ? r.Text : null;
    }

    public async Task<bool> ConfirmAsync(string title, string message, string yes = "Yes", string no = "No") =>
        (await DialogWindow.ShowAsync(this, title, message, yes, no, null)).Ok;

    public async Task ShowMessageAsync(string title, string message) =>
        await DialogWindow.ShowAsync(this, title, message, "OK", null, null);

    public void OpenPath(string path)
    {
        try
        {
            if (File.Exists(path) || Directory.Exists(path))
                _ = Launcher.LaunchFileInfoAsync(new FileInfo(path));
        }
        catch (Exception ex) { Debug.WriteLine(ex); }
    }
}
