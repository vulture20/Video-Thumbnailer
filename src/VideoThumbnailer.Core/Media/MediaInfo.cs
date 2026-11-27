namespace VideoThumbnailer.Core.Media;

public sealed class VideoStreamInfo
{
    public int Index { get; init; }
    public string Codec { get; init; } = "";
    public string? Profile { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }
    /// <summary>Sample aspect ratio (pixel aspect), 1.0 for square pixels.</summary>
    public double SampleAspect { get; init; } = 1.0;
    public double FrameRate { get; init; }
    public string? PixelFormat { get; init; }
    public string? ColorTransfer { get; init; }
    public string? ColorPrimaries { get; init; }
    public long? BitRate { get; init; }
    /// <summary>Rotation in degrees as signalled by the container (display matrix).</summary>
    public int Rotation { get; init; }

    public bool IsHdr => ColorTransfer is "smpte2084" or "arib-std-b67";

    /// <summary>Size as it is displayed (SAR and rotation applied).</summary>
    public (int Width, int Height) DisplaySize
    {
        get
        {
            var w = (int)Math.Round(Width * (SampleAspect > 0 ? SampleAspect : 1.0));
            var h = Height;
            var rot = ((Rotation % 360) + 360) % 360;
            return rot is 90 or 270 ? (h, w) : (w, h);
        }
    }

    public double DisplayAspect
    {
        get
        {
            var (w, h) = DisplaySize;
            return w > 0 && h > 0 ? (double)w / h : 16.0 / 9.0;
        }
    }
}

public sealed class AudioStreamInfo
{
    public int Index { get; init; }
    public string Codec { get; init; } = "";
    public int SampleRate { get; init; }
    public int Channels { get; init; }
    public string? ChannelLayout { get; init; }
    public long? BitRate { get; init; }
    public string? Language { get; init; }
}

/// <summary>A subtitle track (#35): a stream inside the container or a file next to the video.</summary>
public sealed class SubtitleStreamInfo
{
    /// <summary>Stream index in the container; -1 for an external file.</summary>
    public int Index { get; init; } = -1;
    /// <summary>ffprobe codec name (subrip, ass, hdmv_pgs_subtitle …) or, for files, the extension (srt, ass …).</summary>
    public string Codec { get; init; } = "";
    public string? Language { get; init; }
    public string? Title { get; init; }
    public bool Forced { get; init; }
    public bool HearingImpaired { get; init; }
    /// <summary>Path of the external file; null for a stream in the container.</summary>
    public string? ExternalPath { get; init; }
    public bool IsExternal => ExternalPath is not null;

    /// <summary>Short, readable format name: SRT, ASS, PGS, VobSub …</summary>
    public string FormatName => Codec.ToLowerInvariant() switch
    {
        "subrip" or "srt" => "SRT",
        "ass" => "ASS",
        "ssa" => "SSA",
        "webvtt" or "vtt" => "WebVTT",
        "hdmv_pgs_subtitle" or "sup" => "PGS",
        "dvd_subtitle" or "sub" or "idx" => "VobSub",
        "dvb_subtitle" => "DVB",
        "dvb_teletext" => "Teletext",
        "mov_text" => "TX3G",
        "eia_608" => "CEA-608",
        "microdvd" => "MicroDVD",
        "text" => "Text",
        "ttml" => "TTML",
        "" => "?",
        var c => c.ToUpperInvariant(),
    };
}

public sealed class MediaInfo
{
    public required string Path { get; init; }
    public long FileSize { get; init; }
    public double Duration { get; init; }
    public long? BitRate { get; init; }
    public string? ContainerFormat { get; init; }
    public string? Title { get; init; }
    public VideoStreamInfo? Video { get; init; }
    public IReadOnlyList<AudioStreamInfo> AudioStreams { get; init; } = [];
    public AudioStreamInfo? Audio => AudioStreams.Count > 0 ? AudioStreams[0] : null;
    /// <summary>Subtitle tracks: streams in the container first, then files next to the video (#35).</summary>
    public IReadOnlyList<SubtitleStreamInfo> SubtitleStreams { get; init; } = [];

    /// <summary>Copy with other subtitle tracks.</summary>
    public MediaInfo WithSubtitles(IReadOnlyList<SubtitleStreamInfo> subtitles) => new()
    {
        Path = Path, FileSize = FileSize, Duration = Duration, BitRate = BitRate, ContainerFormat = ContainerFormat,
        Title = Title, Video = Video, AudioStreams = AudioStreams, SubtitleStreams = subtitles,
    };
}
