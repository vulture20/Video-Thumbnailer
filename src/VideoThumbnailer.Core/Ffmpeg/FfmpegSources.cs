using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using VideoThumbnailer.Core.Localization;

namespace VideoThumbnailer.Core.Ffmpeg;

public enum ArchiveKind { Zip, TarXz }

/// <summary>One downloadable archive containing one or more of the wanted binaries.</summary>
public sealed record FfmpegAsset(string FileName, Uri Url, string Sha256, ArchiveKind Kind, IReadOnlyList<string> Binaries);

/// <summary>A concrete build that can be installed. <see cref="Id"/> changes whenever the build changes.</summary>
public sealed record FfmpegRelease(string SourceName, string Version, IReadOnlyList<FfmpegAsset> Assets)
{
    public string Id => string.Join("+", Assets.Select(a => a.Sha256));
}

public interface IFfmpegSource
{
    string Name { get; }
    string Homepage { get; }
    Task<FfmpegRelease> GetLatestAsync(HttpClient http, CancellationToken ct);
}

public static class FfmpegSources
{
    /// <summary>Picks the download source for the current OS/architecture, or null if none exists.</summary>
    public static IFfmpegSource? ForCurrentPlatform()
    {
        var arch = RuntimeInformation.OSArchitecture;
        if (OperatingSystem.IsWindows())
            return arch == Architecture.Arm64 ? new BtbNSource("winarm64") : new BtbNSource("win64");
        if (OperatingSystem.IsLinux())
            return arch switch
            {
                Architecture.X64 => new BtbNSource("linux64"),
                Architecture.Arm64 => new BtbNSource("linuxarm64"),
                _ => null,
            };
        if (OperatingSystem.IsMacOS())
            return new MartinRiedlSource(arch == Architecture.Arm64 ? "arm64" : "amd64");
        return null;
    }

    /// <summary>Parses "sha256␠␠filename" lines (sha256sum format).</summary>
    public static Dictionary<string, string> ParseChecksums(string text)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var line in text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var m = Regex.Match(line, @"^(?<hash>[0-9a-fA-F]{64})\s+\*?(?<name>\S.*)$");
            if (m.Success) result[m.Groups["name"].Value.Trim()] = m.Groups["hash"].Value.ToLowerInvariant();
        }
        return result;
    }
}

/// <summary>
/// Static GPL builds from https://github.com/BtbN/FFmpeg-Builds (Windows and Linux, x64 and arm64).
/// Uses the floating "latest" release and its checksums.sha256; picks the newest release branch
/// build (e.g. n8.1), not master.
/// </summary>
public sealed partial class BtbNSource(string target) : IFfmpegSource
{
    public const string BaseUrl = "https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/";
    public string Name => $"BtbN/FFmpeg-Builds ({target})";
    public string Homepage => "https://github.com/BtbN/FFmpeg-Builds";
    public string Target => target;

    public async Task<FfmpegRelease> GetLatestAsync(HttpClient http, CancellationToken ct)
    {
        var text = await http.GetStringAsync(BaseUrl + "checksums.sha256", ct).ConfigureAwait(false);
        return SelectRelease(FfmpegSources.ParseChecksums(text))
               ?? throw new InvalidOperationException(CoreText.T("NoSuitableBuild", target));
    }

    internal FfmpegRelease? SelectRelease(IReadOnlyDictionary<string, string> checksums)
    {
        var best = checksums
            .Select(kv => (kv, m: ReleaseAsset().Match(kv.Key)))
            .Where(x => x.m.Success && x.m.Groups["target"].Value == target && x.m.Groups["variant"].Value == "gpl")
            .OrderByDescending(x => int.Parse(x.m.Groups["major"].Value))
            .ThenByDescending(x => int.Parse(x.m.Groups["minor"].Value))
            .FirstOrDefault();
        if (best.kv.Key is null) return null;
        var kind = best.kv.Key.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) ? ArchiveKind.Zip : ArchiveKind.TarXz;
        var version = $"{best.m.Groups["major"].Value}.{best.m.Groups["minor"].Value}";
        var asset = new FfmpegAsset(best.kv.Key, new Uri(BaseUrl + best.kv.Key), best.kv.Value, kind, ["ffmpeg", "ffprobe"]);
        return new FfmpegRelease(Name, version, [asset]);
    }

    // e.g. ffmpeg-n8.1-latest-win64-gpl-8.1.zip, ffmpeg-n8.1-latest-linux64-gpl-8.1.tar.xz
    [GeneratedRegex(@"^ffmpeg-n(?<major>\d+)\.(?<minor>\d+)-latest-(?<target>[a-z0-9]+)-(?<variant>gpl|lgpl|gpl-shared|lgpl-shared|nonfree)-\d+\.\d+\.(zip|tar\.xz)$")]
    private static partial Regex ReleaseAsset();
}

/// <summary>
/// macOS builds from https://ffmpeg.martin-riedl.de (signed and notarized, arm64 and amd64).
/// ffmpeg and ffprobe come as separate ZIPs, each with a .sha256 file next to it.
/// </summary>
public sealed class MartinRiedlSource(string arch) : IFfmpegSource
{
    public string Name => $"ffmpeg.martin-riedl.de (macOS {arch})";
    public string Homepage => "https://ffmpeg.martin-riedl.de";

    private string Url(string tool) => $"https://ffmpeg.martin-riedl.de/redirect/latest/macos/{arch}/release/{tool}.zip";

    public async Task<FfmpegRelease> GetLatestAsync(HttpClient http, CancellationToken ct)
    {
        var assets = new List<FfmpegAsset>();
        foreach (var tool in new[] { "ffmpeg", "ffprobe" })
        {
            // Resolve the redirect so the checksum belongs to exactly the file we download later.
            using var head = new HttpRequestMessage(HttpMethod.Head, Url(tool));
            using var resp = await http.SendAsync(head, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
            resp.EnsureSuccessStatusCode();
            var finalUrl = resp.RequestMessage?.RequestUri ?? new Uri(Url(tool));
            var shaText = await http.GetStringAsync(finalUrl + ".sha256", ct).ConfigureAwait(false);
            var sha = shaText.Trim().Split((char[])[' ', '\t', '\n'], StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.ToLowerInvariant();
            if (sha is null || sha.Length != 64)
                throw new InvalidOperationException(CoreText.T("NoChecksum", finalUrl));
            assets.Add(new FfmpegAsset(tool + ".zip", finalUrl, sha, ArchiveKind.Zip, [tool]));
        }
        // The resolved URL contains the version/date folder; use it as a human readable version.
        var version = assets[0].Url.Segments.Reverse().Skip(1).FirstOrDefault()?.Trim('/') ?? "latest";
        return new FfmpegRelease(Name, version, assets);
    }
}
