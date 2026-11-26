using System.Diagnostics;
using System.Globalization;
using SkiaSharp;
using VideoThumbnailer.Core.Settings;

namespace VideoThumbnailer.Core.Rendering;

/// <summary>Feeds rendered sheet frames (raw RGBA) to an ffmpeg process that encodes MP4, WebM or GIF.</summary>
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

    public static AnimationEncoder Start(string ffmpegPath, string outputPath, SheetSettings s, int width, int height)
    {
        var psi = new ProcessStartInfo(ffmpegPath)
        {
            RedirectStandardInput = true,
            RedirectStandardError = true,
            RedirectStandardOutput = false,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var a in BuildArguments(outputPath, s, width, height)) psi.ArgumentList.Add(a);
        var process = Process.Start(psi) ?? throw new InvalidOperationException("ffmpeg could not be started.");
        return new AnimationEncoder(process, width, height);
    }

    internal static List<string> BuildArguments(string outputPath, SheetSettings s, int width, int height)
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
                               "-b:v", "0", "-row-mt", "1", "-pix_fmt", "yuv420p", "-f", "webm"]);
                break;
            default:
                args.AddRange(["-vf", pad, "-c:v", "libx264", "-preset", "medium",
                               "-crf", Math.Clamp((int)Math.Round(51 - q * 0.33), 16, 40).ToString(inv),
                               "-pix_fmt", "yuv420p", "-movflags", "+faststart", "-f", "mp4"]);
                break;
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
