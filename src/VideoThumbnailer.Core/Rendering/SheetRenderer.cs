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

        var style = s.TileStyle ?? new TileStyleSettings();
        var count = Math.Min(layout.Tiles.Count, frames.Count);
        var shapes = new SKPath[count];
        for (var i = 0; i < count; i++) shapes[i] = TileShape(ToRect(layout.Tiles[i]), style);
        try
        {
            // All shadows first, so a shadow never covers the neighbouring tile.
            if (style.Shadow)
                foreach (var shape in shapes) DrawTileShadow(canvas, shape, style);

            using var tilePaint = new SKPaint();
            using var gray = style.Grayscale ? SKColorFilter.CreateColorMatrix(GrayscaleMatrix) : null;
            tilePaint.ColorFilter = gray;
            var sampling = new SKSamplingOptions(SKCubicResampler.Mitchell);
            for (var i = 0; i < count; i++)
            {
                var dest = ToRect(layout.Tiles[i]);
                var frame = frames[i];
                canvas.Save();
                if (style.Corners != TileCorners.Square) canvas.ClipPath(shapes[i], antialias: true);
                if (frame.Bitmap is { } fb)
                {
                    using var img = SKImage.FromBitmap(fb);
                    canvas.DrawImage(img, CoverSource(fb.Width, fb.Height, dest), dest, sampling, tilePaint);
                }
                else
                {
                    DrawMissingTile(canvas, dest, s);
                }
                if (style.BorderWidth > 0)
                {
                    // Twice as wide on the outline, clipped to the tile: the visible half lies inside, following the corners.
                    canvas.ClipPath(shapes[i], antialias: true);
                    using var border = new SKPaint
                    {
                        IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = style.BorderWidth * 2,
                        StrokeJoin = style.Corners == TileCorners.Cut ? SKStrokeJoin.Bevel : SKStrokeJoin.Miter,
                        Color = ParseColor(style.BorderColor, SKColors.White),
                    };
                    canvas.DrawPath(shapes[i], border);
                }
                canvas.Restore();
                if (s.Watermark is { Enabled: true, PerTile: true })
                    DrawWatermark(canvas, s, dest, shapes[i], style.Corners != TileCorners.Square);
                if (s.Timestamp.Enabled)
                    DrawTimestamp(canvas, dest, s, frame.Time);
            }
        }
        finally
        {
            foreach (var p in shapes) p?.Dispose();
        }

        if (layout.HeaderHeight > 0)
            DrawHeader(canvas, s, info, layout.HeaderRect);

        if (s.Watermark is { Enabled: true, PerTile: false })
            DrawWatermark(canvas, s, new SKRect(0, 0, layout.SheetWidth, layout.SheetHeight), null, false);

        if (s.SheetBorderWidth > 0)
        {
            var w = s.SheetBorderWidth;
            using var border = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = w, Color = ParseColor(s.SheetBorderColor, SKColors.White) };
            canvas.DrawRect(new SKRect(w / 2, w / 2, layout.SheetWidth - w / 2, layout.SheetHeight - w / 2), border);
        }

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
        AnimationFormat.Webp => ".webp",
        AnimationFormat.Apng => ".png",
        AnimationFormat.Ogv => ".ogv",
        _ => ".mp4", // H.264 and H.265
    };

    public static string Extension(ImageFormat f) => f switch
    {
        ImageFormat.Png => ".png",
        ImageFormat.Webp => ".webp",
        _ => ".jpg",
    };

    // ---------------------------------------------------------------- helpers

    private static SKRect ToRect(TileRect r) => new(r.X, r.Y, r.X + r.Width, r.Y + r.Height);

    /// <summary>Luminance (Rec. 709) into all three channels, alpha unchanged.</summary>
    private static readonly float[] GrayscaleMatrix =
    [
        0.2126f, 0.7152f, 0.0722f, 0, 0,
        0.2126f, 0.7152f, 0.0722f, 0, 0,
        0.2126f, 0.7152f, 0.0722f, 0, 0,
        0, 0, 0, 1, 0,
    ];

    /// <summary>Outline of a tile: rectangle, rounded rectangle or rectangle with cut corners (#27).</summary>
    internal static SKPath TileShape(SKRect r, TileStyleSettings style)
    {
        var path = new SKPath();
        var c = Math.Min(style.CornerRadius, Math.Min(r.Width, r.Height) / 2);
        if (style.Corners == TileCorners.Square || c <= 0)
        {
            path.AddRect(r);
        }
        else if (style.Corners == TileCorners.Rounded)
        {
            path.AddRoundRect(r, c, c);
        }
        else
        {
            path.MoveTo(r.Left + c, r.Top);
            path.LineTo(r.Right - c, r.Top);
            path.LineTo(r.Right, r.Top + c);
            path.LineTo(r.Right, r.Bottom - c);
            path.LineTo(r.Right - c, r.Bottom);
            path.LineTo(r.Left + c, r.Bottom);
            path.LineTo(r.Left, r.Bottom - c);
            path.LineTo(r.Left, r.Top + c);
            path.Close();
        }
        return path;
    }

    private static void DrawTileShadow(SKCanvas canvas, SKPath shape, TileStyleSettings style)
    {
        using var paint = new SKPaint
        {
            IsAntialias = true,
            Color = ParseColor(style.ShadowColor, new SKColor(0, 0, 0, 180)),
            MaskFilter = style.ShadowBlur > 0 ? SKMaskFilter.CreateBlur(SKBlurStyle.Normal, style.ShadowBlur / 2) : null,
        };
        canvas.Save();
        canvas.Translate(style.ShadowOffset, style.ShadowOffset);
        canvas.DrawPath(shape, paint);
        canvas.Restore();
    }

    /// <summary>
    /// Watermark (#29) in <paramref name="area"/> (the sheet or one tile): the picture, or the text when there is none.
    /// Its width is <see cref="WatermarkSettings.Size"/> of the area width; a missing picture draws nothing.
    /// </summary>
    private static void DrawWatermark(SKCanvas canvas, SheetSettings s, SKRect area, SKPath? clip, bool clipToShape)
    {
        var wm = s.Watermark;
        if (wm.Opacity <= 0 || area.Width < 2 || area.Height < 2) return;
        var opacity = (float)Math.Clamp(wm.Opacity, 0, 1);
        var width = (float)(area.Width * Math.Clamp(wm.Size, 0.01, 1));

        SKImage? image = null;
        SKFont? font = null;
        string text = "";
        float height;
        if (wm.HasImage)
        {
            if ((image = ImageFileCache.Get(wm.ImagePath)) is null || image.Width <= 0) return;
            height = width * image.Height / image.Width;
        }
        else
        {
            text = (wm.Text ?? "").Replace("\r\n", " ").Replace('\n', ' ').Trim();
            if (text.Length == 0) return;
            font = new SKFont(FontProvider.Get(s.FontFamily, true), 100) { Edging = SKFontEdging.Antialias };
            var measured = font.MeasureText(text);
            if (measured <= 0) { font.Dispose(); return; }
            font.Size = 100 * width / measured;
            height = font.Size * 1.2f;
        }
        // Never larger than the area (very long texts, tall pictures).
        if (height > area.Height)
        {
            var k = area.Height / height;
            width *= k; height *= k;
            if (font is not null) font.Size *= k;
        }

        using (font)
        {
            canvas.Save();
            canvas.ClipRect(area);
            if (clipToShape && clip is not null) canvas.ClipPath(clip, antialias: true);
            var m = Math.Max(2, Math.Min(area.Width, area.Height) * 0.03f);
            if (wm.Position == WatermarkPosition.Tiled)
            {
                var stepX = width * 1.5f;
                var stepY = height * 2.5f;
                var row = 0;
                for (var y = area.Top + m; y < area.Bottom; y += stepY, row++)
                    for (var x = area.Left + m - (row % 2 == 1 ? stepX / 2 : 0); x < area.Right; x += stepX)
                        DrawWatermarkItem(canvas, image, font, text, wm, opacity, new SKRect(x, y, x + width, y + height));
            }
            else
            {
                var x = wm.Position switch
                {
                    WatermarkPosition.TopLeft or WatermarkPosition.BottomLeft => area.Left + m,
                    WatermarkPosition.Center => area.MidX - width / 2,
                    _ => area.Right - m - width,
                };
                var y = wm.Position switch
                {
                    WatermarkPosition.TopLeft or WatermarkPosition.TopRight => area.Top + m,
                    WatermarkPosition.Center => area.MidY - height / 2,
                    _ => area.Bottom - m - height,
                };
                DrawWatermarkItem(canvas, image, font, text, wm, opacity, new SKRect(x, y, x + width, y + height));
            }
            canvas.Restore();
        }
    }

    private static void DrawWatermarkItem(SKCanvas canvas, SKImage? image, SKFont? font, string text, WatermarkSettings wm, float opacity, SKRect r)
    {
        if (image is not null)
        {
            using var paint = new SKPaint { Color = SKColors.White.WithAlpha((byte)Math.Round(255 * opacity)), IsAntialias = true };
            canvas.DrawImage(image, r, new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear), paint);
            return;
        }
        if (font is null) return;
        using var fill = new SKPaint { IsAntialias = true, Color = WithOpacity(ParseColor(wm.Color, SKColors.White), opacity) };
        var mt = font.Metrics;
        var baseline = r.Top + (r.Height - (mt.Descent - mt.Ascent)) / 2 - mt.Ascent;
        canvas.DrawText(text, r.Left, baseline, SKTextAlign.Left, font, fill);
    }

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
        if (!bg.Enabled || bg.Opacity <= 0 || ImageFileCache.Get(bg.Path) is not { } img) return;
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
        var template = InfoTemplate.WithComment(s.Header.EffectiveTemplate(lang), s.Header.Comment);
        var lines = InfoTemplate.Expand(template, info, lang, s.Header.Comment)
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
