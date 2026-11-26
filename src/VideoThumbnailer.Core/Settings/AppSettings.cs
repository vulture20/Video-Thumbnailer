using System.Text.Json;

namespace VideoThumbnailer.Core.Settings;

/// <summary>Persistent application state shared by GUI and CLI (settings.json in the config folder).</summary>
public sealed class AppSettings
{
    /// <summary>Settings last used in the GUI. The CLI uses them when no preset is given.</summary>
    public SheetSettings? LastSettings { get; set; }
    public string? LastPreset { get; set; }
    public bool Recursive { get; set; } = true;
    public int? MaxParallelProcesses { get; set; }
    public int? MaxParallelVideos { get; set; }
    /// <summary>Folder that contains ffmpeg and ffprobe; overrides the managed download and PATH.</summary>
    public string? FfmpegDirectory { get; set; }
    /// <summary>True once the user answered the first-start question about downloading ffmpeg.</summary>
    public bool FfmpegDownloadAsked { get; set; }
    public bool CheckFfmpegUpdates { get; set; } = true;
    public DateTimeOffset? LastFfmpegUpdateCheck { get; set; }
    public string? LastInputFolder { get; set; }

    public static AppSettings Load(string? path = null)
    {
        path ??= AppPaths.SettingsFile;
        try
        {
            if (File.Exists(path))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path), SettingsJson.Options) ?? new AppSettings();
        }
        catch { /* corrupt settings: start fresh */ }
        return new AppSettings();
    }

    public void Save(string? path = null)
    {
        path ??= AppPaths.SettingsFile;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(this, SettingsJson.Options));
        File.Move(tmp, path, overwrite: true);
    }
}
