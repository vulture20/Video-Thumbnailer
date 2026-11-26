using System.Text.RegularExpressions;
using VideoThumbnailer.Core.Media;
using VideoThumbnailer.Core.Util;

namespace VideoThumbnailer.Core.Rendering;

/// <summary>
/// Expands placeholders like {filename}, {size:GB} or {fps} in the info header template.
/// Unknown placeholders are left as they are so typos are visible.
/// </summary>
public static partial class InfoTemplate
{
    [GeneratedRegex(@"\{(?<key>[a-zA-Z_]+)(?::(?<arg>[^}]*))?\}")]
    private static partial Regex Placeholder();

    public static IReadOnlyList<string> Placeholders { get; } =
    [
        "filename", "name", "ext", "folder", "path", "title", "container",
        "size", "size:B|KB|MB|GB", "duration", "bitrate",
        "width", "height", "resolution", "vcodec", "vprofile", "fps", "pixfmt", "hdr", "vbitrate", "rotation",
        "audio", "acodec", "samplerate", "channels", "channellayout", "abitrate", "alang", "audiotracks",
    ];

    public static string Expand(string template, MediaInfo info) =>
        Placeholder().Replace(template.Replace("\r\n", "\n"), m =>
            Resolve(m.Groups["key"].Value.ToLowerInvariant(), m.Groups["arg"].Success ? m.Groups["arg"].Value : null, info) ?? m.Value);

    private static string? Resolve(string key, string? arg, MediaInfo i)
    {
        var v = i.Video;
        var a = i.Audio;
        const string na = "n/a";
        return key switch
        {
            "filename" => Path.GetFileName(i.Path),
            "name" => Path.GetFileNameWithoutExtension(i.Path),
            "ext" => Path.GetExtension(i.Path).TrimStart('.'),
            "folder" => Path.GetDirectoryName(i.Path) ?? "",
            "path" => i.Path,
            "title" => i.Title ?? Path.GetFileNameWithoutExtension(i.Path),
            "container" => i.ContainerFormat ?? na,
            "size" => Formatting.Size(i.FileSize, arg),
            "duration" => Formatting.Duration(i.Duration),
            "bitrate" => Formatting.Bitrate(i.BitRate),
            "width" => v is null ? na : v.DisplaySize.Width.ToString(),
            "height" => v is null ? na : v.DisplaySize.Height.ToString(),
            "resolution" => v is null ? na : $"{v.DisplaySize.Width}×{v.DisplaySize.Height}",
            "vcodec" => v?.Codec ?? na,
            "vprofile" => v?.Profile ?? na,
            "fps" => v is null || v.FrameRate <= 0 ? na : Formatting.Number(v.FrameRate, "0.###"),
            "pixfmt" => v?.PixelFormat ?? na,
            "hdr" => v?.IsHdr == true ? (arg ?? " · HDR") : "",
            "vbitrate" => Formatting.Bitrate(v?.BitRate),
            "rotation" => v is null ? na : v.Rotation.ToString(),
            "audio" => a is null ? "none" : AudioSummary(a),
            "acodec" => a?.Codec ?? na,
            "samplerate" => a is null ? na : a.SampleRate + " Hz",
            "channels" => a is null ? na : a.Channels.ToString(),
            "channellayout" => a?.ChannelLayout ?? na,
            "abitrate" => Formatting.Bitrate(a?.BitRate),
            "alang" => a?.Language ?? na,
            "audiotracks" => i.AudioStreams.Count.ToString(),
            _ => null,
        };
    }

    private static string AudioSummary(AudioStreamInfo a)
    {
        var parts = new List<string> { a.Codec };
        if (a.SampleRate > 0) parts.Add(a.SampleRate + " Hz");
        if (!string.IsNullOrEmpty(a.ChannelLayout)) parts.Add(a.ChannelLayout!);
        else if (a.Channels > 0) parts.Add(a.Channels + " ch");
        if (a.BitRate is > 0) parts.Add(Formatting.Bitrate(a.BitRate));
        if (!string.IsNullOrEmpty(a.Language) && a.Language != "und") parts.Add(a.Language!);
        return string.Join(" · ", parts);
    }
}
