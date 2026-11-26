using SkiaSharp;
using VideoThumbnailer.Core.Layout;
using VideoThumbnailer.Core.Localization;
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
        DrawBackgroundImage(canvas, s.BackgroundImage, layout.SheetWidth, layout.SheetHeight);

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
                canvas.DrawImage(img, CoverSource(fb.Width, fb.Height, dest), dest, sampling, tilePaint);
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

    /// <summary>File extension of the sheet: animation format if enabled, otherwise the image format.</summary>
    public static string Extension(SheetSettings s) => s.Animation.Enabled ? Extension(s.Animation.Format) : Extension(s.Output.Format);

    public static string Extension(AnimationFormat f) => f switch
    {
        AnimationFormat.Webm => ".webm",
        AnimationFormat.Gif => ".gif",
        _ => ".mp4",
    };

    public static string Extension(ImageFormat f) => f switch
    {
        ImageFormat.Png => ".png",
        ImageFormat.Webp => ".webp",
        _ => ".jpg",
    };

    // ---------------------------------------------------------------- helpers

    /// <summary>Part of a picture that fills <paramref name="dest"/> without distortion (cut off evenly at two sides).</summary>
    internal static SKRect CoverSource(int width, int height, SKRect dest)
    {
        if (width <= 0 || height <= 0 || dest.Width <= 0 || dest.Height <= 0) return new SKRect(0, 0, width, height);
        var destAspect = dest.Width / dest.Height;
        var srcAspect = (float)width / height;
        if (Math.Abs(srcAspect - destAspect) < 0.0005f) return new SKRect(0, 0, width, height);
        if (srcAspect > destAspect)
        {
            var w = height * destAspect;
            var x = (width - w) / 2;
            return new SKRect(x, 0, x + w, height);
        }
        var h = width / destAspect;
        var y = (height - h) / 2;
        return new SKRect(0, y, width, y + h);
    }

    private static void DrawBackgroundImage(SKCanvas canvas, BackgroundImageSettings bg, int width, int height)
    {
        if (!bg.Enabled || bg.Opacity <= 0 || BackgroundImageCache.Get(bg.Path) is not { } img) return;
        using var paint = new SKPaint { Color = SKColors.White.WithAlpha((byte)Math.Round(255 * Math.Clamp(bg.Opacity, 0, 1))), IsAntialias = true };
        var sampling = new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear);
        var full = new SKRect(0, 0, width, height);
        switch (bg.Mode)
        {
            case BackgroundImageMode.Stretch:
                canvas.DrawImage(img, full, sampling, paint);
                break;
            case BackgroundImageMode.Center:
                canvas.DrawImage(img, (width - img.Width) / 2f, (height - img.Height) / 2f, sampling, paint);
                break;
            case BackgroundImageMode.Tile:
                using (var shader = img.ToShader(SKShaderTileMode.Repeat, SKShaderTileMode.Repeat, sampling))
                {
                    paint.Shader = shader;
                    canvas.DrawRect(full, paint);
                }
                break;
            default:
                canvas.DrawImage(img, CoverSource(img.Width, img.Height, full), full, sampling, paint);
                break;
        }
    }

    private static List<string> HeaderLines(SheetSettings s, MediaInfo info)
    {
        var lang = s.EffectiveTextLanguage;
        var lines = InfoTemplate.Expand(s.Header.EffectiveTemplate(lang), info, lang)
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
        canvas.DrawText(CoreText.TIn(s.EffectiveTextLanguage, "Sheet.NoFrame"), dest.MidX, dest.MidY + font.Size / 3, SKTextAlign.Center, font, tp);
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
