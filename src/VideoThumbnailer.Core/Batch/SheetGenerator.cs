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
