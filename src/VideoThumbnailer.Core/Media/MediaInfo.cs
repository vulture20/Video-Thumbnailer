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
}
