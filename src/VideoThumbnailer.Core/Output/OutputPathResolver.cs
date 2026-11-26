using System.Text.RegularExpressions;
using VideoThumbnailer.Core.Rendering;
using VideoThumbnailer.Core.Settings;

namespace VideoThumbnailer.Core.Output;

public static partial class OutputPathResolver
{
    [GeneratedRegex(@"\{(?<key>[a-zA-Z]+)\}")]
    private static partial Regex Placeholder();

    /// <summary>Directory where the sheet for <paramref name="videoPath"/> is written.</summary>
    public static string ResolveDirectory(OutputSettings o, string videoPath, string? inputRoot)
    {
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
            "name" => Path.GetFileNameWithoutExtension(videoPath),
            "ext" => Path.GetExtension(videoPath).TrimStart('.'),
            "preset" => s.Name,
            "cols" => s.Columns.ToString(),
            "rows" => s.Rows.ToString(),
            "date" => t.ToString("yyyy-MM-dd"),
            "time" => t.ToString("HHmmss"),
            _ => m.Value,
        });
        foreach (var c in Path.GetInvalidFileNameChars().Append('/').Append('\\'))
            result = result.Replace(c, '_');
        return string.IsNullOrWhiteSpace(result) ? Path.GetFileNameWithoutExtension(videoPath) : result;
    }

    /// <summary>Full path of the sheet file (before overwrite handling).</summary>
    public static string ResolveSheetPath(SheetSettings s, string videoPath, string? inputRoot) =>
        Path.Combine(ResolveDirectory(s.Output, videoPath, inputRoot),
            ExpandPattern(s.Output.FileNamePattern, videoPath, s) + SheetRenderer.Extension(s.Output.Format));

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
