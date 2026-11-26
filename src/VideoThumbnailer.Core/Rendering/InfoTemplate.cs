using System.Text.RegularExpressions;
using VideoThumbnailer.Core.Input;
using VideoThumbnailer.Core.Localization;
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

    /// <param name="lang">Language for words like "none" and the decimal separator; default <see cref="Lang.Current"/>.</param>
    public static string Expand(string template, MediaInfo info, string? lang = null)
    {
        lang ??= Lang.Current;
        return Placeholder().Replace(template.Replace("\r\n", "\n"), m =>
            Resolve(m.Groups["key"].Value.ToLowerInvariant(), m.Groups["arg"].Success ? m.Groups["arg"].Value : null, info, lang) ?? m.Value);
    }

    private static string? Resolve(string key, string? arg, MediaInfo i, string lang)
    {
        var v = i.Video;
        var a = i.Audio;
        var na = CoreText.TIn(lang, "Sheet.NotAvailable");
        var nf = Lang.NumberFormat(lang);
        return key switch
        {
            "filename" => VideoSource.FileName(i.Path),
            "name" => VideoSource.NameWithoutExtension(i.Path),
            "ext" => VideoSource.Extension(i.Path),
            "folder" => VideoSource.Folder(i.Path),
            "path" => VideoSource.DisplayPath(i.Path),
            "title" => i.Title ?? VideoSource.NameWithoutExtension(i.Path),
            "container" => i.ContainerFormat ?? na,
            "size" => Formatting.Size(i.FileSize, arg, nf),
            "duration" => Formatting.Duration(i.Duration),
            "bitrate" => Formatting.Bitrate(i.BitRate, nf, na),
            "width" => v is null ? na : v.DisplaySize.Width.ToString(),
            "height" => v is null ? na : v.DisplaySize.Height.ToString(),
            "resolution" => v is null ? na : $"{v.DisplaySize.Width}×{v.DisplaySize.Height}",
            "vcodec" => v?.Codec ?? na,
            "vprofile" => v?.Profile ?? na,
            "fps" => v is null || v.FrameRate <= 0 ? na : Formatting.Number(v.FrameRate, "0.###", nf),
            "pixfmt" => v?.PixelFormat ?? na,
            "hdr" => v?.IsHdr == true ? (arg ?? " · HDR") : "",
            "vbitrate" => Formatting.Bitrate(v?.BitRate, nf, na),
            "rotation" => v is null ? na : v.Rotation.ToString(),
            "audio" => a is null ? CoreText.TIn(lang, "Sheet.NoAudio") : AudioSummary(a, nf),
            "acodec" => a?.Codec ?? na,
            "samplerate" => a is null ? na : a.SampleRate + " Hz",
            "channels" => a is null ? na : a.Channels.ToString(),
            "channellayout" => a?.ChannelLayout ?? na,
            "abitrate" => Formatting.Bitrate(a?.BitRate, nf, na),
            "alang" => a?.Language ?? na,
            "audiotracks" => i.AudioStreams.Count.ToString(),
            _ => null,
        };
    }

    private static string AudioSummary(AudioStreamInfo a, IFormatProvider nf)
    {
        var parts = new List<string> { a.Codec };
        if (a.SampleRate > 0) parts.Add(a.SampleRate + " Hz");
        if (!string.IsNullOrEmpty(a.ChannelLayout)) parts.Add(a.ChannelLayout!);
        else if (a.Channels > 0) parts.Add(a.Channels + " ch");
        if (a.BitRate is > 0) parts.Add(Formatting.Bitrate(a.BitRate, nf));
        if (!string.IsNullOrEmpty(a.Language) && a.Language != "und") parts.Add(a.Language!);
        return string.Join(" · ", parts);
    }
}
