using SkiaSharp;
using VideoThumbnailer.Core.Extraction;
using VideoThumbnailer.Core.Ffmpeg;
using VideoThumbnailer.Core.Layout;
using VideoThumbnailer.Core.Localization;
using VideoThumbnailer.Core.Media;
using VideoThumbnailer.Core.Output;
using VideoThumbnailer.Core.Rendering;
using VideoThumbnailer.Core.Settings;

namespace VideoThumbnailer.Core.Batch;

public sealed record SheetResult(JobStatus Status, string? SheetPath, string? Message);

/// <summary>Creates one sheet for one video: probe, plan, extract (parallel), render, save.</summary>
public sealed class SheetGenerator(FfmpegTools tools, SemaphoreSlim processLimiter, PauseGate? pause = null)
{
    private readonly FrameExtractor _extractor = new(tools.FfmpegPath);
    private readonly ClipExtractor _clips = new(tools.FfmpegPath);

    public async Task<SheetResult> GenerateAsync(string videoPath, string? inputRoot, SheetSettings settings, IProgress<double>? progress, CancellationToken ct)
    {
        var s = settings;
        var target = OutputPathResolver.ResolveSheetPath(s, videoPath, inputRoot);
        if (File.Exists(target))
        {
            switch (s.Output.Overwrite)
            {
                case OverwriteMode.Skip: return new SheetResult(JobStatus.Skipped, target, CoreText.T("SheetExists"));
                case OverwriteMode.Rename: target = OutputPathResolver.MakeUnique(target); break;
            }
        }

        await WaitIfPausedAsync(ct).ConfigureAwait(false);
        MediaInfo info;
        await processLimiter.WaitAsync(ct).ConfigureAwait(false);
        try { info = await FfprobeReader.ReadAsync(tools.FfprobePath, videoPath, ct).ConfigureAwait(false); }
        finally { processLimiter.Release(); }

        if (info.Video is null) return new SheetResult(JobStatus.Failed, null, CoreText.T("NoVideoStream"));
        progress?.Report(0.05);

        var aspect = info.Video.DisplayAspect;
        var seed = StringComparer.Ordinal.GetHashCode(Path.GetFileName(videoPath));
        var layout = GridLayout.Create(s, aspect, SheetRenderer.MeasureHeader(s, info), new Random(seed));
        if (s.Animation.Enabled)
        {
            // Leave room for the clip at the end so the last tile does not run out of video.
            var clipShare = info.Duration > 0 ? 100.0 * s.Animation.ClipSeconds / info.Duration : 0;
            var animTimes = TimestampPlanner.Plan(info.Duration, layout.Tiles.Count, s.SkipStartPercent,
                                                  Math.Min(45, s.SkipEndPercent + clipShare));
            return await GenerateAnimatedAsync(s, info, layout, animTimes, target, progress, ct).ConfigureAwait(false);
        }

        var times = TimestampPlanner.Plan(info.Duration, layout.Tiles.Count, s.SkipStartPercent, s.SkipEndPercent);

        var frames = new CapturedFrame[times.Length];
        var fullFrames = s.Output.SaveFrames ? new SKBitmap?[times.Length] : null;
        var done = 0;
        try
        {
            var tasks = times.Select(async (t, i) =>
            {
                await WaitIfPausedAsync(ct).ConfigureAwait(false);
                await processLimiter.WaitAsync(ct).ConfigureAwait(false);
                SKBitmap? bmp;
                try
                {
                    if (fullFrames is not null)
                    {
                        var full = await _extractor.ExtractAsync(info, t, null, null, ct).ConfigureAwait(false);
                        fullFrames[i] = full;
                        bmp = full?.Resize(new SKImageInfo(layout.TileWidth, layout.TileHeight), new SKSamplingOptions(SKCubicResampler.Mitchell));
                    }
                    else
                    {
                        bmp = await _extractor.ExtractAsync(info, t, layout.TileWidth, layout.TileHeight, ct).ConfigureAwait(false);
                    }
                }
                finally { processLimiter.Release(); }
                frames[i] = new CapturedFrame(t, bmp);
                progress?.Report(0.05 + 0.85 * Interlocked.Increment(ref done) / times.Length);
            }).ToList();
            await Task.WhenAll(tasks).ConfigureAwait(false);

            if (frames.All(f => f.Bitmap is null))
                return new SheetResult(JobStatus.Failed, null, CoreText.T("NoFrameDecoded"));

            ct.ThrowIfCancellationRequested();
            using var sheet = SheetRenderer.Render(s, info, layout, frames);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            await SaveAtomicAsync(target, stream => SheetRenderer.Encode(sheet, stream, s.Output.Format, s.Output.Quality)).ConfigureAwait(false);

            if (fullFrames is not null)
                await SaveFramesAsync(s, videoPath, target, fullFrames, times).ConfigureAwait(false);

            progress?.Report(1);
            var missing = frames.Count(f => f.Bitmap is null);
            return new SheetResult(JobStatus.Done, target, missing > 0 ? CoreText.T("FramesMissing", missing) : null);
        }
        finally
        {
            foreach (var f in frames) f?.Bitmap?.Dispose();
            if (fullFrames is not null) foreach (var f in fullFrames) f?.Dispose();
        }
    }

    private async Task<SheetResult> GenerateAnimatedAsync(SheetSettings s, MediaInfo info, GridLayout layout, double[] times,
                                                          string target, IProgress<double>? progress, CancellationToken ct)
    {
        var a = s.Animation;
        var clips = new IReadOnlyList<byte[]>[times.Length];
        var done = 0;
        var tasks = times.Select(async (t, i) =>
        {
            await WaitIfPausedAsync(ct).ConfigureAwait(false);
            await processLimiter.WaitAsync(ct).ConfigureAwait(false);
            try { clips[i] = await _clips.ExtractAsync(info, t, a.ClipSeconds, a.FramesPerSecond, layout.TileWidth, layout.TileHeight, ct).ConfigureAwait(false); }
            finally { processLimiter.Release(); }
            progress?.Report(0.05 + 0.6 * Interlocked.Increment(ref done) / times.Length);
        }).ToList();
        await Task.WhenAll(tasks).ConfigureAwait(false);

        if (clips.All(c => c.Count == 0))
            return new SheetResult(JobStatus.Failed, null, CoreText.T("NoFrameDecoded"));

        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        var tmp = target + ".part";
        var frameCount = a.FrameCount;
        await processLimiter.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await using (var encoder = AnimationEncoder.Start(tools.FfmpegPath, tmp, s, layout.SheetWidth, layout.SheetHeight))
            {
                var current = new SKBitmap?[clips.Length];
                try
                {
                    for (var k = 0; k < frameCount; k++)
                    {
                        await WaitIfPausedAsync(ct).ConfigureAwait(false);
                        var frames = new CapturedFrame[clips.Length];
                        for (var i = 0; i < clips.Length; i++)
                        {
                            // Short clips (end of video) keep showing their last frame.
                            if (k < clips[i].Count)
                            {
                                current[i]?.Dispose();
                                current[i] = SKBitmap.Decode(clips[i][k]);
                            }
                            frames[i] = new CapturedFrame(times[i] + (double)Math.Min(k, Math.Max(0, clips[i].Count - 1)) / a.FramesPerSecond, current[i]);
                        }
                        using var sheet = SheetRenderer.Render(s, info, layout, frames);
                        await encoder.WriteFrameAsync(sheet, ct).ConfigureAwait(false);
                        progress?.Report(0.65 + 0.35 * (k + 1) / frameCount);
                    }
                }
                finally
                {
                    foreach (var b in current) b?.Dispose();
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

        progress?.Report(1);
        var missing = clips.Count(c => c.Count == 0);
        return new SheetResult(JobStatus.Done, target, missing > 0 ? CoreText.T("FramesMissing", missing) : null);
    }

    private static async Task SaveFramesAsync(SheetSettings s, string videoPath, string sheetPath, SKBitmap?[] frames, double[] times)
    {
        var dir = Path.Combine(Path.GetDirectoryName(sheetPath)!, OutputPathResolver.ExpandPattern(s.Output.FramesFolderPattern, videoPath, s));
        Directory.CreateDirectory(dir);
        var name = Path.GetFileNameWithoutExtension(videoPath);
        var ext = SheetRenderer.Extension(s.Output.Format);
        for (var i = 0; i < frames.Length; i++)
        {
            if (frames[i] is not { } bmp) continue;
            var stamp = TimeSpan.FromSeconds(times[i]).ToString(@"hh\-mm\-ss");
            var file = Path.Combine(dir, $"{name}_{i + 1:000}_{stamp}{ext}");
            await SaveAtomicAsync(file, stream => SheetRenderer.Encode(bmp, stream, s.Output.Format, s.Output.Quality)).ConfigureAwait(false);
        }
    }

    private static async Task SaveAtomicAsync(string path, Action<Stream> write)
    {
        var tmp = path + ".part";
        await using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
            write(fs);
        File.Move(tmp, path, overwrite: true);
    }

    private Task WaitIfPausedAsync(CancellationToken ct) => pause?.WaitAsync(ct) ?? Task.CompletedTask;
}
