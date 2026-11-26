using VideoThumbnailer.Core.Util;

namespace VideoThumbnailer.Core.Ffmpeg;

/// <summary>
/// Finds ffmpeg/ffprobe. Order: explicitly configured folder → app-managed download → PATH.
/// </summary>
public static class FfmpegLocator
{
    public static string ExeName(string tool) => OperatingSystem.IsWindows() ? tool + ".exe" : tool;

    public static FfmpegTools? Locate(string? customDirectory = null, string? managedDirectory = null)
    {
        if (!string.IsNullOrWhiteSpace(customDirectory) && FromDirectory(customDirectory, FfmpegOrigin.Custom) is { } custom)
            return custom;
        if (FromDirectory(Path.Combine(managedDirectory ?? AppPaths.FfmpegDir, "bin"), FfmpegOrigin.Managed) is { } managed)
            return managed;
        return FromPath();
    }

    public static FfmpegTools? FromDirectory(string dir, FfmpegOrigin origin)
    {
        var ff = Path.Combine(dir, ExeName("ffmpeg"));
        var fp = Path.Combine(dir, ExeName("ffprobe"));
        return File.Exists(ff) && File.Exists(fp) ? new FfmpegTools(ff, fp, origin) : null;
    }

    public static FfmpegTools? FromPath()
    {
        var path = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var dir in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                if (FromDirectory(dir.Trim('"'), FfmpegOrigin.SystemPath) is { } t) return t;
            }
            catch { /* invalid PATH entries */ }
        }
        // Common locations not always on PATH (e.g. GUI apps on macOS do not inherit the shell PATH).
        string[] extra = OperatingSystem.IsMacOS() ? ["/opt/homebrew/bin", "/usr/local/bin", "/opt/local/bin"]
            : OperatingSystem.IsLinux() ? ["/usr/bin", "/usr/local/bin", "/snap/bin"] : [];
        foreach (var dir in extra)
            if (FromDirectory(dir, FfmpegOrigin.SystemPath) is { } t) return t;
        return null;
    }

    /// <summary>Returns the first line of "ffmpeg -version", or null if it cannot be run.</summary>
    public static async Task<string?> GetVersionAsync(string ffmpegPath, CancellationToken ct = default)
    {
        try
        {
            var r = await ProcessRunner.RunAsync(ffmpegPath, ["-hide_banner", "-version"], ct).ConfigureAwait(false);
            if (r.ExitCode != 0) return null;
            var text = System.Text.Encoding.UTF8.GetString(r.StdOut);
            return text.Split('\n').FirstOrDefault()?.Trim();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return null;
        }
    }
}
