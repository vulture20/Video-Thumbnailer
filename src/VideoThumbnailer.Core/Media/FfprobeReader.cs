using System.Globalization;
using System.Text.Json;
using VideoThumbnailer.Core.Localization;
using VideoThumbnailer.Core.Util;

namespace VideoThumbnailer.Core.Media;

public static class FfprobeReader
{
    public static async Task<MediaInfo> ReadAsync(string ffprobePath, string videoPath, CancellationToken ct = default)
    {
        List<string> args = ["-v", "error", .. Input.VideoSource.InputOptions(videoPath), "-print_format", "json", "-show_format", "-show_streams", videoPath];
        var result = await ProcessRunner.RunAsync(ffprobePath, args, ct).ConfigureAwait(false);
        if (result.ExitCode != 0)
            throw new InvalidDataException(CoreText.T("FfprobeFailed", result.ExitCode, result.StdErr.Trim()));
        return Parse(System.Text.Encoding.UTF8.GetString(result.StdOut), videoPath);
    }

    internal static MediaInfo Parse(string json, string videoPath)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        root.TryGetProperty("format", out var format);

        VideoStreamInfo? video = null;
        var audio = new List<AudioStreamInfo>();
        double streamDuration = 0;

        if (root.TryGetProperty("streams", out var streams))
        {
            foreach (var s in streams.EnumerateArray())
            {
                var type = Str(s, "codec_type");
                if (type == "video" && video is null)
                {
                    if (s.TryGetProperty("disposition", out var disp) && Int(disp, "attached_pic") == 1)
                        continue; // cover art
                    video = new VideoStreamInfo
                    {
                        Index = Int(s, "index") ?? 0,
                        Codec = Str(s, "codec_name") ?? "unknown",
                        Profile = Str(s, "profile"),
                        Width = Int(s, "width") ?? 0,
                        Height = Int(s, "height") ?? 0,
                        SampleAspect = Ratio(Str(s, "sample_aspect_ratio")) is { } sar and > 0 ? sar : 1.0,
                        FrameRate = Ratio(Str(s, "avg_frame_rate")) is { } fps and > 0 ? fps : Ratio(Str(s, "r_frame_rate")) ?? 0,
                        PixelFormat = Str(s, "pix_fmt"),
                        ColorTransfer = Str(s, "color_transfer"),
                        ColorPrimaries = Str(s, "color_primaries"),
                        BitRate = Long(s, "bit_rate"),
                        Rotation = ReadRotation(s),
                    };
                    streamDuration = Dbl(s, "duration") ?? 0;
                }
                else if (type == "audio")
                {
                    string? lang = null;
                    if (s.TryGetProperty("tags", out var tags)) lang = Str(tags, "language");
                    audio.Add(new AudioStreamInfo
                    {
                        Index = Int(s, "index") ?? 0,
                        Codec = Str(s, "codec_name") ?? "unknown",
                        SampleRate = Int(s, "sample_rate") ?? 0,
                        Channels = Int(s, "channels") ?? 0,
                        ChannelLayout = Str(s, "channel_layout"),
                        BitRate = Long(s, "bit_rate"),
                        Language = lang,
                    });
                }
            }
        }

        string? title = null;
        if (format.ValueKind == JsonValueKind.Object && format.TryGetProperty("tags", out var ftags))
            title = Str(ftags, "title");

        long size = format.ValueKind == JsonValueKind.Object ? Long(format, "size") ?? 0 : 0;
        if (size == 0 && !Input.VideoSource.IsUrl(videoPath) && File.Exists(videoPath)) size = new FileInfo(videoPath).Length;

        var duration = format.ValueKind == JsonValueKind.Object ? Dbl(format, "duration") ?? 0 : 0;
        if (duration <= 0) duration = streamDuration;

        return new MediaInfo
        {
            Path = videoPath,
            FileSize = size,
            Duration = duration,
            BitRate = format.ValueKind == JsonValueKind.Object ? Long(format, "bit_rate") : null,
            ContainerFormat = format.ValueKind == JsonValueKind.Object ? Str(format, "format_name") : null,
            Title = title,
            Video = video,
            AudioStreams = audio,
        };
    }

    private static int ReadRotation(JsonElement s)
    {
        if (s.TryGetProperty("side_data_list", out var sdl) && sdl.ValueKind == JsonValueKind.Array)
            foreach (var sd in sdl.EnumerateArray())
                if (Int(sd, "rotation") is { } r) return r;
        if (s.TryGetProperty("tags", out var tags) && Int(tags, "rotate") is { } rot) return rot;
        return 0;
    }

    private static string? Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) ? v.ValueKind switch
        {
            JsonValueKind.String => v.GetString(),
            JsonValueKind.Number => v.GetRawText(),
            _ => null,
        } : null;

    private static int? Int(JsonElement e, string name) =>
        Str(e, name) is { } s && double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? (int)d : null;

    private static long? Long(JsonElement e, string name) =>
        Str(e, name) is { } s && long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var l) ? l : null;

    private static double? Dbl(JsonElement e, string name) =>
        Str(e, name) is { } s && double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : null;

    /// <summary>Parses "30000/1001" or "16:9" style ratios.</summary>
    internal static double? Ratio(string? s)
    {
        if (string.IsNullOrEmpty(s)) return null;
        var parts = s.Split('/', ':');
        if (parts.Length == 2 &&
            double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var a) &&
            double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var b) && b != 0)
            return a / b;
        return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : null;
    }
}
