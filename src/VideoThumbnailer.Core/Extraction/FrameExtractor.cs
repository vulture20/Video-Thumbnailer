using System.Globalization;
using SkiaSharp;
using VideoThumbnailer.Core.Media;
using VideoThumbnailer.Core.Util;

namespace VideoThumbnailer.Core.Extraction;

/// <summary>Grabs single frames with ffmpeg (fast input seeking, one process per frame).</summary>
public sealed class FrameExtractor(string ffmpegPath)
{
    // Tone mapping for HDR (PQ/HLG) sources. Needs zimg (zscale), which the downloaded builds include.
    private const string HdrToneMap =
        "zscale=t=linear:npl=100,format=gbrpf32le,zscale=p=bt709,tonemap=tonemap=hable:desat=0,zscale=t=bt709:m=bt709:r=tv,format=yuv420p";

    /// <summary>
    /// Extracts the frame at <paramref name="time"/>. When <paramref name="width"/>/<paramref name="height"/>
    /// are given, ffmpeg scales to exactly that size; otherwise the full display resolution is returned.
    /// Returns null if no frame could be decoded.
    /// </summary>
    public async Task<SKBitmap?> ExtractAsync(MediaInfo info, double time, int? width, int? height, CancellationToken ct)
    {
        var video = info.Video ?? throw new InvalidDataException("No video stream.");
        var hdr = video.IsHdr;
        // Try exact time first; near the end of a file a fast seek can overshoot, so retry a bit earlier.
        double[] attempts = [time, Math.Max(0, time - 2), Math.Max(0, time - 10)];
        foreach (var t in attempts.Distinct())
        {
            var bmp = await RunAsync(info, video, t, width, height, hdr, ct).ConfigureAwait(false);
            if (bmp is null && hdr)
                bmp = await RunAsync(info, video, t, width, height, false, ct).ConfigureAwait(false); // build without zscale
            if (bmp is not null) return bmp;
        }
        return null;
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

        var args = new List<string>
        {
            "-hide_banner", "-loglevel", "error", "-nostdin",
            "-ss", time.ToString("0.###", CultureInfo.InvariantCulture),
            "-i", info.Path,
            "-map", $"0:{video.Index}",
            "-frames:v", "1", "-an", "-sn", "-dn",
            "-vf", string.Join(",", filters),
            "-f", "image2pipe", "-c:v", "bmp", "-",
        };
        var result = await ProcessRunner.RunAsync(ffmpegPath, args, ct).ConfigureAwait(false);
        if (result.ExitCode != 0 || result.StdOut.Length == 0) return null;
        return SKBitmap.Decode(result.StdOut);
    }
}
