using System.Globalization;
using SkiaSharp;
using VideoThumbnailer.Core.Media;
using VideoThumbnailer.Core.Util;

namespace VideoThumbnailer.Core.Extraction;

/// <summary>Grabs single frames with ffmpeg (fast input seeking, one process per frame).</summary>
public sealed class FrameExtractor(string ffmpegPath)
{
    // Tone mapping for HDR (PQ/HLG) sources. Needs zimg (zscale), which the downloaded builds include.
    internal const string HdrToneMap =
        "zscale=t=linear:npl=100,format=gbrpf32le,zscale=p=bt709,tonemap=tonemap=hable:desat=0,zscale=t=bt709:m=bt709:r=tv,format=yuv420p";

    /// <summary>
    /// Extracts the frame at <paramref name="time"/>. When <paramref name="width"/>/<paramref name="height"/>
    /// are given, ffmpeg scales to exactly that size; otherwise the full display resolution is returned.
    /// Returns null if no frame could be decoded.
    /// </summary>
    public async Task<SKBitmap?> ExtractAsync(MediaInfo info, double time, int? width, int? height, CancellationToken ct)
    {
        var video = info.Video ?? throw new InvalidDataException("No video stream.");
        // Try exact time first; near the end of a file a fast seek can overshoot, so retry a bit earlier.
        double[] attempts = [time, Math.Max(0, time - 2), Math.Max(0, time - 10)];
        foreach (var t in attempts.Distinct())
        {
            var bmp = await ExtractAtAsync(info, video, t, width, height, ct).ConfigureAwait(false);
            if (bmp is not null) return bmp;
        }
        return null;
    }

    /// <summary>
    /// Like <see cref="ExtractAsync"/>, but a (nearly) black or single-colored frame is replaced by the nearest good
    /// one within ±<paramref name="searchSeconds"/> (see <see cref="BlankFrames"/>). Returns the time actually used.
    /// If nothing better is found, the original frame is kept.
    /// </summary>
    public async Task<(SKBitmap? Bitmap, double Time)> ExtractNonBlankAsync(MediaInfo info, double time, int? width, int? height,
                                                                            double searchSeconds, CancellationToken ct)
    {
        var bmp = await ExtractAsync(info, time, width, height, ct).ConfigureAwait(false);
        if (bmp is null || searchSeconds <= 0 || !BlankFrames.IsBlank(bmp)) return (bmp, time);
        var t = await FindNonBlankAsync(info, time, searchSeconds, width, height, ct).ConfigureAwait(false);
        if (t is not { } found) return (bmp, time);
        bmp.Dispose();
        return (found.Bitmap, found.Time);
    }

    /// <summary>Nearest time around <paramref name="time"/> whose frame is not blank, with that frame; null if there is none.</summary>
    public async Task<(SKBitmap Bitmap, double Time)?> FindNonBlankAsync(MediaInfo info, double time, double searchSeconds, int? width, int? height, CancellationToken ct)
    {
        var video = info.Video ?? throw new InvalidDataException("No video stream.");
        foreach (var t in BlankFrames.Candidates(time, searchSeconds, info.Duration))
        {
            var candidate = await ExtractAtAsync(info, video, t, width, height, ct).ConfigureAwait(false);
            if (candidate is null) continue;
            if (!BlankFrames.IsBlank(candidate)) return (candidate, t);
            candidate.Dispose();
        }
        return null;
    }

    private async Task<SKBitmap?> ExtractAtAsync(MediaInfo info, VideoStreamInfo video, double t, int? width, int? height, CancellationToken ct)
    {
        var bmp = await RunAsync(info, video, t, width, height, video.IsHdr, ct).ConfigureAwait(false);
        if (bmp is null && video.IsHdr)
            bmp = await RunAsync(info, video, t, width, height, false, ct).ConfigureAwait(false); // build without zscale
        return bmp;
    }

    private async Task<SKBitmap?> RunAsync(MediaInfo info, VideoStreamInfo video, double time, int? width, int? height, bool toneMap, CancellationToken ct)
    {
        var filters = new List<string>();
        if (toneMap) filters.Add(HdrToneMap);
        var (dw, dh) = video.DisplaySize;
        var w = width ?? dw;
        var h = height ?? dh;
        filters.Add($"scale=w={w}:h={h}:flags=bicubic");
        filters.Add("setsar=1");
        filters.Add("format=bgra");

        List<string> args =
        [
            "-hide_banner", "-loglevel", "error", "-nostdin",
            "-ss", time.ToString("0.###", CultureInfo.InvariantCulture),
            .. Input.VideoSource.InputOptions(info.Path),
            "-i", info.Path,
            "-map", $"0:{video.Index}",
            "-frames:v", "1", "-an", "-sn", "-dn",
            "-vf", string.Join(",", filters),
            "-f", "image2pipe", "-c:v", "bmp", "-",
        ];
        var result = await ProcessRunner.RunAsync(ffmpegPath, args, ct).ConfigureAwait(false);
        if (result.ExitCode != 0 || result.StdOut.Length == 0) return null;
        return SKBitmap.Decode(result.StdOut);
    }
}
