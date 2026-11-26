namespace VideoThumbnailer.Gui.ViewModels;

/// <summary>Things the view models need from the window (dialogs, pickers, shell).</summary>
public interface IUiServices
{
    Task<IReadOnlyList<string>> PickVideoFilesAsync();
    Task<string?> PickFolderAsync(string title);
    Task<string?> PickPresetFileAsync(bool save, string? suggestedName = null);
    Task<string?> PromptTextAsync(string title, string message, string initial);
    /// <summary>Yes/No question; null button texts use the translated "Yes"/"No".</summary>
    Task<bool> ConfirmAsync(string title, string message, string? yes = null, string? no = null);
    Task ShowMessageAsync(string title, string message);
    /// <summary>Font selection for the sheet: "" = built-in font, a family name, or null when cancelled.</summary>
    Task<string?> PickFontAsync(string? current);
    /// <summary>Dialog for the capture time of one tile; changes reach the preview through the editor while it is open.</summary>
    Task<TileTimeResult> EditTileTimeAsync(TileTimeViewModel editor);
    /// <summary>Opens a file or folder, or a web address in the browser.</summary>
    void OpenPath(string path);
}
