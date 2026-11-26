using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using SharpCompress.Readers;
using VideoThumbnailer.Core.Settings;

namespace VideoThumbnailer.Core.Ffmpeg;

public sealed class FfmpegManifest
{
    public string Source { get; set; } = "";
    public string Version { get; set; } = "";
    public string Id { get; set; } = "";
    public DateTimeOffset InstalledAt { get; set; }
    public List<string> Files { get; set; } = [];
}

public sealed record FfmpegUpdateInfo(FfmpegManifest? Installed, FfmpegRelease Latest)
{
    public bool UpdateAvailable => Installed is null || !string.Equals(Installed.Id, Latest.Id, StringComparison.OrdinalIgnoreCase);
}

public sealed record DownloadProgress(string File, long BytesReceived, long? TotalBytes);

/// <summary>
/// Downloads, verifies (SHA-256) and installs ffmpeg + ffprobe into the app's data folder.
/// Installation is atomic: a new build is unpacked next to the old one and swapped in only when complete.
/// </summary>
public sealed class FfmpegInstaller
{
    private readonly IFfmpegSource _source;
    private readonly HttpClient _http;
    private readonly string _root;

    public FfmpegInstaller(IFfmpegSource source, HttpClient? http = null, string? installRoot = null)
    {
        _source = source;
        _http = http ?? CreateHttpClient();
        _root = installRoot ?? AppPaths.FfmpegDir;
    }

    /// <summary>Installer for the current platform, or null if there is no download source for it.</summary>
    public static FfmpegInstaller? ForCurrentPlatform(string? installRoot = null) =>
        FfmpegSources.ForCurrentPlatform() is { } src ? new FfmpegInstaller(src, null, installRoot) : null;

    public IFfmpegSource Source => _source;
    public string InstallRoot => _root;
    public string BinDirectory => Path.Combine(_root, "bin");
    private string ManifestPath => Path.Combine(_root, "manifest.json");

    public static HttpClient CreateHttpClient()
    {
        var http = new HttpClient { Timeout = TimeSpan.FromMinutes(15) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("Video-Thumbnailer/" + typeof(FfmpegInstaller).Assembly.GetName().Version?.ToString(3));
        return http;
    }

    public FfmpegManifest? ReadManifest()
    {
        try
        {
            return File.Exists(ManifestPath)
                ? JsonSerializer.Deserialize<FfmpegManifest>(File.ReadAllText(ManifestPath), SettingsJson.Options)
                : null;
        }
        catch { return null; }
    }

    public async Task<FfmpegUpdateInfo> CheckForUpdateAsync(CancellationToken ct = default)
    {
        var latest = await _source.GetLatestAsync(_http, ct).ConfigureAwait(false);
        return new FfmpegUpdateInfo(ReadManifest(), latest);
    }

    public async Task<FfmpegTools> InstallAsync(FfmpegRelease? release = null, IProgress<DownloadProgress>? progress = null, CancellationToken ct = default)
    {
        release ??= await _source.GetLatestAsync(_http, ct).ConfigureAwait(false);
        Directory.CreateDirectory(_root);
        var work = Path.Combine(_root, ".work-" + Guid.NewGuid().ToString("N")[..8]);
        var staging = Path.Combine(work, "bin");
        Directory.CreateDirectory(staging);
        try
        {
            var files = new List<string>();
            foreach (var asset in release.Assets)
            {
                var archive = Path.Combine(work, asset.FileName);
                await DownloadAsync(asset, archive, progress, ct).ConfigureAwait(false);
                await VerifyAsync(archive, asset.Sha256, ct).ConfigureAwait(false);
                files.AddRange(Extract(archive, asset, staging));
                File.Delete(archive);
            }

            foreach (var f in files) MakeExecutable(f);
            var check = FfmpegLocator.FromDirectory(staging, FfmpegOrigin.Managed)
                        ?? throw new InvalidDataException("The downloaded archive did not contain ffmpeg and ffprobe.");
            if (await FfmpegLocator.GetVersionAsync(check.FfmpegPath, ct).ConfigureAwait(false) is null)
                throw new InvalidDataException("The downloaded ffmpeg does not run on this system.");

            SwapIn(staging);
            var manifest = new FfmpegManifest
            {
                Source = release.SourceName,
                Version = release.Version,
                Id = release.Id,
                InstalledAt = DateTimeOffset.UtcNow,
                Files = files.Select(Path.GetFileName).ToList()!,
            };
            File.WriteAllText(ManifestPath, JsonSerializer.Serialize(manifest, SettingsJson.Options));
            return FfmpegLocator.FromDirectory(BinDirectory, FfmpegOrigin.Managed)!;
        }
        finally
        {
            TryDeleteDirectory(work);
        }
    }

    public bool Uninstall()
    {
        if (!Directory.Exists(_root)) return false;
        TryDeleteDirectory(_root);
        return true;
    }

    private async Task DownloadAsync(FfmpegAsset asset, string target, IProgress<DownloadProgress>? progress, CancellationToken ct)
    {
        using var resp = await _http.GetAsync(asset.Url, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        var total = resp.Content.Headers.ContentLength;
        await using var src = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        await using var dst = new FileStream(target, FileMode.Create, FileAccess.Write, FileShare.None, 1 << 16, useAsync: true);
        var buffer = new byte[1 << 16];
        long received = 0;
        var lastReport = 0L;
        int n;
        while ((n = await src.ReadAsync(buffer, ct).ConfigureAwait(false)) > 0)
        {
            await dst.WriteAsync(buffer.AsMemory(0, n), ct).ConfigureAwait(false);
            received += n;
            if (received - lastReport > 512 * 1024) { progress?.Report(new DownloadProgress(asset.FileName, received, total)); lastReport = received; }
        }
        progress?.Report(new DownloadProgress(asset.FileName, received, total));
    }

    internal static async Task VerifyAsync(string file, string expectedSha256, CancellationToken ct)
    {
        await using var fs = File.OpenRead(file);
        var hash = Convert.ToHexStringLower(await SHA256.HashDataAsync(fs, ct).ConfigureAwait(false));
        if (!string.Equals(hash, expectedSha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"Checksum mismatch for {Path.GetFileName(file)}: expected {expectedSha256}, got {hash}.");
    }

    /// <summary>Extracts the wanted executables (by file name, anywhere in the archive) into <paramref name="dest"/>.</summary>
    internal static List<string> Extract(string archive, FfmpegAsset asset, string dest)
    {
        var wanted = asset.Binaries.Select(FfmpegLocator.ExeName).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var written = new List<string>();
        if (asset.Kind == ArchiveKind.Zip)
        {
            using var zip = ZipFile.OpenRead(archive);
            foreach (var e in zip.Entries)
            {
                if (!wanted.Contains(e.Name)) continue;
                var target = Path.Combine(dest, e.Name);
                e.ExtractToFile(target, overwrite: true);
                written.Add(target);
            }
        }
        else
        {
            using var stream = File.OpenRead(archive);
            using var reader = ReaderFactory.OpenReader(stream);
            while (reader.MoveToNextEntry())
            {
                if (reader.Entry.IsDirectory || reader.Entry.Key is null) continue;
                var name = Path.GetFileName(reader.Entry.Key.Replace('\\', '/'));
                if (!wanted.Contains(name)) continue;
                var target = Path.Combine(dest, name);
                using (var es = reader.OpenEntryStream())
                using (var fs = File.Create(target))
                    es.CopyTo(fs);
                written.Add(target);
            }
        }
        var missing = wanted.Except(written.Select(Path.GetFileName)!, StringComparer.OrdinalIgnoreCase).ToList();
        if (missing.Count > 0)
            throw new InvalidDataException($"{Path.GetFileName(archive)} does not contain: {string.Join(", ", missing)}");
        return written;
    }

    private void SwapIn(string staging)
    {
        var bin = BinDirectory;
        var old = bin + ".old";
        TryDeleteDirectory(old);
        if (Directory.Exists(bin)) Directory.Move(bin, old);
        try
        {
            Directory.Move(staging, bin);
        }
        catch
        {
            if (Directory.Exists(old) && !Directory.Exists(bin)) Directory.Move(old, bin); // roll back
            throw;
        }
        TryDeleteDirectory(old);
    }

    private static void MakeExecutable(string file)
    {
        if (OperatingSystem.IsWindows()) return;
        File.SetUnixFileMode(file, File.GetUnixFileMode(file)
            | UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute
            | UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }

    private static void TryDeleteDirectory(string dir)
    {
        try { if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true); } catch { /* best effort */ }
    }
}
