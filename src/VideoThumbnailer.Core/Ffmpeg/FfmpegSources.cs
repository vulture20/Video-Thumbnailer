using System.Runtime.InteropServices;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using VideoThumbnailer.Core.Localization;

namespace VideoThumbnailer.Core.Ffmpeg;

[JsonConverter(typeof(JsonStringEnumConverter<ArchiveKind>))]
public enum ArchiveKind { Zip, TarXz }

/// <summary>
/// One downloadable archive containing one or more of the wanted binaries. <paramref name="Libraries"/>: file
/// extensions of shared libraries that sit next to the binaries and must be installed with them (e.g. ".dll").
/// </summary>
public sealed record FfmpegAsset(string FileName, Uri Url, string Sha256, ArchiveKind Kind, IReadOnlyList<string> Binaries,
    IReadOnlyList<string>? Libraries = null);

/// <summary>
/// A concrete build that can be installed. <see cref="Id"/> changes whenever the build changes.
/// <paramref name="MacTeamId"/>: on macOS the binaries must carry a valid code signature of this Apple Team ID.
/// </summary>
public sealed record FfmpegRelease(string SourceName, string Version, IReadOnlyList<FfmpegAsset> Assets, string? MacTeamId = null)
{
    public string Id => string.Join("+", Assets.Select(a => a.Sha256));
}

public interface IFfmpegSource
{
    string Name { get; }
    string Homepage { get; }
    /// <summary>True if the checksums come from this app (ffmpeg-pins.json), not from the download server.</summary>
    bool IsPinned => false;
    Task<FfmpegRelease> GetLatestAsync(HttpClient http, CancellationToken ct);
}

public static class FfmpegSources
{
    /// <summary>
    /// Picks the download source for the current OS/architecture, or null if none exists. By default the build fixed
    /// in ffmpeg-pins.json (checksums stored in this app); <paramref name="latest"/> or a platform without a pinned
    /// build (Windows arm64) uses the newest build, checked only against the checksum published next to it.
    /// </summary>
    public static IFfmpegSource? ForCurrentPlatform(bool latest = false)
    {
        if (!latest && FfmpegPins.ForCurrentPlatform() is { } pin) return new PinnedSource(pin);
        return LatestForCurrentPlatform();
    }

    /// <summary>Runtime id of this OS/architecture as used in ffmpeg-pins.json, e.g. "win-x64", "osx-arm64".</summary>
    public static string? CurrentRid()
    {
        var arch = RuntimeInformation.OSArchitecture switch
        {
            Architecture.X64 => "x64",
            Architecture.Arm64 => "arm64",
            _ => null,
        };
        var os = OperatingSystem.IsWindows() ? "win" : OperatingSystem.IsLinux() ? "linux" : OperatingSystem.IsMacOS() ? "osx" : null;
        return arch is null || os is null ? null : $"{os}-{arch}";
    }

    private static IFfmpegSource? LatestForCurrentPlatform() => CurrentRid() switch
    {
        "win-x64" => new BtbNSource("win64"),
        "win-arm64" => new BtbNSource("winarm64"),
        "linux-x64" => new MartinRiedlSource("linux", "amd64"),
        "linux-arm64" => new MartinRiedlSource("linux", "arm64"),
        "osx-x64" => new MartinRiedlSource("macos", "amd64"),
        "osx-arm64" => new MartinRiedlSource("macos", "arm64"),
        _ => null,
    };

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
/// GPL builds from https://github.com/BtbN/FFmpeg-Builds (Windows and Linux, x64 and arm64). Only used for the newest
/// build ("ffmpeg install --latest") and on Windows arm64, which has no pinned build: BtbN keeps dated builds only
/// for 14 days, so they cannot be pinned.
/// Uses the floating "latest" release and its checksums.sha256; picks the newest release branch
/// build (e.g. n8.1), not master. Windows uses the "gpl-shared" variant: ffmpeg and ffprobe share their
/// DLLs there, which makes the download less than half as big (about 87 instead of 198 MB, about 200 instead of
/// 336 MB installed). Linux keeps the static build (62 vs. 53 MB, not worth shared libraries and rpath).
/// </summary>
public sealed partial class BtbNSource(string target) : IFfmpegSource
{
    public const string BaseUrl = "https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/";
    public string Name => $"BtbN/FFmpeg-Builds ({target})";
    public string Homepage => "https://github.com/BtbN/FFmpeg-Builds";
    public string Target => target;
    public string Variant => target.StartsWith("win", StringComparison.Ordinal) ? "gpl-shared" : "gpl";

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
            .Where(x => x.m.Success && x.m.Groups["target"].Value == target && x.m.Groups["variant"].Value == Variant)
            .OrderByDescending(x => int.Parse(x.m.Groups["major"].Value))
            .ThenByDescending(x => int.Parse(x.m.Groups["minor"].Value))
            .FirstOrDefault();
        if (best.kv.Key is null) return null;
        var kind = best.kv.Key.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) ? ArchiveKind.Zip : ArchiveKind.TarXz;
        var version = $"{best.m.Groups["major"].Value}.{best.m.Groups["minor"].Value}";
        var asset = new FfmpegAsset(best.kv.Key, new Uri(BaseUrl + best.kv.Key), best.kv.Value, kind, ["ffmpeg", "ffprobe"],
            Variant.EndsWith("-shared", StringComparison.Ordinal) ? [".dll"] : null);
        return new FfmpegRelease(Name, version, [asset]);
    }

    // e.g. ffmpeg-n8.1-latest-win64-gpl-8.1.zip, ffmpeg-n8.1-latest-linux64-gpl-8.1.tar.xz
    [GeneratedRegex(@"^ffmpeg-n(?<major>\d+)\.(?<minor>\d+)-latest-(?<target>[a-z0-9]+)-(?<variant>gpl|lgpl|gpl-shared|lgpl-shared|nonfree)-\d+\.\d+\.(zip|tar\.xz)$")]
    private static partial Regex ReleaseAsset();
}

/// <summary>
/// Builds from https://ffmpeg.martin-riedl.de (macOS: signed and notarized; Linux; arm64 and amd64), newest release.
/// ffmpeg and ffprobe come as separate ZIPs, each with a .sha256 file next to it.
/// </summary>
public sealed class MartinRiedlSource(string os, string arch) : IFfmpegSource
{
    public string Name => $"ffmpeg.martin-riedl.de ({os} {arch})";
    public string Homepage => "https://ffmpeg.martin-riedl.de";

    private string Url(string tool) => $"https://ffmpeg.martin-riedl.de/redirect/latest/{os}/{arch}/release/{tool}.zip";

    public async Task<FfmpegRelease> GetLatestAsync(HttpClient http, CancellationToken ct)
    {
        var assets = new List<FfmpegAsset>();
        foreach (var tool in new[] { "ffmpeg", "ffprobe" })
        {
            // Resolve the redirect so the checksum belongs to exactly the file we download later.
            // GET (headers only) instead of HEAD: the redirect service does not answer HEAD requests.
            var finalUrl = await ResolveAsync(http, Url(tool), ct).ConfigureAwait(false);
            var shaText = await GetStringAsync(http, finalUrl + ".sha256", ct).ConfigureAwait(false);
            var sha = shaText.Trim().Split((char[])[' ', '\t', '\n'], StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.ToLowerInvariant();
            if (sha is null || sha.Length != 64)
                throw new InvalidOperationException(CoreText.T("NoChecksum", finalUrl));
            assets.Add(new FfmpegAsset(tool + ".zip", finalUrl, sha, ArchiveKind.Zip, [tool]));
        }
        // The resolved URL contains the version/date folder; use it as a human readable version.
        var version = assets[0].Url.Segments.Reverse().Skip(1).FirstOrDefault()?.Trim('/') ?? "latest";
        return new FfmpegRelease(Name, version, assets);
    }

    private static async Task<Uri> ResolveAsync(HttpClient http, string url, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        using var resp = await http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        if (!resp.IsSuccessStatusCode)
            throw new HttpRequestException($"{(int)resp.StatusCode} {resp.ReasonPhrase}: {url}", null, resp.StatusCode);
        return resp.RequestMessage?.RequestUri ?? new Uri(url);
    }

    private static async Task<string> GetStringAsync(HttpClient http, string url, CancellationToken ct)
    {
        using var resp = await http.GetAsync(url, ct).ConfigureAwait(false);
        if (!resp.IsSuccessStatusCode)
            throw new HttpRequestException($"{(int)resp.StatusCode} {resp.ReasonPhrase}: {url}", null, resp.StatusCode);
        return await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
    }
}
