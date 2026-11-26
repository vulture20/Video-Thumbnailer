using SkiaSharp;
using VideoThumbnailer.Core.Layout;
using VideoThumbnailer.Core.Media;
using VideoThumbnailer.Core.Settings;

namespace VideoThumbnailer.Core.Rendering;

/// <summary>Result of <see cref="SheetPreview.Render"/>: the image plus the size of the real sheet.</summary>
/// <param name="TileRects">Tile rectangles in sheet pixels, in timeline order (to find the tile under the mouse).</param>
public sealed record SheetPreviewResult(SKBitmap Bitmap, int SheetWidth, int SheetHeight, int Tiles, int RealFrames = 0,
                                        IReadOnlyList<TileRect>? TileRects = null) : IDisposable
{
    public void Dispose() => Bitmap.Dispose();
}

/// <summary>
/// Renders a sheet with the real <see cref="SheetRenderer"/> for the live preview in the GUI, so header,
/// timestamps, colors and geometry match the result exactly. Pictures come from a frame provider (real
/// frames of the video); every frame it cannot deliver (yet) is drawn as a painted placeholder scene.
/// </summary>
public static class SheetPreview
{
    // Sky, horizon, ground: muted "landscape" colors so the preview reads as film frames.
    private static readonly (uint Sky, uint Horizon, uint Ground)[] Palettes =
    [
        (0xFF7FB8E6, 0xFFD9ECF7, 0xFF2F5F4F), (0xFFF4B06B, 0xFFF7D9A8, 0xFF3B3A52), (0xFF3A4A7A, 0xFF8893C2, 0xFF141A2C),
        (0xFF9FD4D0, 0xFFE8F4EF, 0xFF4F7D5C), (0xFFE27D5F, 0xFFF2C27B, 0xFF4A2F3C), (0xFF5B8FB9, 0xFFB8D4E8, 0xFF33475C),
        (0xFFC9D6DF, 0xFFF2F4F5, 0xFF6B7B86), (0xFF204B5E, 0xFF3F8AA1, 0xFF0D1E26), (0xFFF0C987, 0xFFFBE9C4, 0xFF7A5A3A),
        (0xFF6D5BA3, 0xFFC39AD6, 0xFF22183A), (0xFF8CC0A5, 0xFFDCEFE3, 0xFF2E4A3A), (0xFFD9604C, 0xFFF39A6B, 0xFF2A1A1E),
    ];

    /// <summary>A plausible stand-in for a video when the real one has not been read yet.</summary>
    public static MediaInfo SampleInfo(string path, long fileSize = 1_975_000_000) => new()
    {
        Path = path,
        FileSize = fileSize,
        Duration = 1543.2,
        BitRate = 10_240_000,
        ContainerFormat = "mov,mp4,m4a,3gp,3g2,mj2",
        Video = new VideoStreamInfo { Codec = "h264", Profile = "High", Width = 1920, Height = 1080, FrameRate = 25, PixelFormat = "yuv420p" },
        AudioStreams = [new AudioStreamInfo { Codec = "aac", SampleRate = 48000, Channels = 2, ChannelLayout = "stereo" }],
    };

    /// <summary>Capture times of the sheet for these settings and this video (same plan as the real sheet).</summary>
    public static PlannedTime[] PlanTimes(SheetSettings settings, MediaInfo info, IReadOnlyList<ManualTime?>? manual = null)
    {
        var (s, layout) = Prepare(settings, info);
        return TimestampPlanner.Plan(info.Duration, layout.Tiles.Count, s.SkipStartPercent, s.SkipEndPercent, manual);
    }

    private static (SheetSettings, GridLayout) Prepare(SheetSettings settings, MediaInfo info)
    {
        var s = settings.Clone();
        s.Normalize();
        var aspect = info.Video?.DisplayAspect ?? 16.0 / 9.0;
        // Same seed as SheetGenerator, so "random" order matches the real sheet.
        var seed = StringComparer.Ordinal.GetHashCode(Input.VideoSource.FileName(info.Path));
        return (s, GridLayout.Create(s, aspect, SheetRenderer.MeasureHeader(s, info), new Random(seed)));
    }

    /// <param name="frame">
    /// Returns the real frame for a planned capture time (with the time actually used, e.g. after skipping a black
    /// frame), or null to draw a placeholder. Returned bitmaps stay owned by the caller (they are not disposed here).
    /// Null provider = placeholders only.
    /// </param>
    /// <param name="maxWidth">The returned bitmap is scaled down to at most this width (the sheet size is reported unscaled).</param>
    /// <param name="manual">Capture times chosen by hand for this video (see <see cref="TimestampPlanner"/>).</param>
    public static SheetPreviewResult Render(SheetSettings settings, MediaInfo info, int maxWidth = 1400, Func<PlannedTime, CapturedFrame?>? frame = null,
                                            IReadOnlyList<ManualTime?>? manual = null)
    {
        var (s, layout) = Prepare(settings, info);
        var aspect = info.Video?.DisplayAspect ?? 16.0 / 9.0;
        var times = TimestampPlanner.Plan(info.Duration, layout.Tiles.Count, s.SkipStartPercent, s.SkipEndPercent, manual);

        var sceneW = Math.Clamp(layout.TileWidth, 16, 320);
        var sceneH = Math.Max(2, (int)Math.Round(sceneW / aspect));
        var frames = new List<CapturedFrame>(times.Length);
        var scenes = new List<SKBitmap>();
        var real = 0;
        try
        {
            for (var i = 0; i < times.Length; i++)
            {
                if (frame?.Invoke(times[i]) is { Bitmap: not null } got)
                {
                    real++;
                    frames.Add(got);
                    continue;
                }
                var scene = Scene(i, sceneW, sceneH);
                scenes.Add(scene);
                frames.Add(new CapturedFrame(times[i].Time, scene));
            }
            var full = SheetRenderer.Render(s, info, layout, frames);
            if (full.Width <= maxWidth)
                return new SheetPreviewResult(full, layout.SheetWidth, layout.SheetHeight, layout.Tiles.Count, real, layout.Tiles);
            var h = Math.Max(1, (int)Math.Round((double)full.Height * maxWidth / full.Width));
            var small = full.Resize(new SKImageInfo(maxWidth, h), new SKSamplingOptions(SKCubicResampler.Mitchell)) ?? full;
            if (!ReferenceEquals(small, full)) full.Dispose();
            return new SheetPreviewResult(small, layout.SheetWidth, layout.SheetHeight, layout.Tiles.Count, real, layout.Tiles);
        }
        finally
        {
            foreach (var b in scenes) b.Dispose();
        }
    }

    /// <summary>A small painted "landscape": sky gradient, a sun and ground. Deterministic per index.</summary>
    internal static SKBitmap Scene(int index, int width, int height)
    {
        var p = Palettes[(index * 7 + 3) % Palettes.Length];
        var horizon = height * (0.40f + (index * 13 % 25) / 100f);
        var bmp = new SKBitmap(width, height);
        using var canvas = new SKCanvas(bmp);
        using (var sky = new SKPaint())
        {
            sky.Shader = SKShader.CreateLinearGradient(new SKPoint(0, 0), new SKPoint(0, horizon),
                [new SKColor(p.Sky), new SKColor(p.Horizon)], SKShaderTileMode.Clamp);
            canvas.DrawRect(0, 0, width, horizon, sky);
        }
        using (var sun = new SKPaint { Color = new SKColor(255, 255, 255, 140), IsAntialias = true })
            canvas.DrawCircle(width * (0.15f + (index * 29 % 70) / 100f), horizon * 0.45f, Math.Max(1.5f, height * 0.06f), sun);
        using (var ground = new SKPaint { Color = new SKColor(p.Ground) })
            canvas.DrawRect(0, horizon, width, height - horizon, ground);
        return bmp;
    }
}
