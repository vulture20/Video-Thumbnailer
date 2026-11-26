using System.Collections.Concurrent;
using SkiaSharp;
using VideoThumbnailer.Core.Ffmpeg;
using VideoThumbnailer.Core.Layout;
using VideoThumbnailer.Core.Media;

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
    private readonly ConcurrentDictionary<(string Path, long Ms), SKBitmap> _frames = new();
    private readonly ConcurrentQueue<(string Path, long Ms)> _order = new();
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
        try
        {
            var fi = new FileInfo(path);
            return fi.Exists ? (fi.Length, fi.LastWriteTimeUtc) : (-1, DateTime.MinValue);
        }
        catch { return (-1, DateTime.MinValue); }
    }

    private static long Key(double time) => (long)Math.Round(time * 1000);

    /// <summary>A cached frame (null if it has not been extracted yet). The bitmap stays owned by the cache.</summary>
    public SKBitmap? TryGetFrame(string path, double time) => _frames.TryGetValue((path, Key(time)), out var b) ? b : null;

    /// <summary>Extracts every frame that is not cached yet; <paramref name="onFrame"/> runs after each new one.</summary>
    public async Task LoadFramesAsync(MediaInfo info, IReadOnlyList<double> times, Action? onFrame, CancellationToken ct)
    {
        if (info.Video is null) return;
        var w = FrameWidth;
        var h = GridLayout.ComputeTileHeight(w, info.Video.DisplayAspect);
        var todo = times.Where(t => TryGetFrame(info.Path, t) is null).Distinct().ToList();
        await Task.WhenAll(todo.Select(async t =>
        {
            await _limiter.WaitAsync(ct).ConfigureAwait(false);
            SKBitmap? bmp;
            try
            {
                ct.ThrowIfCancellationRequested();
                bmp = await _extractor.ExtractAsync(info, t, w, h, ct).ConfigureAwait(false);
            }
            finally { _limiter.Release(); }
            if (bmp is null) return;
            var key = (info.Path, Key(t));
            // The file was forgotten (changed) while this frame was extracted: do not cache a stale frame.
            if (TryGetInfoUnchecked(info.Path) != info || !_frames.TryAdd(key, bmp)) { bmp.Dispose(); return; }
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
