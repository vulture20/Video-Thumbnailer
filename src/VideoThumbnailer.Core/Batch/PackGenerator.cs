using SkiaSharp;
using VideoThumbnailer.Core.Extraction;
using VideoThumbnailer.Core.Ffmpeg;
using VideoThumbnailer.Core.Input;
using VideoThumbnailer.Core.Layout;
using VideoThumbnailer.Core.Localization;
using VideoThumbnailer.Core.Media;
using VideoThumbnailer.Core.Rendering;
using VideoThumbnailer.Core.Settings;
using VideoThumbnailer.Core.Util;

namespace VideoThumbnailer.Core.Batch;

/// <summary>Result of a pack picture (#37): the files written and the videos that could not be read.</summary>
public sealed record PackResult(IReadOnlyList<string> Files, int Videos, IReadOnlyList<(string Path, string Error)> Failed);

/// <summary>
/// One overview picture for many videos (#37), e.g. a season or a folder: per video one row with its name, duration
/// and resolution and a strip of small tiles. Colours, font, time stamps, gaps and skipping of the start/end come
/// from the sheet settings; the number of tiles per row is the column count, the width the sheet width. Pictures
/// taller than the format allows are split into several files ("pack-1.jpg", "pack-2.jpg" …).
/// </summary>
public sealed class PackGenerator(FfmpegTools tools, SemaphoreSlim processLimiter)
{
    /// <summary>Highest picture the formats can store: WebP 16383 px, JPEG 65535 px; PNG is limited for memory.</summary>
    public static int MaxHeight(ImageFormat f) => f switch
    {
        ImageFormat.Webp => 16383,
        ImageFormat.Jpeg => 65000,
        _ => 32000,
    };

    /// <summary>Format by the file extension (.png, .webp, otherwise JPEG).</summary>
    public static ImageFormat FormatFor(string file) => Path.GetExtension(file).ToLowerInvariant() switch
    {
        ".png" => ImageFormat.Png,
        ".webp" => ImageFormat.Webp,
        _ => ImageFormat.Jpeg,
    };

    /// <summary>Geometry of a pack: tile box, row height, rows per file.</summary>
    public sealed record Geometry(int Width, int TilesPerRow, int TileWidth, int TileHeight, int TitleHeight, int RowHeight, int Margin, int Gap, int RowsPerFile);

    public static Geometry Measure(SheetSettings s, int tilesPerRow, int width, ImageFormat format)
    {
        var n = Math.Clamp(tilesPerRow, 1, 50);
        width = Math.Clamp(width, 200, 16000);
        var margin = Math.Clamp(s.Margin, 0, 500);
        var gap = Math.Clamp(s.Gap, 0, 500);
        var tileW = Math.Max(16, (width - 2 * margin - (n - 1) * gap) / n);
        width = 2 * margin + n * tileW + (n - 1) * gap;
        var tileH = Math.Max(9, (int)Math.Round(tileW * 9.0 / 16));
        var titleH = (int)Math.Ceiling(Math.Clamp(s.Header.FontSize, 8, 72) * 1.6f) + 4;
        var rowH = titleH + tileH + gap;
        var rows = Math.Max(1, (MaxHeight(format) - 2 * margin + gap) / rowH);
        return new Geometry(width, n, tileW, tileH, titleH, rowH, margin, gap, rows);
    }

    /// <summary>File names: "pack.jpg" for one file, "pack-1.jpg", "pack-2.jpg" … for more.</summary>
    public static IReadOnlyList<string> FileNames(string target, int files)
    {
        if (files <= 1) return [target];
        var dir = Path.GetDirectoryName(target) ?? "";
        var stem = Path.GetFileNameWithoutExtension(target);
        var ext = Path.GetExtension(target);
        return Enumerable.Range(1, files).Select(i => Path.Combine(dir, $"{stem}-{i}{ext}")).ToList();
    }

    private sealed record Row(string Path, MediaInfo? Info, CapturedFrame[] Frames, string? Error);

    public async Task<PackResult> GenerateAsync(IReadOnlyList<string> videos, SheetSettings settings, string target, int tilesPerRow, int width,
                                                IProgress<double>? progress, CancellationToken ct)
    {
        var s = settings.Clone();
        s.Normalize();
        var format = FormatFor(target);
        var g = Measure(s, tilesPerRow, width, format);
        var extractor = new FrameExtractor(tools.FfmpegPath);
        var rows = new Row?[videos.Count];
        var done = 0;
        var total = Math.Max(1, videos.Count * g.TilesPerRow);
        try
        {
            await Task.WhenAll(videos.Select(async (video, vi) =>
            {
                MediaInfo info;
                await processLimiter.WaitAsync(ct).ConfigureAwait(false);
                try { info = await FfprobeReader.ReadAsync(tools.FfprobePath, video, ct).ConfigureAwait(false); }
                catch (Exception e) when (e is InvalidDataException or IOException or System.ComponentModel.Win32Exception or System.Text.Json.JsonException)
                {
                    rows[vi] = new Row(video, null, [], e.Message);
                    Interlocked.Add(ref done, g.TilesPerRow);
                    progress?.Report((double)done / total);
                    return;
                }
                finally { processLimiter.Release(); }
                if (info.Video is null)
                {
                    rows[vi] = new Row(video, info, [], CoreText.T("NoVideoStream"));
                    Interlocked.Add(ref done, g.TilesPerRow);
                    return;
                }
                var (fw, fh) = Fit(info.Video.DisplayAspect, g.TileWidth, g.TileHeight);
                var plan = TimestampPlanner.Plan(info.Duration, g.TilesPerRow, s.SkipStartPercent, s.SkipEndPercent, null);
                var frames = new CapturedFrame[plan.Length];
                var search = s.SkipBlackFrames ? s.BlackFrameSearchSeconds : 0;
                await Task.WhenAll(plan.Select(async (p, i) =>
                {
                    await processLimiter.WaitAsync(ct).ConfigureAwait(false);
                    try
                    {
                        var (bmp, t) = await extractor.ExtractNonBlankAsync(info, p.Time, fw, fh, search, ct).ConfigureAwait(false);
                        frames[i] = new CapturedFrame(t, bmp);
                    }
                    finally { processLimiter.Release(); }
                    progress?.Report((double)Interlocked.Increment(ref done) / total);
                })).ConfigureAwait(false);
                rows[vi] = new Row(video, info, frames, frames.All(f => f.Bitmap is null) ? CoreText.T("NoFrameDecoded") : null);
            })).ConfigureAwait(false);

            ct.ThrowIfCancellationRequested();
            var good = rows.Where(r => r is { Error: null }).Cast<Row>().ToList();
            var failed = rows.Where(r => r is { Error: not null }).Select(r => (r!.Path, r.Error!)).ToList();
            if (good.Count == 0) return new PackResult([], 0, failed);

            var pages = (good.Count + g.RowsPerFile - 1) / g.RowsPerFile;
            var names = FileNames(target, pages);
            for (var p = 0; p < pages; p++)
            {
                var pageRows = good.Skip(p * g.RowsPerFile).Take(g.RowsPerFile).ToList();
                using var bmp = Render(s, g, pageRows);
                var file = names[p];
                var dir = Path.GetDirectoryName(Path.GetFullPath(file));
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                var tmp = file + ".part";
                await using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
                    SheetRenderer.Encode(bmp, fs, format, s.Output.Quality);
                File.Move(tmp, file, overwrite: true);
            }
            progress?.Report(1);
            return new PackResult(names, good.Count, failed);
        }
        finally
        {
            foreach (var r in rows)
                if (r is not null) foreach (var f in r.Frames) f?.Bitmap?.Dispose();
        }
    }

    /// <summary>Largest size of the video's aspect ratio inside the tile box.</summary>
    internal static (int W, int H) Fit(double aspect, int boxW, int boxH)
    {
        if (aspect <= 0) aspect = 16.0 / 9;
        var w = boxW;
        var h = (int)Math.Round(boxW / aspect);
        if (h > boxH) { h = boxH; w = (int)Math.Round(boxH * aspect); }
        return (Math.Max(2, w), Math.Max(2, h));
    }

    private static SKBitmap Render(SheetSettings s, Geometry g, IReadOnlyList<Row> rows)
    {
        var height = 2 * g.Margin + rows.Count * g.RowHeight - g.Gap;
        var bmp = new SKBitmap(new SKImageInfo(g.Width, height, SKColorType.Rgba8888, SKAlphaType.Premul));
        using var canvas = new SKCanvas(bmp);
        canvas.Clear(SheetRenderer.ParseColor(s.BackgroundColor, new SKColor(30, 30, 30)));
        var lang = s.EffectiveTextLanguage;
        using var titleFont = new SKFont(FontProvider.Get(s.FontFamily, true), Math.Clamp(s.Header.FontSize, 8, 72));
        using var infoFont = new SKFont(FontProvider.Get(s.FontFamily, false), Math.Clamp(s.Header.FontSize * 0.85f, 7, 60));
        using var titlePaint = new SKPaint { Color = SheetRenderer.ParseColor(s.Header.TitleColor, SKColors.White), IsAntialias = true };
        using var infoPaint = new SKPaint { Color = SheetRenderer.ParseColor(s.Header.Color, new SKColor(0xE6, 0xE6, 0xE6)), IsAntialias = true };
        using var boxPaint = new SKPaint { Color = new SKColor(0, 0, 0, 60) };
        var sampling = new SKSamplingOptions(SKCubicResampler.Mitchell);

        for (var r = 0; r < rows.Count; r++)
        {
            var row = rows[r];
            var info = row.Info!;
            var y = g.Margin + r * g.RowHeight;
            var baseline = y + g.TitleHeight - 2 - titleFont.Metrics.Descent;
            var details = Formatting.Duration(info.Duration) + (info.Video is { } v ? $" · {v.DisplaySize.Width}×{v.DisplaySize.Height}" : "")
                          + " · " + Formatting.Size(info.FileSize, null, Lang.NumberFormat(lang));
            var detailsWidth = infoFont.MeasureText(details);
            var title = SheetRenderer.Ellipsize(info.Title is { Length: > 0 } t ? t : VideoSource.NameWithoutExtension(row.Path), titleFont,
                                                Math.Max(40, g.Width - 2 * g.Margin - detailsWidth - 16));
            canvas.DrawText(title, g.Margin, baseline, titleFont, titlePaint);
            canvas.DrawText(details, g.Width - g.Margin - detailsWidth, baseline, infoFont, infoPaint);

            var ty = y + g.TitleHeight;
            for (var i = 0; i < g.TilesPerRow; i++)
            {
                var x = g.Margin + i * (g.TileWidth + g.Gap);
                var box = new SKRect(x, ty, x + g.TileWidth, ty + g.TileHeight);
                canvas.DrawRect(box, boxPaint);
                if (i >= row.Frames.Length || row.Frames[i].Bitmap is not { } fb) continue;
                var dest = new SKRect(box.MidX - fb.Width / 2f, box.MidY - fb.Height / 2f, box.MidX + fb.Width / 2f, box.MidY + fb.Height / 2f);
                using var img = SKImage.FromBitmap(fb);
                canvas.DrawImage(img, new SKRect(0, 0, fb.Width, fb.Height), dest, sampling);
                if (s.Timestamp.Enabled) SheetRenderer.DrawTimestamp(canvas, box, s, row.Frames[i].Time);
            }
        }
        return bmp;
    }
}
