using System.Globalization;
using System.Text;
using SkiaSharp;
using VideoThumbnailer.Core.Rendering;

namespace VideoThumbnailer.Core.Output;

/// <summary>
/// Thumbnails for the seek bar of web players (#32): one sprite image with all captured frames in a plain grid and a
/// WebVTT file whose cues point at a part of it ("sprite.jpg#xywh=x,y,w,h").
/// </summary>
public static class WebVttWriter
{
    public const string Suffix = ".thumbnails";

    /// <summary>Paths of the WebVTT file and the sprite for a sheet: "movie.thumbnails.vtt" and "movie.thumbnails.jpg".</summary>
    public static (string Vtt, string Sprite) PathsFor(string sheetPath)
    {
        var dir = Path.GetDirectoryName(sheetPath) ?? ".";
        var name = Path.GetFileNameWithoutExtension(sheetPath);
        foreach (var marker in new[] { OutputPathResolver.AnimatedMarker, OutputPathResolver.TrailerMarker })
            if (name.EndsWith(marker, StringComparison.OrdinalIgnoreCase)) name = name[..^marker.Length];
        return (Path.Combine(dir, name + Suffix + ".vtt"), Path.Combine(dir, name + Suffix + ".jpg"));
    }

    /// <summary>Columns of the sprite: about square, at most 10.</summary>
    internal static int SpriteColumns(int count) => Math.Clamp((int)Math.Ceiling(Math.Sqrt(count)), 1, 10);

    /// <summary>
    /// Cue ranges: each frame stands for the time up to the middle between it and its neighbours; the first starts
    /// at 0, the last ends with the video. <paramref name="times"/> must be in timeline order.
    /// </summary>
    internal static (double Start, double End)[] CueRanges(IReadOnlyList<double> times, double duration)
    {
        var n = times.Count;
        var result = new (double, double)[n];
        var end = Math.Max(duration, n > 0 ? times[^1] : 0);
        for (var i = 0; i < n; i++)
        {
            var start = i == 0 ? 0 : (times[i - 1] + times[i]) / 2;
            var stop = i == n - 1 ? end : (times[i] + times[i + 1]) / 2;
            if (i > 0) start = Math.Max(start, result[i - 1].Item2);
            if (stop <= start) stop = start + 0.001;
            result[i] = (start, stop);
        }
        return result;
    }

    internal static string Timestamp(double seconds)
    {
        var t = TimeSpan.FromMilliseconds(Math.Round(Math.Max(0, seconds) * 1000));
        return $"{(int)t.TotalHours:00}:{t.Minutes:00}:{t.Seconds:00}.{t.Milliseconds:000}";
    }

    /// <summary>The WebVTT text for frames of <paramref name="tileWidth"/> × <paramref name="tileHeight"/> in a sprite with <paramref name="columns"/> columns.</summary>
    internal static string BuildVtt(string spriteFileName, IReadOnlyList<double> times, double duration, int columns, int tileWidth, int tileHeight)
    {
        var sb = new StringBuilder("WEBVTT\n");
        var url = Uri.EscapeDataString(spriteFileName);
        var ranges = CueRanges(times, duration);
        for (var i = 0; i < ranges.Length; i++)
        {
            var x = i % columns * tileWidth;
            var y = i / columns * tileHeight;
            sb.Append('\n').Append(i + 1).Append('\n')
              .Append(Timestamp(ranges[i].Start)).Append(" --> ").Append(Timestamp(ranges[i].End)).Append('\n')
              .Append(url).Append("#xywh=").Append(string.Join(",", new[] { x, y, tileWidth, tileHeight }.Select(v => v.ToString(CultureInfo.InvariantCulture))))
              .Append('\n');
        }
        return sb.ToString();
    }

    /// <summary>
    /// Writes sprite and WebVTT file next to the sheet. <paramref name="frames"/> are in timeline order; missing frames
    /// (null) become gray pictures. Returns the path of the WebVTT file.
    /// </summary>
    public static string Write(string sheetPath, IReadOnlyList<SKBitmap?> frames, IReadOnlyList<double> times, double duration,
                               double aspect, int tileWidth, int quality)
    {
        var (vttPath, spritePath) = PathsFor(sheetPath);
        var count = frames.Count;
        var columns = SpriteColumns(count);
        var rows = (count + columns - 1) / columns;
        var w = Math.Max(16, tileWidth);
        var h = Math.Max(16, (int)Math.Round(w / (aspect > 0 ? aspect : 16.0 / 9)));
        using (var sprite = new SKBitmap(new SKImageInfo(columns * w, Math.Max(1, rows) * h, SKColorType.Rgba8888, SKAlphaType.Premul)))
        {
            using (var canvas = new SKCanvas(sprite))
            {
                canvas.Clear(SKColors.Black);
                using var gray = new SKPaint { Color = new SKColor(60, 60, 60) };
                using var paint = new SKPaint { IsAntialias = true };
                for (var i = 0; i < count; i++)
                {
                    var dest = SKRect.Create(i % columns * w, i / columns * h, w, h);
                    if (frames[i] is { } bmp)
                    {
                        using var img = SKImage.FromBitmap(bmp);
                        canvas.DrawImage(img, dest, new SKSamplingOptions(SKCubicResampler.Mitchell), paint);
                    }
                    else canvas.DrawRect(dest, gray);
                }
            }
            WriteAtomic(spritePath, fs => SheetRenderer.Encode(sprite, fs, Settings.ImageFormat.Jpeg, quality));
        }
        var vtt = BuildVtt(Path.GetFileName(spritePath), times, duration, columns, w, h);
        WriteAtomic(vttPath, fs => { var b = new UTF8Encoding(false).GetBytes(vtt); fs.Write(b); });
        return vttPath;
    }

    private static void WriteAtomic(string path, Action<Stream> write)
    {
        var tmp = path + ".part";
        using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None)) write(fs);
        File.Move(tmp, path, overwrite: true);
    }
}
