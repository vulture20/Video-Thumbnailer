using System.Text.RegularExpressions;
using VideoThumbnailer.Core.Input;
using VideoThumbnailer.Core.Rendering;
using VideoThumbnailer.Core.Settings;

namespace VideoThumbnailer.Core.Output;

public static partial class OutputPathResolver
{
    [GeneratedRegex(@"\{(?<key>[a-zA-Z]+)\}")]
    private static partial Regex Placeholder();

    /// <summary>
    /// Folder for sheets of remote videos (URLs) when no output folder is set: there is no "next to the video".
    /// The GUI uses the user's pictures folder, the command line sets the current directory.
    /// </summary>
    public static string RemoteFallbackFolder { get; set; } = DefaultRemoteFallbackFolder();

    private static string DefaultRemoteFallbackFolder()
    {
        var pictures = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
        if (!string.IsNullOrEmpty(pictures)) return pictures;
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return string.IsNullOrEmpty(home) ? Environment.CurrentDirectory : home;
    }

    /// <summary>Directory where the sheet for <paramref name="videoPath"/> is written.</summary>
    public static string ResolveDirectory(OutputSettings o, string videoPath, string? inputRoot)
    {
        // A URL has no folder of its own: always the output folder, or the fallback when none is set.
        if (VideoSource.IsUrl(videoPath))
            return string.IsNullOrWhiteSpace(o.Folder) ? RemoteFallbackFolder : Path.GetFullPath(o.Folder);
        var videoDir = Path.GetDirectoryName(Path.GetFullPath(videoPath)) ?? ".";
        switch (o.Location)
        {
            case OutputLocation.NextToVideo:
                return videoDir;
            case OutputLocation.SubfolderNextToVideo:
                return Path.Combine(videoDir, string.IsNullOrWhiteSpace(o.SubfolderName) ? "thumbnails" : o.SubfolderName);
            default:
                var folder = string.IsNullOrWhiteSpace(o.Folder) ? videoDir : Path.GetFullPath(o.Folder);
                if (o.KeepFolderStructure && !string.IsNullOrEmpty(inputRoot))
                {
                    var rel = Path.GetRelativePath(Path.GetFullPath(inputRoot), videoDir);
                    if (!rel.StartsWith("..", StringComparison.Ordinal) && !Path.IsPathRooted(rel) && rel != ".")
                        folder = Path.Combine(folder, rel);
                }
                return folder;
        }
    }

    public static string ExpandPattern(string pattern, string videoPath, SheetSettings s, DateTime? now = null)
    {
        var t = now ?? DateTime.Now;
        var result = Placeholder().Replace(pattern, m => m.Groups["key"].Value.ToLowerInvariant() switch
        {
            "name" => VideoSource.NameWithoutExtension(videoPath),
            "ext" => VideoSource.Extension(videoPath),
            "preset" => s.Name,
            "cols" => s.Columns.ToString(),
            "rows" => s.Rows.ToString(),
            "date" => t.ToString("yyyy-MM-dd"),
            "time" => t.ToString("HHmmss"),
            _ => m.Value,
        });
        foreach (var c in Path.GetInvalidFileNameChars().Append('/').Append('\\'))
            result = result.Replace(c, '_');
        return string.IsNullOrWhiteSpace(result) ? VideoSource.NameWithoutExtension(videoPath) : result;
    }

    /// <summary>Marker in the name of animated sheets ("movie.sheet.mp4"), so they never replace a video and are skipped when folders are scanned.</summary>
    public const string AnimatedMarker = ".sheet";

    /// <summary>Full path of the sheet file (before overwrite handling).</summary>
    public static string ResolveSheetPath(SheetSettings s, string videoPath, string? inputRoot)
    {
        var name = ExpandPattern(s.Output.FileNamePattern, videoPath, s);
        if (s.Animation.Enabled) name += AnimatedMarker;
        return Path.Combine(ResolveDirectory(s.Output, videoPath, inputRoot), name + SheetRenderer.Extension(s));
    }

    /// <summary>Returns a free variant "name (2).ext" if the file exists.</summary>
    public static string MakeUnique(string path)
    {
        if (!File.Exists(path)) return path;
        var dir = Path.GetDirectoryName(path) ?? ".";
        var name = Path.GetFileNameWithoutExtension(path);
        var ext = Path.GetExtension(path);
        for (var i = 2; ; i++)
        {
            var candidate = Path.Combine(dir, $"{name} ({i}){ext}");
            if (!File.Exists(candidate)) return candidate;
        }
    }
}
