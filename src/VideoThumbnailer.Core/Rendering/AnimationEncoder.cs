using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using SkiaSharp;
using VideoThumbnailer.Core.Settings;

namespace VideoThumbnailer.Core.Rendering;

/// <summary>Sound for an animated sheet: a part of the video (#30).</summary>
public sealed record AudioClip(string Path, double Start, double Seconds);

/// <summary>Feeds rendered sheet frames (raw RGBA) to an ffmpeg process that encodes the animation (MP4, WebM, GIF, …).</summary>
public sealed class AnimationEncoder : IAsyncDisposable
{
    private readonly Process _process;
    private readonly Stream _stdin;
    private readonly Task<string> _stderr;
    private readonly int _width, _height;

    private AnimationEncoder(Process process, int width, int height)
    {
        _process = process;
        _stdin = process.StandardInput.BaseStream;
        _stderr = process.StandardError.ReadToEndAsync();
        _width = width;
        _height = height;
    }

    /// <param name="encoders">Encoders of this ffmpeg (<see cref="GetEncodersAsync"/>); null = assume the preferred ones.</param>
    public static AnimationEncoder Start(string ffmpegPath, string outputPath, SheetSettings s, int width, int height,
                                         AudioClip? audio = null, IReadOnlySet<string>? encoders = null)
    {
        var psi = new ProcessStartInfo(ffmpegPath)
        {
            RedirectStandardInput = true,
            RedirectStandardError = true,
            RedirectStandardOutput = false,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var a in BuildArguments(outputPath, s, width, height, audio, encoders)) psi.ArgumentList.Add(a);
        var process = Process.Start(psi) ?? throw new InvalidOperationException("ffmpeg could not be started.");
        return new AnimationEncoder(process, width, height);
    }

    private static readonly ConcurrentDictionary<string, Task<IReadOnlySet<string>>> EncoderCache = new(StringComparer.Ordinal);

    /// <summary>Names of the encoders this ffmpeg has (<c>ffmpeg -encoders</c>), read once per program run.</summary>
    public static Task<IReadOnlySet<string>> GetEncodersAsync(string ffmpegPath, CancellationToken ct = default) =>
        EncoderCache.GetOrAdd(ffmpegPath, async path =>
        {
            try
            {
                var r = await Util.ProcessRunner.RunAsync(path, ["-hide_banner", "-encoders"], CancellationToken.None).ConfigureAwait(false);
                return ParseEncoders(System.Text.Encoding.UTF8.GetString(r.StdOut));
            }
            catch { return (IReadOnlySet<string>)new HashSet<string>(); }
        });

    internal static IReadOnlySet<string> ParseEncoders(string listing)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        var started = false;
        foreach (var raw in listing.Split('\n'))
        {
            var line = raw.Trim();
            if (!started) { started = line.StartsWith("---", StringComparison.Ordinal); continue; }
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2 && parts[0].Length == 6) set.Add(parts[1]);
        }
        return set;
    }

    /// <summary>Video encoder for the format; for WebP the older single-image encoder also writes animations.</summary>
    internal static string VideoEncoder(AnimationFormat f, IReadOnlySet<string>? have) => f switch
    {
        AnimationFormat.Gif => "gif",
        AnimationFormat.Webm => "libvpx-vp9",
        AnimationFormat.H265 => "libx265",
        AnimationFormat.Webp => have is not null && !have.Contains("libwebp_anim") && have.Contains("libwebp") ? "libwebp" : "libwebp_anim",
        AnimationFormat.Apng => "apng",
        AnimationFormat.Ogv => "libtheora",
        _ => "libx264",
    };

    /// <summary>Audio encoder for the format: AAC in MP4, Opus (or Vorbis) in WebM, Vorbis (or Opus) in Ogg.</summary>
    internal static string? AudioEncoder(AnimationFormat f, IReadOnlySet<string>? have) => f switch
    {
        AnimationFormat.Mp4 or AnimationFormat.H265 => "aac",
        AnimationFormat.Webm => have is not null && !have.Contains("libopus") && have.Contains("libvorbis") ? "libvorbis" : "libopus",
        AnimationFormat.Ogv => have is not null && !have.Contains("libvorbis") && have.Contains("libopus") ? "libopus" : "libvorbis",
        _ => null,
    };

    /// <summary>The encoder this ffmpeg lacks for the settings (video first, then sound), or null when all are there.</summary>
    public static string? MissingEncoder(SheetSettings s, bool withAudio, IReadOnlySet<string> have)
    {
        if (have.Count == 0) return null; // listing failed: let ffmpeg report the problem itself
        var video = VideoEncoder(s.Animation.Format, have);
        if (!have.Contains(video)) return video;
        if (withAudio && AudioEncoder(s.Animation.Format, have) is { } audio && !have.Contains(audio)) return audio;
        return null;
    }

    internal static List<string> BuildArguments(string outputPath, SheetSettings s, int width, int height,
                                                AudioClip? audio = null, IReadOnlySet<string>? encoders = null)
    {
        var a = s.Animation;
        var inv = CultureInfo.InvariantCulture;
        var args = new List<string>
        {
            "-hide_banner", "-loglevel", "error", "-y",
            "-f", "rawvideo", "-pix_fmt", "rgba",
            "-s", $"{width}x{height}", "-r", a.FramesPerSecond.ToString(inv),
            "-i", "-",
        };
        var audioCodec = audio is not null ? AudioEncoder(a.Format, encoders) : null;
        if (audioCodec is not null)
        {
            args.AddRange(["-ss", audio!.Start.ToString("0.###", inv), "-t", audio.Seconds.ToString("0.###", inv)]);
            args.AddRange(Input.VideoSource.InputOptions(audio.Path));
            args.AddRange(["-i", audio.Path, "-map", "0:v:0", "-map", "1:a:0?"]);
        }
        var q = Math.Clamp(s.Output.Quality, 1, 100);
        var bg = SheetRenderer.ParseColor(s.BackgroundColor, new SKColor(30, 30, 30));
        // yuv420p needs even dimensions: pad by one pixel in the background colour if necessary.
        var pad = $"pad=ceil(iw/2)*2:ceil(ih/2)*2:color=0x{bg.Red:X2}{bg.Green:X2}{bg.Blue:X2}";
        switch (a.Format)
        {
            case AnimationFormat.Gif:
                args.AddRange(["-vf", "split[a][b];[a]palettegen=stats_mode=diff[p];[b][p]paletteuse=dither=bayer:bayer_scale=3:diff_mode=rectangle",
                               "-loop", "0", "-f", "gif"]);
                break;
            case AnimationFormat.Webm:
                args.AddRange(["-vf", pad, "-c:v", "libvpx-vp9", "-crf", Math.Clamp((int)Math.Round(63 - q * 0.4), 15, 55).ToString(inv),
                               "-b:v", "0", "-row-mt", "1", "-pix_fmt", "yuv420p"]);
                break;
            case AnimationFormat.H265:
                args.AddRange(["-vf", pad, "-c:v", "libx265", "-preset", "medium",
                               "-crf", Math.Clamp((int)Math.Round(55 - q * 0.33), 18, 44).ToString(inv),
                               "-x265-params", "log-level=error", "-tag:v", "hvc1", "-pix_fmt", "yuv420p", "-movflags", "+faststart"]);
                break;
            case AnimationFormat.Webp:
                args.AddRange(["-c:v", VideoEncoder(a.Format, encoders), "-lossless", "0", "-quality", q.ToString(inv),
                               "-compression_level", "4", "-loop", "0", "-pix_fmt", "yuv420p", "-f", "webp"]);
                break;
            case AnimationFormat.Apng:
                args.AddRange(["-c:v", "apng", "-pred", "mixed", "-plays", "0", "-pix_fmt", "rgb24", "-f", "apng"]);
                break;
            case AnimationFormat.Ogv:
                args.AddRange(["-vf", pad, "-c:v", "libtheora", "-q:v", Math.Clamp((int)Math.Round(q / 10.0), 1, 10).ToString(inv),
                               "-pix_fmt", "yuv420p"]);
                break;
            default:
                args.AddRange(["-vf", pad, "-c:v", "libx264", "-preset", "medium",
                               "-crf", Math.Clamp((int)Math.Round(51 - q * 0.33), 16, 40).ToString(inv),
                               "-pix_fmt", "yuv420p", "-movflags", "+faststart"]);
                break;
        }
        if (audioCodec is not null)
        {
            args.AddRange(audioCodec switch
            {
                "aac" => ["-c:a", "aac", "-b:a", "128k"],
                "libopus" => ["-c:a", "libopus", "-b:a", "96k"],
                _ => (string[])["-c:a", audioCodec, "-q:a", "4"],
            });
        }
        // Container last (the .part file has no telling extension).
        switch (a.Format)
        {
            case AnimationFormat.Webm: args.AddRange(["-f", "webm"]); break;
            case AnimationFormat.Ogv: args.AddRange(["-f", "ogg"]); break;
            case AnimationFormat.Mp4 or AnimationFormat.H265: args.AddRange(["-f", "mp4"]); break;
        }
        args.Add(outputPath);
        return args;
    }

    public async Task WriteFrameAsync(SKBitmap frame, CancellationToken ct)
    {
        if (frame.Width != _width || frame.Height != _height || frame.ColorType != SKColorType.Rgba8888)
            throw new ArgumentException("Frame does not match the encoder format.", nameof(frame));
        var bytes = frame.GetPixelSpan().ToArray();
        await _stdin.WriteAsync(bytes, ct).ConfigureAwait(false);
    }

    /// <summary>Closes the input and waits for ffmpeg. Throws with ffmpeg's message if encoding failed.</summary>
    public async Task FinishAsync(CancellationToken ct)
    {
        await _stdin.DisposeAsync().ConfigureAwait(false);
        await _process.WaitForExitAsync(ct).ConfigureAwait(false);
        var err = await _stderr.ConfigureAwait(false);
        if (_process.ExitCode != 0)
            throw new InvalidOperationException($"ffmpeg ({_process.ExitCode}): {err.Trim()}");
    }

    public async ValueTask DisposeAsync()
    {
        try { await _stdin.DisposeAsync().ConfigureAwait(false); } catch { /* already closed */ }
        try { if (!_process.HasExited) _process.Kill(entireProcessTree: true); } catch { /* ignore */ }
        _process.Dispose();
    }
}
