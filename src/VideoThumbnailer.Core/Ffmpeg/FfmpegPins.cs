using System.Text.Json;
using VideoThumbnailer.Core.Settings;

namespace VideoThumbnailer.Core.Ffmpeg;

/// <summary>One file of a pinned build, as stored in ffmpeg-pins.json.</summary>
public sealed class FfmpegPinAsset
{
    public string FileName { get; set; } = "";
    public string Url { get; set; } = "";
    public string Sha256 { get; set; } = "";
    public ArchiveKind Kind { get; set; } = ArchiveKind.Zip;
    public List<string> Binaries { get; set; } = [];
    public List<string>? Libraries { get; set; }
}

/// <summary>A pinned build for one platform.</summary>
public sealed class FfmpegPin
{
    public string Source { get; set; } = "";
    public string Homepage { get; set; } = "";
    public string Version { get; set; } = "";
    public List<FfmpegPinAsset> Assets { get; set; } = [];
    public string? MacTeamId { get; set; }
}

public sealed class FfmpegPinFile
{
    public string? Generated { get; set; }
    public Dictionary<string, FfmpegPin> Platforms { get; set; } = [];
}

/// <summary>
/// The fixed ffmpeg builds. The current list lives in this repository and is loaded from GitHub when the app looks for
/// ffmpeg (<see cref="RemoteUrl"/>); a weekly workflow adds newer builds there, so a new ffmpeg needs no new app
/// version. The copy embedded at build time is the fallback when GitHub cannot be reached. The SHA-256 hashes were
/// computed by tools/ffmpeg-pins.py when a build was added, so a file swapped on the download server later is rejected,
/// even if its checksum file was swapped too.
/// </summary>
public static class FfmpegPins
{
    public const string ResourceName = "VideoThumbnailer.Ffmpeg.ffmpeg-pins.json";
    public const string RemoteUrl = "https://raw.githubusercontent.com/vulture20/Video-Thumbnailer/main/src/VideoThumbnailer.Core/Ffmpeg/ffmpeg-pins.json";
    private static readonly Lazy<FfmpegPinFile> Embedded = new(Load);

    /// <summary>The list embedded in this app version.</summary>
    public static FfmpegPinFile All => Embedded.Value;

    public static FfmpegPin? ForCurrentPlatform() => FfmpegSources.CurrentRid() is { } rid ? For(rid) : null;

    public static FfmpegPin? For(string rid) => All.Platforms.GetValueOrDefault(rid);

    public static FfmpegPinFile Parse(string json) =>
        JsonSerializer.Deserialize<FfmpegPinFile>(json, SettingsJson.Options) ?? new FfmpegPinFile();

    /// <summary>
    /// The pin for <paramref name="rid"/> from the list on GitHub, or null if it cannot be loaded, is invalid, has no
    /// entry for this platform or is older than the embedded list (no downgrade to an older list).
    /// </summary>
    public static async Task<FfmpegPin?> LoadRemoteAsync(HttpClient http, string rid, string? url, CancellationToken ct)
    {
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(20));
            var remote = Parse(await http.GetStringAsync(url ?? RemoteUrl, timeout.Token).ConfigureAwait(false));
            if (string.CompareOrdinal(remote.Generated ?? "", All.Generated ?? "") < 0) return null;
            return remote.Platforms.GetValueOrDefault(rid) is { } pin && IsValid(pin) ? pin : null;
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or OperationCanceledException or NotSupportedException)
        {
            if (ct.IsCancellationRequested) throw;
            return null;
        }
    }

    internal static bool IsValid(FfmpegPin pin) =>
        pin.Assets.Count > 0 && pin.Assets.All(a =>
            a.Sha256.Length == 64 && a.Sha256.All(Uri.IsHexDigit) && a.Binaries.Count > 0 && !string.IsNullOrWhiteSpace(a.FileName)
            && Uri.TryCreate(a.Url, UriKind.Absolute, out var u) && u.Scheme == Uri.UriSchemeHttps);

    private static FfmpegPinFile Load()
    {
        using var stream = typeof(FfmpegPins).Assembly.GetManifestResourceStream(ResourceName);
        if (stream is null) return new FfmpegPinFile();
        using var reader = new StreamReader(stream);
        return Parse(reader.ReadToEnd());
    }
}

/// <summary>
/// Installs the pinned build: the current one from the list on GitHub, or the embedded one if that cannot be loaded.
/// </summary>
/// <param name="remoteUrl">Where the current list is loaded from; null for <see cref="FfmpegPins.RemoteUrl"/>, "" for
/// none (embedded list only).</param>
public sealed class PinnedSource(string rid, FfmpegPin embedded, string? remoteUrl = null) : IFfmpegSource
{
    public string Name => embedded.Source;
    public string Homepage => embedded.Homepage;
    public bool IsPinned => true;

    public async Task<FfmpegRelease> GetLatestAsync(HttpClient http, CancellationToken ct)
    {
        var pin = remoteUrl == "" ? null : await FfmpegPins.LoadRemoteAsync(http, rid, remoteUrl, ct).ConfigureAwait(false);
        return ToRelease(pin ?? embedded);
    }

    internal static FfmpegRelease ToRelease(FfmpegPin pin)
    {
        var assets = pin.Assets.Select(a => new FfmpegAsset(a.FileName, new Uri(a.Url), a.Sha256.ToLowerInvariant(), a.Kind,
            a.Binaries, a.Libraries is { Count: > 0 } ? a.Libraries : null)).ToList();
        return new FfmpegRelease(pin.Source, pin.Version, assets, pin.MacTeamId);
    }
}
