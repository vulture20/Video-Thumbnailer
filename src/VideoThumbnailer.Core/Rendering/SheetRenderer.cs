using SkiaSharp;
using VideoThumbnailer.Core.Layout;
using VideoThumbnailer.Core.Media;
using VideoThumbnailer.Core.Settings;
using VideoThumbnailer.Core.Util;

namespace VideoThumbnailer.Core.Rendering;

public sealed record CapturedFrame(double Time, SKBitmap? Bitmap);

/// <summary>Composes captured frames, timestamps and the info header into one image.</summary>
public static class SheetRenderer
{
    /// <summary>Height of the info header band for the given settings and media.</summary>
    public static int MeasureHeader(SheetSettings s, MediaInfo info)
    {
        if (!s.Header.Enabled) return 0;
        var lines = HeaderLines(s, info);
        if (lines.Count == 0) return 0;
        float h = 0;
        for (var i = 0; i < lines.Count; i++)
            h += LineHeight(s, i == 0);
        return (int)Math.Ceiling(h + s.Header.FontSize * 0.3f);
    }

    public static SKBitmap Render(SheetSettings s, MediaInfo info, GridLayout layout, IReadOnlyList<CapturedFrame> frames)
    {
        var bmp = new SKBitmap(new SKImageInfo(layout.SheetWidth, layout.SheetHeight, SKColorType.Rgba8888, SKAlphaType.Premul));
        using var canvas = new SKCanvas(bmp);
        canvas.Clear(ParseColor(s.BackgroundColor, new SKColor(30, 30, 30)));

        using var tilePaint = new SKPaint();
        var sampling = new SKSamplingOptions(SKCubicResampler.Mitchell);
        for (var i = 0; i < layout.Tiles.Count && i < frames.Count; i++)
        {
            var rect = layout.Tiles[i];
            var dest = new SKRect(rect.X, rect.Y, rect.X + rect.Width, rect.Y + rect.Height);
            var frame = frames[i];
            if (frame.Bitmap is { } fb)
            {
                using var img = SKImage.FromBitmap(fb);
                canvas.DrawImage(img, dest, sampling, tilePaint);
            }
            else
            {
                DrawMissingTile(canvas, dest, s);
            }
            if (s.Timestamp.Enabled)
                DrawTimestamp(canvas, dest, s, frame.Time);
        }

        if (layout.HeaderHeight > 0)
            DrawHeader(canvas, s, info, layout.HeaderRect);

        canvas.Flush();
        return bmp;
    }

    public static void Encode(SKBitmap bitmap, Stream output, ImageFormat format, int quality)
    {
        using var image = SKImage.FromBitmap(bitmap);
        var skFormat = format switch
        {
            ImageFormat.Png => SKEncodedImageFormat.Png,
            ImageFormat.Webp => SKEncodedImageFormat.Webp,
            _ => SKEncodedImageFormat.Jpeg,
        };
        using var data = image.Encode(skFormat, Math.Clamp(quality, 1, 100))
                         ?? throw new InvalidOperationException($"Encoding as {format} is not supported on this platform.");
        data.SaveTo(output);
    }

    public static string Extension(ImageFormat f) => f switch
    {
        ImageFormat.Png => ".png",
        ImageFormat.Webp => ".webp",
        _ => ".jpg",
    };

    // ---------------------------------------------------------------- helpers

    private static List<string> HeaderLines(SheetSettings s, MediaInfo info)
    {
        var lines = InfoTemplate.Expand(s.Header.Template ?? "", info)
            .Split('\n')
            .Select(l => l.TrimEnd())
            .ToList();
        return lines.All(string.IsNullOrWhiteSpace) ? [] : lines;
    }

    private static float LineHeight(SheetSettings s, bool title) =>
        (title ? s.Header.TitleFontSize : s.Header.FontSize) * s.Header.LineSpacing;

    private static void DrawHeader(SKCanvas canvas, SheetSettings s, MediaInfo info, TileRect rect)
    {
        var lines = HeaderLines(s, info);
        var right = s.Header.Position is HeaderPosition.TopRight or HeaderPosition.BottomRight;
        float y = rect.Y;
        using var paint = new SKPaint { IsAntialias = true };
        for (var i = 0; i < lines.Count; i++)
        {
            var title = i == 0;
            using var font = new SKFont(FontProvider.Get(s.FontFamily, title), title ? s.Header.TitleFontSize : s.Header.FontSize)
            {
                Subpixel = true,
                Edging = SKFontEdging.SubpixelAntialias,
            };
            paint.Color = ParseColor(title ? s.Header.TitleColor : s.Header.Color, SKColors.White);
            var lh = LineHeight(s, title);
            var text = Ellipsize(lines[i], font, rect.Width);
            var baseline = y + lh - (lh - font.Size) / 2 - font.Metrics.Descent * 0.5f;
            var x = right ? rect.X + rect.Width : rect.X;
            canvas.DrawText(text, x, baseline, right ? SKTextAlign.Right : SKTextAlign.Left, font, paint);
            y += lh;
        }
    }

    private static void DrawTimestamp(SKCanvas canvas, SKRect tile, SheetSettings s, double time)
    {
        var ts = s.Timestamp;
        var text = Formatting.Duration(time, compact: false, millis: ts.ShowMilliseconds);
        using var font = new SKFont(FontProvider.Get(s.FontFamily, ts.Bold), ts.FontSize) { Edging = SKFontEdging.Antialias };
        var width = font.MeasureText(text);
        var metrics = font.Metrics;
        var textH = -metrics.Ascent; // cap-ish height above baseline
        var pad = ts.Padding;

        float x = ts.Position switch
        {
            OverlayPosition.TopLeft or OverlayPosition.BottomLeft => tile.Left + pad,
            OverlayPosition.Center => tile.MidX - width / 2,
            _ => tile.Right - pad - width,
        };
        float baseline = ts.Position switch
        {
            OverlayPosition.TopLeft or OverlayPosition.TopRight => tile.Top + pad + textH,
            OverlayPosition.Center => tile.MidY + textH / 2,
            _ => tile.Bottom - pad - metrics.Descent,
        };

        canvas.Save();
        canvas.ClipRect(tile);
        var opacity = (float)Math.Clamp(ts.Opacity, 0, 1);

        if (ts.Shadow)
        {
            using var shadow = new SKPaint
            {
                IsAntialias = true,
                Color = WithOpacity(ParseColor(ts.ShadowColor, new SKColor(0, 0, 0, 170)), opacity),
                MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, Math.Max(0.5f, ts.ShadowOffset * 0.75f)),
            };
            canvas.DrawText(text, x + ts.ShadowOffset, baseline + ts.ShadowOffset, SKTextAlign.Left, font, shadow);
        }
        if (ts.OutlineWidth > 0)
        {
            using var outline = new SKPaint
            {
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = ts.OutlineWidth,
                StrokeJoin = SKStrokeJoin.Round,
                Color = WithOpacity(ParseColor(ts.OutlineColor, SKColors.Black), opacity),
            };
            canvas.DrawText(text, x, baseline, SKTextAlign.Left, font, outline);
        }
        using var fill = new SKPaint { IsAntialias = true, Color = WithOpacity(ParseColor(ts.Color, SKColors.White), opacity) };
        canvas.DrawText(text, x, baseline, SKTextAlign.Left, font, fill);
        canvas.Restore();
    }

    private static void DrawMissingTile(SKCanvas canvas, SKRect dest, SheetSettings s)
    {
        using var p = new SKPaint { Color = new SKColor(60, 60, 60) };
        canvas.DrawRect(dest, p);
        using var font = new SKFont(FontProvider.Get(s.FontFamily, false), Math.Max(10, dest.Height / 8));
        using var tp = new SKPaint { Color = new SKColor(160, 160, 160), IsAntialias = true };
        canvas.DrawText("no frame", dest.MidX, dest.MidY + font.Size / 3, SKTextAlign.Center, font, tp);
    }

    internal static string Ellipsize(string text, SKFont font, float maxWidth)
    {
        if (font.MeasureText(text) <= maxWidth) return text;
        const string ell = "…";
        int lo = 0, hi = text.Length;
        while (lo < hi)
        {
            var mid = (lo + hi + 1) / 2;
            if (font.MeasureText(text[..mid] + ell) <= maxWidth) lo = mid; else hi = mid - 1;
        }
        return text[..lo] + ell;
    }

    public static SKColor ParseColor(string? value, SKColor fallback) =>
        !string.IsNullOrWhiteSpace(value) && SKColor.TryParse(value, out var c) ? c : fallback;

    private static SKColor WithOpacity(SKColor c, float opacity) => c.WithAlpha((byte)Math.Round(c.Alpha * opacity));
}
