using SkiaSharp;
using VideoThumbnailer.Core.Extraction;
using VideoThumbnailer.Core.Ffmpeg;
using VideoThumbnailer.Core.Input;
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

    /// <summary>
    /// Target file after overwrite handling: null = it exists and is to be skipped.
    /// </summary>
    private static string? Target(SheetSettings s, string path)
    {
        if (!File.Exists(path)) return path;
        return s.Output.Overwrite switch
        {
            OverwriteMode.Skip => null,
            OverwriteMode.Rename => OutputPathResolver.MakeUnique(path),
            _ => path,
        };
    }

    /// <summary>
    /// Sheet and/or preview clip (#31) for one video. The result names the sheet (or the clip, when only the clip is
    /// written); a clip next to a sheet is mentioned in the message.
    /// </summary>
    /// <param name="manualTimes">Capture times chosen by hand for this video, by tile in timeline order (null = automatic).</param>
    public async Task<SheetResult> GenerateAsync(string videoPath, string? inputRoot, SheetSettings settings, IProgress<double>? progress, CancellationToken ct,
                                                 IReadOnlyList<ManualTime?>? manualTimes = null)
    {
        var s = settings;
        var wantSheet = !(s.Trailer.Enabled && s.Trailer.Only);
        var sheetPath = OutputPathResolver.ResolveSheetPath(s, videoPath, inputRoot);
        var sheetTarget = wantSheet ? Target(s, sheetPath) : null;
        var trailerTarget = s.Trailer.Enabled ? Target(s, OutputPathResolver.ResolveTrailerPath(s, videoPath, inputRoot)) : null;
        if (sheetTarget is null && trailerTarget is null)
            return new SheetResult(JobStatus.Skipped, wantSheet ? sheetPath : OutputPathResolver.ResolveTrailerPath(s, videoPath, inputRoot), CoreText.T("SheetExists"));

        await WaitIfPausedAsync(ct).ConfigureAwait(false);
        MediaInfo info;
        await processLimiter.WaitAsync(ct).ConfigureAwait(false);
        try { info = await FfprobeReader.ReadAsync(tools.FfprobePath, videoPath, ct).ConfigureAwait(false); }
        finally { processLimiter.Release(); }

        if (info.Video is null) return new SheetResult(JobStatus.Failed, null, CoreText.T("NoVideoStream"));
        progress?.Report(0.05);

        // Share of the progress bar for the sheet when a clip follows.
        var sheetShare = trailerTarget is null ? 1.0 : sheetTarget is null ? 0.05 : 0.6;
        SheetResult? sheet = null;
        if (sheetTarget is not null)
        {
            sheet = await GenerateSheetAsync(videoPath, s, info, sheetTarget, Scaled(progress, 0, sheetShare), ct, manualTimes).ConfigureAwait(false);
            if (sheet.Status != JobStatus.Done || trailerTarget is null) return sheet;
        }
        if (trailerTarget is null) return sheet!;

        var trailer = await new TrailerGenerator(tools.FfmpegPath, processLimiter, WaitIfPausedAsync)
            .GenerateAsync(s, info, trailerTarget, Scaled(progress, sheetShare, 1), ct).ConfigureAwait(false);
        progress?.Report(1);
        if (sheet is null) return trailer;
        if (trailer.Status != JobStatus.Done)
            return new SheetResult(JobStatus.Failed, sheet.SheetPath, CoreText.T("TrailerFailed", trailer.Message));
        return sheet with { Message = Join(sheet.Message, CoreText.T("TrailerWritten", trailer.SheetPath), trailer.Message) };
    }

    private static string? Join(params string?[] parts)
    {
        var list = parts.Where(p => !string.IsNullOrEmpty(p)).ToList();
        return list.Count > 0 ? string.Join("; ", list) : null;
    }

    private static IProgress<double>? Scaled(IProgress<double>? p, double from, double to) =>
        p is null ? null : new Progress(v => p.Report(from + (to - from) * v));

    private sealed class Progress(Action<double> report) : IProgress<double>
    {
        public void Report(double value) => report(value);
    }

    private async Task<SheetResult> GenerateSheetAsync(string videoPath, SheetSettings s, MediaInfo info, string target, IProgress<double>? progress,
                                                       CancellationToken ct, IReadOnlyList<ManualTime?>? manualTimes)
    {
        var aspect = info.Video!.DisplayAspect;
        var seed = StringComparer.Ordinal.GetHashCode(VideoSource.FileName(videoPath));
        var layout = GridLayout.Create(s, aspect, SheetRenderer.MeasureHeader(s, info), new Random(seed));
        if (s.Animation.Enabled)
        {
            // Leave room for the clip at the end so the last tile does not run out of video.
            var clipShare = info.Duration > 0 ? 100.0 * s.Animation.ClipSeconds / info.Duration : 0;
            var animTimes = TimestampPlanner.Plan(info.Duration, layout.Tiles.Count, s.SkipStartPercent,
                                                  Math.Min(45, s.SkipEndPercent + clipShare), manualTimes);
            return await GenerateAnimatedAsync(s, info, layout, animTimes, target, progress, ct).ConfigureAwait(false);
        }

        var plan = TimestampPlanner.Plan(info.Duration, layout.Tiles.Count, s.SkipStartPercent, s.SkipEndPercent, manualTimes);
        // Actual capture times: a black frame may be replaced by one close by (times chosen by hand are kept).
        var times = plan.Select(p => p.Time).ToArray();
        var search = s.SkipBlackFrames ? s.BlackFrameSearchSeconds : 0;

        var frames = new CapturedFrame[times.Length];
        var fullFrames = s.Output.SaveFrames ? new SKBitmap?[times.Length] : null;
        var done = 0;
        try
        {
            var tasks = plan.Select(async (p, i) =>
            {
                await WaitIfPausedAsync(ct).ConfigureAwait(false);
                await processLimiter.WaitAsync(ct).ConfigureAwait(false);
                SKBitmap? bmp;
                var t = p.Time;
                var window = p.Manual ? 0 : search;
                var (fw, fh) = layout.FrameSize(i);
                try
                {
                    if (fullFrames is not null)
                    {
                        (var full, t) = await _extractor.ExtractNonBlankAsync(info, t, null, null, window, ct).ConfigureAwait(false);
                        fullFrames[i] = full;
                        bmp = full?.Resize(new SKImageInfo(fw, fh), new SKSamplingOptions(SKCubicResampler.Mitchell));
                    }
                    else
                    {
                        (bmp, t) = await _extractor.ExtractNonBlankAsync(info, t, fw, fh, window, ct).ConfigureAwait(false);
                    }
                }
                finally { processLimiter.Release(); }
                times[i] = t;
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
            if (s.Output.WebVtt)
                WebVttWriter.Write(target, frames.Select(f => f.Bitmap).ToList(), times, info.Duration, aspect, s.Output.WebVttTileWidth, s.Output.Quality);
            if (s.Output.Sidecar)
                WriteSidecar(videoPath, target, info, layout, times, plan, null);

            progress?.Report(1);
            var missing = frames.Count(f => f.Bitmap is null);
            return new SheetResult(JobStatus.Done, target, Notes(s, missing));
        }
        finally
        {
            foreach (var f in frames) f?.Bitmap?.Dispose();
            if (fullFrames is not null) foreach (var f in fullFrames) f?.Dispose();
        }
    }

    private async Task<SheetResult> GenerateAnimatedAsync(SheetSettings s, MediaInfo info, GridLayout layout, PlannedTime[] plan,
                                                          string target, IProgress<double>? progress, CancellationToken ct)
    {
        var a = s.Animation;
        var withAudio = a.Audio && AnimationSettings.SupportsAudio(a.Format) && info.Audio is not null;
        var encoders = await AnimationEncoder.GetEncodersAsync(tools.FfmpegPath, ct).ConfigureAwait(false);
        if (AnimationEncoder.MissingEncoder(s, withAudio, encoders) is { } missingEncoder)
            return new SheetResult(JobStatus.Failed, null, CoreText.T("EncoderMissing", missingEncoder));
        var times = plan.Select(p => p.Time).ToArray();
        var clips = new IReadOnlyList<byte[]>[times.Length];
        var search = s.SkipBlackFrames ? s.BlackFrameSearchSeconds : 0;
        var done = 0;
        var tasks = plan.Select(async (p, i) =>
        {
            await WaitIfPausedAsync(ct).ConfigureAwait(false);
            await processLimiter.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                var seconds = ClipLength(p, a);
                var (fw, fh) = layout.FrameSize(i);
                var clip = await _clips.ExtractAsync(info, p.Time, seconds, a.FramesPerSecond, fw, fh, ct).ConfigureAwait(false);
                // Clip starts on a black frame: start it at the nearest good frame instead.
                if (!p.Manual && search > 0 && clip.Count > 0 && BlankFrames.IsBlank(clip[0])
                    && await _extractor.FindNonBlankAsync(info, p.Time, search, fw, fh, ct).ConfigureAwait(false) is { } found)
                {
                    found.Bitmap.Dispose();
                    var moved = await _clips.ExtractAsync(info, found.Time, seconds, a.FramesPerSecond, fw, fh, ct).ConfigureAwait(false);
                    if (moved.Count > 0) { clip = moved; times[i] = found.Time; }
                }
                clips[i] = clip;
            }
            finally { processLimiter.Release(); }
            progress?.Report(0.05 + 0.6 * Interlocked.Increment(ref done) / times.Length);
        }).ToList();
        await Task.WhenAll(tasks).ConfigureAwait(false);

        if (clips.All(c => c.Count == 0))
            return new SheetResult(JobStatus.Failed, null, CoreText.T("NoFrameDecoded"));

        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        var tmp = target + ".part";
        // Clips may have their own length (chosen by hand): the animation runs as long as the longest one.
        var frameCount = plan.Max(p => FrameCount(ClipLength(p, a), a.FramesPerSecond));
        AudioClip? audio = null;
        if (withAudio && AudioTile(layout, clips) is { } at)
            audio = new AudioClip(info.Path, times[at], Math.Min(ClipLength(plan[at], a), (double)frameCount / a.FramesPerSecond));
        await processLimiter.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await using (var encoder = AnimationEncoder.Start(tools.FfmpegPath, tmp, s, layout.SheetWidth, layout.SheetHeight, audio, encoders))
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

        if (s.Output.WebVtt)
        {
            // First picture of every clip, like the still sheet would show it.
            var firsts = clips.Select(c => c.Count > 0 ? SKBitmap.Decode(c[0]) : null).ToList();
            try { WebVttWriter.Write(target, firsts, times, info.Duration, info.Video!.DisplayAspect, s.Output.WebVttTileWidth, s.Output.Quality); }
            finally { foreach (var b in firsts) b?.Dispose(); }
        }
        if (s.Output.Sidecar)
            WriteSidecar(info.Path, target, info, layout, times, plan, plan.Select(p => ClipLength(p, a)).ToList());
        progress?.Report(1);
        var missing = clips.Count(c => c.Count == 0);
        return new SheetResult(JobStatus.Done, target, Notes(s, missing));
    }

    private static void WriteSidecar(string videoPath, string target, MediaInfo info, GridLayout layout, IReadOnlyList<double> times,
                                     IReadOnlyList<PlannedTime> plan, IReadOnlyList<double>? clipSeconds) =>
        SheetSidecar.Create(videoPath, target, info.Duration, info.Video!.DisplaySize, layout, times, plan.Select(p => p.Manual).ToList(), clipSeconds)
            .Write(SheetSidecar.PathFor(target));

    /// <summary>
    /// Tile whose sound goes into the animation: the biggest one (special grids), of equal size the earliest;
    /// tiles without a clip are passed over. Null = none.
    /// </summary>
    internal static int? AudioTile(GridLayout layout, IReadOnlyList<IReadOnlyList<byte[]>> clips)
    {
        int? best = null;
        for (var i = 0; i < layout.Tiles.Count && i < clips.Count; i++)
        {
            if (clips[i].Count == 0) continue;
            if (best is not { } b || Area(layout.Tiles[i]) > Area(layout.Tiles[b])) best = i;
        }
        return best;
        static long Area(TileRect t) => (long)t.Width * t.Height;
    }

    /// <summary>Remarks for a finished sheet: missing frames, pictures that could not be read.</summary>
    private static string? Notes(SheetSettings s, int missingFrames)
    {
        var notes = new List<string>();
        if (missingFrames > 0) notes.Add(CoreText.T("FramesMissing", missingFrames));
        if (s.BackgroundImage.Enabled && ImageFileCache.Get(s.BackgroundImage.Path) is null)
            notes.Add(CoreText.T("BackgroundImageMissing", s.BackgroundImage.Path));
        if (s.Watermark.HasImage && ImageFileCache.Get(s.Watermark.ImagePath) is null)
            notes.Add(CoreText.T("WatermarkImageMissing", s.Watermark.ImagePath));
        return notes.Count > 0 ? string.Join("; ", notes) : null;
    }

    /// <summary>Clip length of one tile: chosen by hand or the setting, within the allowed range.</summary>
    internal static double ClipLength(PlannedTime p, AnimationSettings a) => Math.Clamp(p.ClipSeconds ?? a.ClipSeconds, 0.5, 30);

    private static int FrameCount(double seconds, int fps) => Math.Max(1, (int)Math.Round(seconds * fps));

    private static async Task SaveFramesAsync(SheetSettings s, string videoPath, string sheetPath, SKBitmap?[] frames, double[] times)
    {
        var dir = Path.Combine(Path.GetDirectoryName(sheetPath)!, OutputPathResolver.ExpandPattern(s.Output.FramesFolderPattern, videoPath, s));
        Directory.CreateDirectory(dir);
        var name = VideoSource.NameWithoutExtension(videoPath);
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
