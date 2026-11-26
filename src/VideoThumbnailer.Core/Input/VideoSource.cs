namespace VideoThumbnailer.Core.Input;

/// <summary>
/// A video is either a local file or an HTTP(S) address that ffmpeg/ffprobe read directly (with range requests,
/// so only the parts around the capture times are downloaded). These helpers give names, folders and ffmpeg
/// input options for both kinds.
/// </summary>
public static class VideoSource
{
    /// <summary>Seconds without any data after which ffmpeg/ffprobe give up on a remote video.</summary>
    public const int NetworkTimeoutSeconds = 30;

    /// <summary>True for http:// and https:// addresses.</summary>
    public static bool IsUrl(string? path) =>
        path is not null
        && (path.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || path.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        && Uri.TryCreate(path, UriKind.Absolute, out var u) && u.Host.Length > 0;

    /// <summary>File name with extension: the last part of a URL's path (decoded) or the local file name.</summary>
    public static string FileName(string path)
    {
        if (!IsUrl(path)) return Path.GetFileName(path);
        var uri = new Uri(path);
        var last = uri.AbsolutePath.TrimEnd('/');
        last = last[(last.LastIndexOf('/') + 1)..];
        var name = Uri.UnescapeDataString(last);
        foreach (var c in Path.GetInvalidFileNameChars().Append('/').Append('\\'))
            name = name.Replace(c, '_');
        return string.IsNullOrWhiteSpace(name) ? uri.Host : name;
    }

    public static string NameWithoutExtension(string path) => Path.GetFileNameWithoutExtension(FileName(path));

    public static string Extension(string path) => Path.GetExtension(FileName(path)).TrimStart('.');

    /// <summary>
    /// Address or path as it may be shown on a sheet or in the queue. For URLs without user name/password,
    /// query and fragment, because those often carry access tokens.
    /// </summary>
    public static string DisplayPath(string path)
    {
        if (!IsUrl(path)) return path;
        var uri = new Uri(path);
        return uri.GetComponents(UriComponents.Scheme | UriComponents.Host | UriComponents.Port | UriComponents.Path, UriFormat.Unescaped);
    }

    /// <summary>Folder of a local file, or the URL up to the last "/" (without credentials and query).</summary>
    public static string Folder(string path)
    {
        if (!IsUrl(path)) return Path.GetDirectoryName(path) ?? "";
        var shown = DisplayPath(path);
        var slash = shown.LastIndexOf('/');
        return slash > shown.IndexOf("//", StringComparison.Ordinal) + 1 ? shown[..(slash + 1)] : shown + "/";
    }

    /// <summary>
    /// ffmpeg/ffprobe options that go in front of <c>-i</c>: for URLs a read timeout and automatic reconnects,
    /// nothing for local files.
    /// </summary>
    public static IReadOnlyList<string> InputOptions(string path) => IsUrl(path)
        ?
        [
            "-rw_timeout", (NetworkTimeoutSeconds * 1_000_000L).ToString(System.Globalization.CultureInfo.InvariantCulture),
            "-reconnect", "1", "-reconnect_on_network_error", "1", "-reconnect_delay_max", "5",
        ]
        : [];
}
