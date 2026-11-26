namespace VideoThumbnailer.Gui.ViewModels;

/// <summary>Things the view models need from the window (dialogs, pickers, shell).</summary>
public interface IUiServices
{
    Task<IReadOnlyList<string>> PickVideoFilesAsync();
    Task<string?> PickFolderAsync(string title);
    Task<string?> PickPresetFileAsync(bool save, string? suggestedName = null);
    Task<string?> PromptTextAsync(string title, string message, string initial);
    Task<bool> ConfirmAsync(string title, string message, string yes = "Yes", string no = "No");
    Task ShowMessageAsync(string title, string message);
    void OpenPath(string path);
}
