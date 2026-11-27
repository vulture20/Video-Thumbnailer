using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using VideoThumbnailer.Core.Input;
using VideoThumbnailer.Core.Layout;

namespace VideoThumbnailer.Core.Output;

/// <summary>
/// The open JSON sidecar next to a sheet (#38): which video, how big the sheet is and, per tile, its rectangle and
/// capture time. A viewer uses it to start the video at the clicked tile (#39); the queue uses it to restore capture
/// times chosen by hand (#23). The format is documented in <c>docs/sheet-json.md</c>.
/// </summary>
public sealed class SheetSidecar
{
    public const string FormatName = "video-thumbnailer-sheet";
    public const int CurrentVersion = 1;
    /// <summary>"movie.jpg" → "movie.sheet.json".</summary>
    public const string Suffix = ".sheet.json";

    public string Format { get; set; } = FormatName;
    public int Version { get; set; } = CurrentVersion;
    public string? Generator { get; set; }
    /// <summary>When the sheet was written (UTC, ISO 8601).</summary>
    public DateTimeOffset? Created { get; set; }
    public SidecarVideo Video { get; set; } = new();
    public SidecarSheet Sheet { get; set; } = new();
    /// <summary>Tiles in timeline order (index 0 = earliest frame).</summary>
    public List<SidecarTile> Tiles { get; set; } = [];

    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <summary>Path of the sidecar for a sheet: "movie.jpg" and "movie.sheet.mp4" both give "movie.sheet.json".</summary>
    public static string PathFor(string sheetPath)
    {
        var dir = Path.GetDirectoryName(sheetPath) ?? ".";
        var name = Path.GetFileNameWithoutExtension(sheetPath);
        foreach (var marker in new[] { OutputPathResolver.AnimatedMarker, OutputPathResolver.TrailerMarker })
            if (name.EndsWith(marker, StringComparison.OrdinalIgnoreCase)) name = name[..^marker.Length];
        return Path.Combine(dir, name + Suffix);
    }

    /// <summary>Builds the sidecar for a finished sheet.</summary>
    /// <param name="manual">Per tile in timeline order: whether its time was chosen by hand.</param>
    /// <param name="clipSeconds">Animated sheets: clip length per tile; null for still sheets.</param>
    public static SheetSidecar Create(string videoPath, string sheetPath, double duration, (int Width, int Height) videoSize,
                                      GridLayout layout, IReadOnlyList<double> times, IReadOnlyList<bool> manual,
                                      IReadOnlyList<double>? clipSeconds = null)
    {
        var sidecarDir = Path.GetDirectoryName(Path.GetFullPath(PathFor(sheetPath)))!;
        var video = new SidecarVideo { Duration = Round(duration), Width = videoSize.Width, Height = videoSize.Height };
        if (VideoSource.IsUrl(videoPath)) video.Url = videoPath;
        else
        {
            var full = Path.GetFullPath(videoPath);
            video.AbsolutePath = full;
            video.Path = RelativePath(sidecarDir, full);
        }
        var sidecar = new SheetSidecar
        {
            Generator = "Video-Thumbnailer " + typeof(SheetSidecar).Assembly.GetName().Version?.ToString(3),
            Created = DateTimeOffset.UtcNow,
            Video = video,
            Sheet = new SidecarSheet
            {
                File = Path.GetFileName(sheetPath),
                Width = layout.SheetWidth,
                Height = layout.SheetHeight,
                Animated = clipSeconds is not null,
            },
        };
        for (var i = 0; i < layout.Tiles.Count && i < times.Count; i++)
        {
            var t = layout.Tiles[i];
            sidecar.Tiles.Add(new SidecarTile
            {
                Index = i, X = t.X, Y = t.Y, Width = t.Width, Height = t.Height,
                Time = Round(times[i]),
                Manual = i < manual.Count && manual[i] ? true : null,
                ClipSeconds = clipSeconds is not null && i < clipSeconds.Count ? Round(clipSeconds[i]) : null,
            });
        }
        return sidecar;
    }

    /// <summary>Relative path with "/" separators, or null when there is none (other drive).</summary>
    internal static string? RelativePath(string fromDir, string file)
    {
        if (!string.Equals(Path.GetPathRoot(fromDir), Path.GetPathRoot(file), StringComparison.OrdinalIgnoreCase)) return null;
        return Path.GetRelativePath(fromDir, file).Replace('\\', '/');
    }

    private static double Round(double v) => Math.Round(v, 3);

    public void Write(string path)
    {
        var dir = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        var tmp = path + ".part";
        File.WriteAllText(tmp, ToJson(), new System.Text.UTF8Encoding(false));
        File.Move(tmp, path, overwrite: true);
    }

    public string ToJson() => JsonSerializer.Serialize(this, Json) + "\n";

    /// <summary>Reads a sidecar; throws <see cref="InvalidDataException"/> if it is not one.</summary>
    public static SheetSidecar Parse(string json)
    {
        SheetSidecar? s;
        try { s = JsonSerializer.Deserialize<SheetSidecar>(json, Json); }
        catch (JsonException e) { throw new InvalidDataException(e.Message, e); }
        if (s is null || s.Format != FormatName) throw new InvalidDataException("not a " + FormatName + " file");
        if (s.Version > CurrentVersion) throw new InvalidDataException("version " + s.Version.ToString(CultureInfo.InvariantCulture) + " is newer than " + CurrentVersion);
        s.Tiles.Sort((a, b) => a.Index.CompareTo(b.Index));
        return s;
    }

    public static SheetSidecar Read(string path) => Parse(File.ReadAllText(path));

    /// <summary>Reads a sidecar if the file exists and is valid, else null.</summary>
    public static SheetSidecar? TryRead(string path)
    {
        try { return File.Exists(path) ? Read(path) : null; }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException) { return null; }
    }

    /// <summary>
    /// The video of a sidecar at <paramref name="sidecarPath"/>: the relative path first (folder moved together),
    /// then the absolute one, then the URL. Null when none of them exists.
    /// </summary>
    public string? ResolveVideo(string sidecarPath)
    {
        var dir = Path.GetDirectoryName(Path.GetFullPath(sidecarPath))!;
        if (!string.IsNullOrEmpty(Video.Path))
        {
            var p = Path.GetFullPath(Path.Combine(dir, Video.Path.Replace('/', Path.DirectorySeparatorChar)));
            if (File.Exists(p)) return p;
        }
        if (!string.IsNullOrEmpty(Video.AbsolutePath) && File.Exists(Video.AbsolutePath)) return Video.AbsolutePath;
        return string.IsNullOrEmpty(Video.Url) ? null : Video.Url;
    }

    /// <summary>The sheet image next to the sidecar, null if it is missing.</summary>
    public string? ResolveSheet(string sidecarPath)
    {
        if (string.IsNullOrEmpty(Sheet.File)) return null;
        var p = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(sidecarPath))!, Sheet.File);
        return File.Exists(p) ? p : null;
    }

    /// <summary>Capture times chosen by hand, per tile in timeline order (null = automatic); empty when there are none.</summary>
    public ManualTime?[] ManualTimes()
    {
        if (!Tiles.Any(t => t.Manual == true)) return [];
        var count = Tiles.Max(t => t.Index) + 1;
        var list = new ManualTime?[count];
        foreach (var t in Tiles)
            if (t.Manual == true && t.Index >= 0)
                list[t.Index] = new ManualTime(t.Time, Sheet.Animated ? t.ClipSeconds : null);
        var last = Array.FindLastIndex(list, m => m is not null);
        return list[..(last + 1)];
    }

    /// <summary>The tile at a point of the sheet (sheet pixels), null between tiles.</summary>
    public SidecarTile? TileAt(double x, double y) =>
        Tiles.FirstOrDefault(t => x >= t.X && y >= t.Y && x < t.X + t.Width && y < t.Y + t.Height);
}

public sealed class SidecarVideo
{
    /// <summary>Relative to the folder of the sidecar, with "/"; missing when the video is on another drive or a URL.</summary>
    public string? Path { get; set; }
    public string? AbsolutePath { get; set; }
    /// <summary>For videos read from a web address (#21).</summary>
    public string? Url { get; set; }
    /// <summary>Seconds.</summary>
    public double Duration { get; set; }
    /// <summary>Display size (rotation and pixel aspect applied).</summary>
    public int Width { get; set; }
    public int Height { get; set; }
}

public sealed class SidecarSheet
{
    /// <summary>File name of the sheet, in the same folder as the sidecar.</summary>
    public string File { get; set; } = "";
    public int Width { get; set; }
    public int Height { get; set; }
    public bool Animated { get; set; }
}

public sealed class SidecarTile
{
    /// <summary>Position in timeline order.</summary>
    public int Index { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    /// <summary>Capture time in seconds (for animated sheets: start of the clip).</summary>
    public double Time { get; set; }
    /// <summary>True when the time was chosen by hand; left out otherwise.</summary>
    public bool? Manual { get; set; }
    /// <summary>Animated sheets: length of the clip in seconds.</summary>
    public double? ClipSeconds { get; set; }
}
