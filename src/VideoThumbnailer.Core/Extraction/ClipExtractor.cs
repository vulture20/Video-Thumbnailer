using System.Globalization;
using VideoThumbnailer.Core.Media;
using VideoThumbnailer.Core.Util;

namespace VideoThumbnailer.Core.Extraction;

/// <summary>
/// Decodes a short clip with ffmpeg, scaled to tile size, as a list of JPEG-encoded frames.
/// JPEG keeps memory small (a few dozen KB per frame) while the sheet is composed.
/// </summary>
public sealed class ClipExtractor(string ffmpegPath)
{
    public async Task<IReadOnlyList<byte[]>> ExtractAsync(MediaInfo info, double start, double seconds, int fps, int width, int height, CancellationToken ct)
    {
        var video = info.Video ?? throw new InvalidDataException("No video stream.");
        // Near the end of a file a seek can overshoot; retry a bit earlier like the still extractor.
        double[] attempts = [start, Math.Max(0, start - 2), Math.Max(0, start - 10)];
        foreach (var t in attempts.Distinct())
        {
            var frames = await RunAsync(info, video, t, seconds, fps, width, height, video.IsHdr, ct).ConfigureAwait(false);
            if (frames.Count == 0 && video.IsHdr)
                frames = await RunAsync(info, video, t, seconds, fps, width, height, false, ct).ConfigureAwait(false);
            if (frames.Count > 0) return frames;
        }
        return [];
    }

    private async Task<IReadOnlyList<byte[]>> RunAsync(MediaInfo info, VideoStreamInfo video, double start, double seconds, int fps,
                                                       int width, int height, bool toneMap, CancellationToken ct)
    {
        var filters = new List<string>();
        if (toneMap) filters.Add(FrameExtractor.HdrToneMap);
        filters.Add($"fps={fps}");
        filters.Add($"scale=w={width}:h={height}:flags=bicubic");
        filters.Add("setsar=1");
        filters.Add("format=yuvj420p");

        var frameCount = Math.Max(1, (int)Math.Round(seconds * fps));
        List<string> args =
        [
            "-hide_banner", "-loglevel", "error", "-nostdin",
            "-ss", start.ToString("0.###", CultureInfo.InvariantCulture),
            .. Input.VideoSource.InputOptions(info.Path),
            "-i", info.Path,
            "-map", $"0:{video.Index}",
            "-an", "-sn", "-dn",
            "-vf", string.Join(",", filters),
            "-frames:v", frameCount.ToString(CultureInfo.InvariantCulture),
            "-f", "image2pipe", "-c:v", "mjpeg", "-q:v", "3", "-",
        ];
        var result = await ProcessRunner.RunAsync(ffmpegPath, args, ct).ConfigureAwait(false);
        return result.StdOut.Length == 0 ? [] : SplitJpegs(result.StdOut);
    }

    /// <summary>Splits concatenated JPEG images at their end-of-image markers (FF D9 is never stuffed data).</summary>
    internal static List<byte[]> SplitJpegs(byte[] data)
    {
        var list = new List<byte[]>();
        var start = -1;
        for (var i = 0; i + 1 < data.Length; i++)
        {
            if (data[i] != 0xFF) continue;
            if (start < 0 && data[i + 1] == 0xD8) start = i;
            else if (start >= 0 && data[i + 1] == 0xD9)
            {
                list.Add(data[start..(i + 2)]);
                start = -1;
                i++;
            }
        }
        return list;
    }
}
