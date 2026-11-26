using System.Collections.Concurrent;
using SkiaSharp;

namespace VideoThumbnailer.Core.Rendering;

/// <summary>
/// Decoded pictures (background, watermark), so animated sheets and the live preview do not read the file for every frame.
/// A changed file (time or size) is read again.
/// </summary>
public static class ImageFileCache
{
    private const int MaxEntries = 8;
    private static readonly ConcurrentDictionary<(string Path, long Stamp, long Length), SKImage?> Images = new();

    /// <summary>The picture, or null when the path is empty, the file is missing or not an image.</summary>
    public static SKImage? Get(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        FileInfo file;
        try
        {
            file = new FileInfo(Path.GetFullPath(Environment.ExpandEnvironmentVariables(path)));
            if (!file.Exists) return null;
        }
        catch { return null; }

        var key = (file.FullName, file.LastWriteTimeUtc.Ticks, file.Length);
        if (Images.TryGetValue(key, out var cached)) return cached;
        SKImage? image = null;
        try
        {
            using var bmp = SKBitmap.Decode(file.FullName);
            if (bmp is not null) image = SKImage.FromBitmap(bmp);
        }
        catch { image = null; }
        // Evicted pictures are not disposed: another render may still draw them; the GC frees them.
        if (Images.Count >= MaxEntries)
            foreach (var k in Images.Keys.Take(Images.Count - MaxEntries + 1)) Images.TryRemove(k, out _);
        return Images.GetOrAdd(key, image);
    }
}
