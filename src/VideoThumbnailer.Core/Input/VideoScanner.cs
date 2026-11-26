using System.Text.RegularExpressions;

namespace VideoThumbnailer.Core.Input;

/// <summary>A video file plus the folder it was found in (used for "keep folder structure").</summary>
public sealed record VideoInput(string Path, string? Root);

public static partial class VideoScanner
{
    /// <summary>Animated sheets written by this tool ("movie.sheet.mp4", "movie.sheet (2).webm").</summary>
    public static bool IsAnimatedSheet(string path) => AnimatedSheetName().IsMatch(System.IO.Path.GetFileNameWithoutExtension(path));

    [GeneratedRegex(@"\.sheet( \(\d+\))?$", RegexOptions.IgnoreCase)]
    private static partial Regex AnimatedSheetName();

    public static readonly IReadOnlySet<string> DefaultExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".m4v", ".mkv", ".avi", ".wmv", ".asf", ".mpg", ".mpeg", ".m2v", ".mov", ".qt", ".flv", ".f4v",
        ".webm", ".ogv", ".ogm", ".ts", ".m2ts", ".mts", ".vob", ".3gp", ".3g2", ".divx", ".xvid", ".rm", ".rmvb",
        ".dv", ".mxf", ".y4m", ".h264", ".h265", ".hevc", ".av1", ".ivf",
    };

    public static bool IsVideoFile(string path, IReadOnlySet<string>? extensions = null) =>
        (extensions ?? DefaultExtensions).Contains(System.IO.Path.GetExtension(path));

    /// <summary>
    /// Expands files and folders into a de-duplicated, sorted list of video files.
    /// Files given explicitly are always included, even with an unknown extension; folder scans skip animated sheets.
    /// </summary>
    public static IReadOnlyList<VideoInput> Scan(IEnumerable<string> paths, bool recursive, IReadOnlySet<string>? extensions = null)
    {
        var seen = new HashSet<string>(OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
            ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        var result = new List<VideoInput>();
        foreach (var raw in paths)
        {
            var p = System.IO.Path.GetFullPath(raw);
            if (File.Exists(p))
            {
                if (seen.Add(p)) result.Add(new VideoInput(p, System.IO.Path.GetDirectoryName(p)));
            }
            else if (Directory.Exists(p))
            {
                var opts = new EnumerationOptions
                {
                    RecurseSubdirectories = recursive,
                    IgnoreInaccessible = true,
                    AttributesToSkip = FileAttributes.Hidden | FileAttributes.System,
                };
                var files = Directory.EnumerateFiles(p, "*", opts)
                    .Where(f => IsVideoFile(f, extensions) && !IsAnimatedSheet(f))
                    .OrderBy(f => f, StringComparer.OrdinalIgnoreCase);
                foreach (var f in files)
                    if (seen.Add(f)) result.Add(new VideoInput(f, p));
            }
        }
        return result;
    }
}
