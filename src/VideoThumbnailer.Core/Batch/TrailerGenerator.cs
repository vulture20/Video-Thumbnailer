using SkiaSharp;
using VideoThumbnailer.Core.Extraction;
using VideoThumbnailer.Core.Layout;
using VideoThumbnailer.Core.Localization;
using VideoThumbnailer.Core.Media;
using VideoThumbnailer.Core.Rendering;
using VideoThumbnailer.Core.Settings;

namespace VideoThumbnailer.Core.Batch;

/// <summary>
/// Preview clip (#31): short parts from evenly spread places of the video, one after the other, with an optional
/// cross-fade and the time of each part in the corner. Encoded like animated sheets (frames piped to ffmpeg).
/// </summary>
internal sealed class TrailerGenerator(string ffmpegPath, SemaphoreSlim processLimiter, Func<CancellationToken, Task> waitIfPaused)
{
    private readonly FrameExtractor _extractor = new(ffmpegPath);
    private readonly ClipExtractor _clips = new(ffmpegPath);

    /// <summary>Picture size: the longer side is <paramref name="size"/>, both even (needed by yuv420p).</summary>
    internal static (int Width, int Height) FrameSize(double aspect, int size)
    {
        if (aspect <= 0) aspect = 16.0 / 9;
        double w = size, h = size;
        if (aspect >= 1) h = size / aspect; else w = size * aspect;
        return (Math.Max(2, (int)Math.Round(w / 2) * 2), Math.Max(2, (int)Math.Round(h / 2) * 2));
    }

    /// <summary>Frames of the cross-fade between two parts with <paramref name="a"/> and <paramref name="b"/> frames.</summary>
    internal static int FadeFrames(double seconds, int fps, int a, int b) =>
        Math.Max(0, Math.Min((int)Math.Round(seconds * fps), Math.Min(a, b) / 2));

    /// <summary>
    /// Order of output frames: (part, frame) of the picture and, during a cross-fade, the part fading in with its
    /// share (0..1). Part i starts after the frames already shown in the fade from part i-1.
    /// </summary>
    internal static List<(int Part, int Frame, int NextFrame, float Mix)> Sequence(IReadOnlyList<int> lengths, IReadOnlyList<int> fades)
    {
        var list = new List<(int, int, int, float)>();
        for (var i = 0; i < lengths.Count; i++)
        {
            var start = i > 0 ? fades[i - 1] : 0;
            var fade = i < lengths.Count - 1 ? fades[i] : 0;
            var end = lengths[i] - fade;
            for (var f = start; f < end; f++) list.Add((i, f, -1, 0));
            for (var k = 0; k < fade; k++) list.Add((i, end + k, k, (k + 1f) / (fade + 1)));
        }
        return list;
    }

    public async Task<SheetResult> GenerateAsync(SheetSettings s, MediaInfo info, string target, IProgress<double>? progress, CancellationToken ct)
    {
        var t = s.Trailer;
        var withAudio = t.Audio && AnimationSettings.SupportsAudio(t.Format) && info.Audio is not null;
        var encoders = await AnimationEncoder.GetEncodersAsync(ffmpegPath, ct).ConfigureAwait(false);
        if (AnimationEncoder.MissingEncoder(t.Format, withAudio, encoders) is { } missingEncoder)
            return new SheetResult(JobStatus.Failed, null, CoreText.T("EncoderMissing", missingEncoder));

        var (w, h) = FrameSize(info.Video!.DisplayAspect, t.Size);
        // Short videos: never more parts than fit side by side.
        var count = info.Duration > 0 ? Math.Max(1, Math.Min(t.Clips, (int)Math.Floor(info.Duration / t.ClipSeconds))) : 1;
        var clipShare = info.Duration > 0 ? 100.0 * t.ClipSeconds / info.Duration : 0;
        var plan = TimestampPlanner.Plan(info.Duration, count, s.SkipStartPercent, Math.Min(45, s.SkipEndPercent + clipShare));
        var times = plan.ToArray();
        var clips = new IReadOnlyList<byte[]>[count];
        var search = s.SkipBlackFrames ? s.BlackFrameSearchSeconds : 0;
        var done = 0;
        await Task.WhenAll(plan.Select(async (start, i) =>
        {
            await waitIfPaused(ct).ConfigureAwait(false);
            await processLimiter.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                var clip = await _clips.ExtractAsync(info, start, t.ClipSeconds, t.FramesPerSecond, w, h, ct).ConfigureAwait(false);
                if (search > 0 && clip.Count > 0 && BlankFrames.IsBlank(clip[0])
                    && await _extractor.FindNonBlankAsync(info, start, search, w, h, ct).ConfigureAwait(false) is { } found)
                {
                    found.Bitmap.Dispose();
                    var moved = await _clips.ExtractAsync(info, found.Time, t.ClipSeconds, t.FramesPerSecond, w, h, ct).ConfigureAwait(false);
                    if (moved.Count > 0) { clip = moved; times[i] = found.Time; }
                }
                clips[i] = clip;
            }
            finally { processLimiter.Release(); }
            progress?.Report(0.6 * Interlocked.Increment(ref done) / count);
        })).ConfigureAwait(false);

        var parts = Enumerable.Range(0, count).Where(i => clips[i].Count > 0).ToList();
        if (parts.Count == 0) return new SheetResult(JobStatus.Failed, null, CoreText.T("NoFrameDecoded"));
        var lengths = parts.Select(i => clips[i].Count).ToList();
        var fades = new List<int>();
        for (var k = 0; k + 1 < parts.Count; k++) fades.Add(FadeFrames(t.CrossfadeSeconds, t.FramesPerSecond, lengths[k], lengths[k + 1]));
        var sequence = Sequence(lengths, fades);

        List<AudioClip>? audio = withAudio
            ? parts.Select((i, k) => new AudioClip(info.Path, times[i], (double)lengths[k] / t.FramesPerSecond)).ToList()
            : null;
        var audioFades = fades.Select(f => (double)f / t.FramesPerSecond).ToList();

        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        var tmp = target + ".part";
        var options = new EncodeOptions(t.Format, t.FramesPerSecond, s.Output.Quality, SKColors.Black);
        await processLimiter.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await using (var encoder = AnimationEncoder.Start(ffmpegPath, tmp, options, w, h, audio, audioFades, encoders))
            {
                using var frame = new SKBitmap(new SKImageInfo(w, h, SKColorType.Rgba8888, SKAlphaType.Premul));
                using var canvas = new SKCanvas(frame);
                using var next = new SKBitmap(new SKImageInfo(w, h, SKColorType.Rgba8888, SKAlphaType.Premul));
                using var nextCanvas = new SKCanvas(next);
                for (var n = 0; n < sequence.Count; n++)
                {
                    await waitIfPaused(ct).ConfigureAwait(false);
                    var (part, f, nextFrame, mix) = sequence[n];
                    Draw(canvas, s, clips[parts[part]][f], times[parts[part]] + (double)f / t.FramesPerSecond, w, h);
                    if (nextFrame >= 0)
                    {
                        Draw(nextCanvas, s, clips[parts[part + 1]][nextFrame], times[parts[part + 1]] + (double)nextFrame / t.FramesPerSecond, w, h);
                        using var paint = new SKPaint { Color = SKColors.White.WithAlpha((byte)Math.Round(255 * mix)) };
                        using var img = SKImage.FromBitmap(next);
                        canvas.DrawImage(img, 0, 0, paint);
                    }
                    canvas.Flush();
                    await encoder.WriteFrameAsync(frame, ct).ConfigureAwait(false);
                    progress?.Report(0.6 + 0.4 * (n + 1) / sequence.Count);
                }
                await encoder.FinishAsync(ct).ConfigureAwait(false);
            }
            File.Move(tmp, target, overwrite: true);
        }
        catch
        {
            try { File.Delete(tmp); } catch { /* ignore */ }
            throw;
        }
        finally { processLimiter.Release(); }

        var missing = count - parts.Count;
        return new SheetResult(JobStatus.Done, target, missing > 0 ? CoreText.T("FramesMissing", missing) : null);
    }

    /// <summary>One frame of a part, scaled to the clip size, with its time in the corner.</summary>
    private static void Draw(SKCanvas canvas, SheetSettings s, byte[] jpeg, double time, int w, int h)
    {
        canvas.Clear(SKColors.Black);
        using (var bmp = SKBitmap.Decode(jpeg))
        {
            if (bmp is not null)
            {
                using var img = SKImage.FromBitmap(bmp);
                canvas.DrawImage(img, SKRect.Create(w, h), new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.None));
            }
        }
        if (s.Trailer.Timestamps) SheetRenderer.DrawTimestamp(canvas, SKRect.Create(w, h), s, time);
    }
}
