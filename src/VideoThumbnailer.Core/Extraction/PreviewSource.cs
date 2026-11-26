using System.Collections.Concurrent;
using SkiaSharp;
using VideoThumbnailer.Core.Ffmpeg;
using VideoThumbnailer.Core.Layout;
using VideoThumbnailer.Core.Media;
using VideoThumbnailer.Core.Rendering;

namespace VideoThumbnailer.Core.Extraction;

/// <summary>
/// Real video data for the live preview: video information (ffprobe) and small frames (ffmpeg), both cached,
/// so changing settings only extracts frames for capture times that are new. A file whose size or
/// modification time changed since it was read is forgotten and read again.
/// </summary>
public sealed class PreviewSource(FfmpegTools tools, int maxParallel = 4)
{
    /// <summary>Width of cached preview frames; the preview image is at most 1200 px wide, so this is enough.</summary>
    public const int FrameWidth = 320;
    /// <summary>Cached frames over all videos; older ones are dropped beyond this (about 50 MB at 16:9).</summary>
    public const int MaxCachedFrames = 200;

    private readonly FrameExtractor _extractor = new(tools.FfmpegPath);
    private readonly SemaphoreSlim _limiter = new(Math.Max(1, maxParallel));
    private readonly ConcurrentDictionary<string, Task<MediaInfo>> _info = new(StringComparer.Ordinal);
    // Key: file, planned time, search window for black frames (0 = off); value: frame and the time actually used.
    private readonly ConcurrentDictionary<(string Path, long Ms, long SearchMs), CapturedFrame> _frames = new();
    private readonly ConcurrentQueue<(string Path, long Ms, long SearchMs)> _order = new();
    private readonly ConcurrentDictionary<string, (long Length, DateTime Written)> _stamps = new(StringComparer.Ordinal);

    public FfmpegTools Tools { get; } = tools;

    /// <summary>Video information, read once per file (a failed read is retried next time).</summary>
    public async Task<MediaInfo> GetInfoAsync(string path, CancellationToken ct)
    {
        ForgetIfChanged(path);
        var task = _info.GetOrAdd(path, p =>
        {
            _stamps[p] = Stamp(p);
            return FfprobeReader.ReadAsync(Tools.FfprobePath, p, CancellationToken.None);
        });
        try { return await task.WaitAsync(ct).ConfigureAwait(false); }
        catch when (task.IsFaulted || task.IsCanceled) { _info.TryRemove(new(path, task)); throw; }
    }

    /// <summary>Video information if it has already been read successfully.</summary>
    public MediaInfo? TryGetInfo(string path)
    {
        ForgetIfChanged(path);
        return _info.TryGetValue(path, out var t) && t.IsCompletedSuccessfully ? t.Result : null;
    }

    /// <summary>
    /// Forgets information and frames of <paramref name="path"/> when its size or modification time differs from
    /// when it was read (file replaced, re-encoded, still being written …). Returns true if something was forgotten.
    /// </summary>
    public bool ForgetIfChanged(string path)
    {
        if (!_stamps.TryGetValue(path, out var known) || Stamp(path) == known) return false;
        Forget(path);
        return true;
    }

    private static (long, DateTime) Stamp(string path)
    {
        if (Input.VideoSource.IsUrl(path)) return (0, DateTime.MinValue); // remote: read once per session
        try
        {
            var fi = new FileInfo(path);
            return fi.Exists ? (fi.Length, fi.LastWriteTimeUtc) : (-1, DateTime.MinValue);
        }
        catch { return (-1, DateTime.MinValue); }
    }

    private static long Key(double time) => (long)Math.Round(time * 1000);

    /// <summary>
    /// A cached frame with the time it was taken at (null if it has not been extracted yet). With
    /// <paramref name="blackSearch"/> &gt; 0 a black frame was replaced by a good one nearby, as on the real sheet.
    /// The bitmap stays owned by the cache.
    /// </summary>
    public CapturedFrame? TryGetFrame(string path, double time, double blackSearch = 0) =>
        _frames.TryGetValue((path, Key(time), Key(blackSearch)), out var f) ? f : null;

    /// <summary>Frame for a planned time: black frames are only skipped for automatic times, as on the real sheet.</summary>
    public CapturedFrame? TryGetFrame(string path, PlannedTime planned, double blackSearch = 0) =>
        TryGetFrame(path, planned.Time, planned.Manual ? 0 : blackSearch);

    /// <summary>Extracts every frame that is not cached yet; <paramref name="onFrame"/> runs after each new one.</summary>
    public Task LoadFramesAsync(MediaInfo info, IReadOnlyList<double> times, Action? onFrame, CancellationToken ct) =>
        LoadFramesAsync(info, times.Select(t => new PlannedTime(t)).ToList(), 0, onFrame, ct);

    /// <summary>
    /// Extracts every frame that is not cached yet; <paramref name="onFrame"/> runs after each new one. Automatic
    /// times skip black frames within ±<paramref name="blackSearch"/> seconds (0 = off).
    /// </summary>
    public async Task LoadFramesAsync(MediaInfo info, IReadOnlyList<PlannedTime> times, double blackSearch, Action? onFrame, CancellationToken ct)
    {
        if (info.Video is null) return;
        var w = FrameWidth;
        var h = GridLayout.ComputeTileHeight(w, info.Video.DisplayAspect);
        var todo = times.Select(p => (p.Time, Search: p.Manual ? 0 : Math.Max(0, blackSearch)))
                        .Where(x => TryGetFrame(info.Path, x.Time, x.Search) is null).Distinct().ToList();
        await Task.WhenAll(todo.Select(async x =>
        {
            await _limiter.WaitAsync(ct).ConfigureAwait(false);
            SKBitmap? bmp;
            double actual;
            try
            {
                ct.ThrowIfCancellationRequested();
                (bmp, actual) = await _extractor.ExtractNonBlankAsync(info, x.Time, w, h, x.Search, ct).ConfigureAwait(false);
            }
            finally { _limiter.Release(); }
            if (bmp is null) return;
            var key = (info.Path, Key(x.Time), Key(x.Search));
            // The file was forgotten (changed) while this frame was extracted: do not cache a stale frame.
            if (TryGetInfoUnchecked(info.Path) != info || !_frames.TryAdd(key, new CapturedFrame(actual, bmp))) { bmp.Dispose(); return; }
            _order.Enqueue(key);
            Trim();
            onFrame?.Invoke();
        })).ConfigureAwait(false);
    }

    private MediaInfo? TryGetInfoUnchecked(string path) =>
        _info.TryGetValue(path, out var t) && t.IsCompletedSuccessfully ? t.Result : null;

    /// <summary>Forgets frames and information of one file (e.g. after it changed on disk).</summary>
    public void Forget(string path)
    {
        _info.TryRemove(path, out _);
        _stamps.TryRemove(path, out _);
        foreach (var k in _frames.Keys.Where(k => k.Path == path)) _frames.TryRemove(k, out _);
    }

    private void Trim()
    {
        // Dropped bitmaps are not disposed: a preview render may still be using them; the GC frees them.
        while (_frames.Count > MaxCachedFrames && _order.TryDequeue(out var old))
            _frames.TryRemove(old, out _);
    }
}
