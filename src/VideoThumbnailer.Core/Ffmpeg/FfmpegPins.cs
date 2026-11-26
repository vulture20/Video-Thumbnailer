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
/// The ffmpeg builds fixed in this app (ffmpeg-pins.json, embedded). The SHA-256 hashes were computed when the pins
/// were created (tools/ffmpeg-pins.py) and reviewed in a pull request, so a file swapped on the download server later
/// is rejected, even if its checksum file was swapped too.
/// </summary>
public static class FfmpegPins
{
    public const string ResourceName = "VideoThumbnailer.Ffmpeg.ffmpeg-pins.json";
    private static readonly Lazy<FfmpegPinFile> Embedded = new(Load);

    public static FfmpegPinFile All => Embedded.Value;

    public static FfmpegPin? ForCurrentPlatform() => FfmpegSources.CurrentRid() is { } rid ? For(rid) : null;

    public static FfmpegPin? For(string rid) => All.Platforms.GetValueOrDefault(rid);

    public static FfmpegPinFile Parse(string json) =>
        JsonSerializer.Deserialize<FfmpegPinFile>(json, SettingsJson.Options) ?? new FfmpegPinFile();

    private static FfmpegPinFile Load()
    {
        using var stream = typeof(FfmpegPins).Assembly.GetManifestResourceStream(ResourceName);
        if (stream is null) return new FfmpegPinFile();
        using var reader = new StreamReader(stream);
        return Parse(reader.ReadToEnd());
    }
}

/// <summary>Installs the pinned build; needs no network access to know what to install.</summary>
public sealed class PinnedSource(FfmpegPin pin) : IFfmpegSource
{
    public string Name => pin.Source;
    public string Homepage => pin.Homepage;
    public bool IsPinned => true;

    public Task<FfmpegRelease> GetLatestAsync(HttpClient http, CancellationToken ct) => Task.FromResult(ToRelease());

    internal FfmpegRelease ToRelease()
    {
        var assets = pin.Assets.Select(a => new FfmpegAsset(a.FileName, new Uri(a.Url), a.Sha256.ToLowerInvariant(), a.Kind,
            a.Binaries, a.Libraries is { Count: > 0 } ? a.Libraries : null)).ToList();
        return new FfmpegRelease(Name, pin.Version, assets, pin.MacTeamId);
    }
}
