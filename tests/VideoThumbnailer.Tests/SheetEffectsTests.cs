using System.Globalization;
using SkiaSharp;
using VideoThumbnailer.Cli;
using VideoThumbnailer.Core.Batch;
using VideoThumbnailer.Core.Ffmpeg;
using VideoThumbnailer.Core.Layout;
using VideoThumbnailer.Core.Rendering;
using VideoThumbnailer.Core.Settings;

namespace VideoThumbnailer.Tests;

/// <summary>#27 tile effects, #28 comment, #29 watermark, #30 more animation formats and sound.</summary>
public class SheetEffectsTests
{
    private static readonly SKColor Red = new(220, 30, 30);

    /// <summary>One tile of solid red on black, without header and timestamps.</summary>
    private static (SKBitmap Sheet, GridLayout Layout) Render(SheetSettings s, int columns = 1, int rows = 1)
    {
        s.Columns = columns; s.Rows = rows;
        s.Header.Enabled = false;
        s.Timestamp.Enabled = false;
        s.BackgroundColor = "#FF000000";
        var layout = GridLayout.Create(s, 16.0 / 9, 0);
        var frames = layout.Tiles.Select(t =>
        {
            var bmp = new SKBitmap(t.Width, t.Height);
            bmp.Erase(Red);
            return new CapturedFrame(1, bmp);
        }).ToList();
        try { return (SheetRenderer.Render(s, TestUtil.SampleInfo(), layout, frames), layout); }
        finally { foreach (var f in frames) f.Bitmap?.Dispose(); }
    }

    private static string MakeImage(string dir, SKColor color, string name = "wm.png", int w = 40, int h = 20)
    {
        using var bmp = new SKBitmap(w, h);
        bmp.Erase(color);
        var path = Path.Combine(dir, name);
        using var fs = File.Create(path);
        bmp.Encode(fs, SKEncodedImageFormat.Png, 100);
        return path;
    }

    // ------------------------------------------------------------------ #27 tile effects

    [Fact]
    public void Tile_border_is_drawn_inside_and_keeps_the_layout()
    {
        var plain = new SheetSettings { SheetWidth = 400, Margin = 20 };
        var s = new SheetSettings { SheetWidth = 400, Margin = 20 };
        s.TileStyle.BorderWidth = 4;
        s.TileStyle.BorderColor = "#FF00FF00";
        var (a, la) = Render(plain);
        var (b, lb) = Render(s);
        using (a) using (b)
        {
            Assert.Equal((a.Width, a.Height), (b.Width, b.Height));
            Assert.Equal(la.Tiles[0], lb.Tiles[0]);
            var t = lb.Tiles[0];
            Assert.Equal(new SKColor(0, 255, 0), b.GetPixel(t.X + 1, t.Y + t.Height / 2));
            Assert.Equal(Red, b.GetPixel(t.X + 6, t.Y + t.Height / 2));
            Assert.Equal(new SKColor(0, 0, 0), b.GetPixel(t.X - 2, t.Y + t.Height / 2)); // nothing outside
        }
    }

    [Fact]
    public void Grayscale_turns_tiles_gray()
    {
        var s = new SheetSettings { SheetWidth = 300 };
        s.TileStyle.Grayscale = true;
        var (sheet, l) = Render(s);
        using (sheet)
        {
            var t = l.Tiles[0];
            var p = sheet.GetPixel(t.X + t.Width / 2, t.Y + t.Height / 2);
            Assert.Equal(p.Red, p.Green);
            Assert.Equal(p.Green, p.Blue);
            Assert.InRange(p.Red, 55, 75); // luminance of the red
        }
    }

    [Theory]
    [InlineData(TileCorners.Rounded)]
    [InlineData(TileCorners.Cut)]
    public void Rounded_and_cut_corners_show_the_background(TileCorners corners)
    {
        var s = new SheetSettings { SheetWidth = 400, Margin = 20 };
        s.TileStyle.Corners = corners;
        s.TileStyle.CornerRadius = 20;
        var (sheet, l) = Render(s);
        using (sheet)
        {
            var t = l.Tiles[0];
            Assert.Equal(new SKColor(0, 0, 0), sheet.GetPixel(t.X + 1, t.Y + 1));
            Assert.Equal(new SKColor(0, 0, 0), sheet.GetPixel(t.X + t.Width - 2, t.Y + t.Height - 2));
            Assert.Equal(Red, sheet.GetPixel(t.X + t.Width / 2, t.Y + 1));
            Assert.Equal(Red, sheet.GetPixel(t.X + 1, t.Y + t.Height / 2));
        }
    }

    [Fact]
    public void Tile_shadow_falls_into_the_gap()
    {
        var s = new SheetSettings { SheetWidth = 400, Margin = 20, Gap = 12 };
        s.TileStyle.ShadowColor = "#FFFFFFFF"; // white on black is easy to see
        var (off, l) = Render(s);
        s.TileStyle.Shadow = true;
        var (on, _) = Render(s);
        using (off) using (on)
        {
            var t = l.Tiles[0];
            var x = t.X + t.Width / 2;
            var y = t.Y + t.Height + 3; // just below the tile
            Assert.Equal(new SKColor(0, 0, 0), off.GetPixel(x, y));
            Assert.True(on.GetPixel(x, y).Red > 60, on.GetPixel(x, y).ToString());
            Assert.Equal(new SKColor(0, 0, 0), on.GetPixel(t.X - 6, t.Y + 2)); // not at the top left
        }
    }

    [Fact]
    public void Sheet_border_runs_along_the_edge()
    {
        var s = new SheetSettings { SheetWidth = 300, Margin = 10, SheetBorderWidth = 3, SheetBorderColor = "#FF0000FF" };
        var (sheet, _) = Render(s);
        using (sheet)
        {
            Assert.Equal(new SKColor(0, 0, 255), sheet.GetPixel(1, sheet.Height / 2));
            Assert.Equal(new SKColor(0, 0, 255), sheet.GetPixel(sheet.Width - 2, sheet.Height - 2));
            Assert.Equal(new SKColor(0, 0, 0), sheet.GetPixel(6, sheet.Height / 2));
        }
    }

    [Fact]
    public void Tile_style_cli_options()
    {
        var s = new SheetSettings();
        foreach (var apply in ArgParser.ParseGenerate(["--tile-border", "2", "--tile-border-color", "#FF112233", "--tile-shadow-offset", "6",
                                                       "--grayscale", "--corners", "rounded", "--corner-radius", "9", "--sheet-border", "5"]).Overrides) apply(s);
        Assert.Equal(2, s.TileStyle.BorderWidth);
        Assert.Equal("#FF112233", s.TileStyle.BorderColor);
        Assert.True(s.TileStyle.Shadow); // an offset switches the shadow on
        Assert.Equal(6, s.TileStyle.ShadowOffset);
        Assert.True(s.TileStyle.Grayscale);
        Assert.Equal(TileCorners.Rounded, s.TileStyle.Corners);
        Assert.Equal(9, s.TileStyle.CornerRadius);
        Assert.Equal(5, s.SheetBorderWidth);
        foreach (var apply in ArgParser.ParseGenerate(["--no-tile-border", "--no-tile-shadow", "--no-grayscale", "--no-sheet-border"]).Overrides) apply(s);
        Assert.Equal(0, s.TileStyle.BorderWidth);
        Assert.False(s.TileStyle.Shadow);
        Assert.False(s.TileStyle.Grayscale);
        Assert.Equal(0, s.SheetBorderWidth);
        Assert.Throws<UsageException>(() => ArgParser.ParseGenerate(["--corners", "star"]));
    }

    // ------------------------------------------------------------------ #28 comment

    [Fact]
    public void Comment_goes_below_the_header_or_where_the_template_says()
    {
        Assert.Equal("{filename}\n{duration}\n{comment}", InfoTemplate.WithComment("{filename}\n{duration}\n", "Holiday"));
        Assert.Equal("{filename}\n{comment}\n{duration}", InfoTemplate.WithComment("{filename}\n{comment}\n{duration}", "Holiday"));
        // Without a comment a line holding only the placeholder disappears; other text around it stays.
        Assert.Equal("{filename}\n{duration}", InfoTemplate.WithComment("{filename}\n{comment}\n{duration}", null));
        Assert.Equal("{filename}\nNote: {comment}", InfoTemplate.WithComment("{filename}\nNote: {comment}", " "));
        var info = TestUtil.SampleInfo();
        Assert.Equal("My Movie.mkv – Holiday\nline 2", InfoTemplate.Expand("{filename} – {comment}", info, "en", "Holiday\nline 2"));
    }

    [Fact]
    public void Comment_adds_header_lines()
    {
        var s = new SheetSettings();
        var info = TestUtil.SampleInfo();
        var plain = SheetRenderer.MeasureHeader(s, info);
        s.Header.Comment = "Holiday 2026\nSource: camera";
        var lineHeight = s.Header.FontSize * s.Header.LineSpacing;
        Assert.InRange(SheetRenderer.MeasureHeader(s, info) - plain, 2 * lineHeight - 1, 2 * lineHeight + 1);
        s.Header.Enabled = false;
        Assert.Equal(0, SheetRenderer.MeasureHeader(s, info)); // part of the header
    }

    [Fact]
    public void Comment_cli_option_and_preset()
    {
        var s = new SheetSettings();
        foreach (var apply in ArgParser.ParseGenerate(["--comment", "A\\nB"]).Overrides) apply(s);
        Assert.Equal("A\nB", s.Header.Comment);
        var json = System.Text.Json.JsonSerializer.Serialize(s, SettingsJson.Options);
        Assert.Contains("\"comment\": \"A\\nB\"", json);
        foreach (var apply in ArgParser.ParseGenerate(["--no-comment"]).Overrides) apply(s);
        Assert.Null(s.Header.Comment);
    }

    // ------------------------------------------------------------------ #29 watermark

    [Fact]
    public void Picture_watermark_sits_in_the_chosen_corner()
    {
        var dir = TestUtil.TempDir();
        var s = new SheetSettings { SheetWidth = 400, Margin = 10 };
        s.Watermark.ImagePath = MakeImage(dir, new SKColor(0, 255, 0));
        s.Watermark.Opacity = 1;
        s.Watermark.Size = 0.25;
        s.Watermark.Text = "ignored, the picture wins";
        var (sheet, _) = Render(s);
        using (sheet)
        {
            var m = (int)Math.Ceiling(Math.Min(sheet.Width, sheet.Height) * 0.03);
            // 100 x 50 px at the bottom right, a few pixels from the edge.
            Assert.Equal(new SKColor(0, 255, 0), sheet.GetPixel(sheet.Width - m - 50, sheet.Height - m - 25));
            Assert.NotEqual(new SKColor(0, 255, 0), sheet.GetPixel(sheet.Width - m - 110, sheet.Height - m - 25));
            Assert.NotEqual(new SKColor(0, 255, 0), sheet.GetPixel(m + 50, m + 25));
        }
    }

    [Fact]
    public void Text_watermark_with_opacity_and_per_tile()
    {
        var s = new SheetSettings { SheetWidth = 600, Margin = 10, Gap = 10 };
        var (plain, l) = Render(s, 2, 1);
        s.Watermark.Text = "WATERMARK";
        s.Watermark.Position = WatermarkPosition.Center;
        s.Watermark.Size = 0.8;
        s.Watermark.Opacity = 0.5;
        s.Watermark.PerTile = true;
        var (marked, _) = Render(s, 2, 1);
        using (plain) using (marked)
        {
            foreach (var t in l.Tiles)
                Assert.True(Differs(plain, marked, new SKRectI(t.X, t.Y, t.X + t.Width, t.Y + t.Height)), $"no watermark in tile at {t.X}");
            // Half opacity: the brightest pixel is a mix of white and red, not white.
            var brightest = Pixels(marked, new SKRectI(l.Tiles[0].X, l.Tiles[0].Y, l.Tiles[0].X + l.Tiles[0].Width, l.Tiles[0].Y + l.Tiles[0].Height))
                .Max(p => p.Green);
            Assert.InRange(brightest, 120, 160);
            Assert.False(Differs(plain, marked, new SKRectI(0, 0, l.Tiles[0].X, plain.Height))); // margin untouched
        }
    }

    [Fact]
    public void Tiled_watermark_repeats_over_the_sheet()
    {
        var s = new SheetSettings { SheetWidth = 800 };
        var (plain, _) = Render(s);
        s.Watermark.Text = "©";
        s.Watermark.Position = WatermarkPosition.Tiled;
        s.Watermark.Size = 0.05;
        s.Watermark.Opacity = 1;
        var (marked, _) = Render(s);
        using (plain) using (marked)
        {
            var w = plain.Width; var h = plain.Height;
            Assert.True(Differs(plain, marked, new SKRectI(0, 0, w / 3, h / 3)));
            Assert.True(Differs(plain, marked, new SKRectI(2 * w / 3, 2 * h / 3, w, h)));
        }
    }

    [Fact]
    public void Watermark_picture_path_is_relative_to_an_exported_preset_and_cli_options()
    {
        var dir = TestUtil.TempDir();
        var img = MakeImage(dir, SKColors.White, "logo.png");
        var s = new SheetSettings { Name = "Wm" };
        s.Watermark.ImagePath = img;
        var file = Path.Combine(dir, "wm" + PresetStore.FileExtension);
        PresetStore.ExportFile(s, file);
        Assert.Contains("\"logo.png\"", File.ReadAllText(file));
        Assert.Equal(img, PresetStore.LoadFile(file).Watermark.ImagePath);

        var c = new SheetSettings();
        foreach (var apply in ArgParser.ParseGenerate(["--watermark", "© me", "--watermark-pos", "top-left", "--watermark-size", "30",
                                                       "--watermark-opacity", "0.7", "--watermark-per-tile"]).Overrides) apply(c);
        Assert.Equal("© me", c.Watermark.Text);
        Assert.Equal(WatermarkPosition.TopLeft, c.Watermark.Position);
        Assert.Equal(0.3, c.Watermark.Size, 3);
        Assert.Equal(0.7, c.Watermark.Opacity, 3);
        Assert.True(c.Watermark.PerTile);
        foreach (var apply in ArgParser.ParseGenerate(["--watermark-image", img]).Overrides) apply(c);
        Assert.Equal(img, c.Watermark.ImagePath);
        foreach (var apply in ArgParser.ParseGenerate(["--no-watermark"]).Overrides) apply(c);
        Assert.False(c.Watermark.Enabled);
        Assert.Throws<UsageException>(() => ArgParser.ParseGenerate(["--watermark-image", Path.Combine(dir, "missing.png")]));
    }

    private static IEnumerable<SKColor> Pixels(SKBitmap b, SKRectI r)
    {
        for (var y = r.Top; y < r.Bottom; y++)
            for (var x = r.Left; x < r.Right; x++)
                yield return b.GetPixel(x, y);
    }

    private static bool Differs(SKBitmap a, SKBitmap b, SKRectI r) => Pixels(a, r).Zip(Pixels(b, r)).Any(p => p.First != p.Second);

    // ------------------------------------------------------------------ #30 formats and sound

    [Theory]
    [InlineData(AnimationFormat.H265, "libx265", ".mp4")]
    [InlineData(AnimationFormat.Webp, "libwebp_anim", ".webp")]
    [InlineData(AnimationFormat.Apng, "apng", ".png")]
    [InlineData(AnimationFormat.Ogv, "libtheora", ".ogv")]
    public void Encoder_arguments_for_new_formats(AnimationFormat format, string encoder, string ext)
    {
        var s = new SheetSettings();
        s.Animation.Enabled = true;
        s.Animation.Format = format;
        var args = AnimationEncoder.BuildArguments("out.part", s, 961, 541);
        Assert.Contains(encoder, args);
        Assert.Equal("out.part", args[^1]);
        Assert.Equal(ext, SheetRenderer.Extension(s));
        Assert.Equal(format is AnimationFormat.H265 or AnimationFormat.Ogv, args.Any(a => a.StartsWith("pad=", StringComparison.Ordinal)));
    }

    [Fact]
    public void Webp_falls_back_to_the_older_encoder_and_missing_encoders_are_named()
    {
        IReadOnlySet<string> old = new HashSet<string> { "libwebp", "libx264", "aac", "libvorbis" };
        Assert.Equal("libwebp", AnimationEncoder.VideoEncoder(AnimationFormat.Webp, old));
        var s = new SheetSettings();
        s.Animation.Format = AnimationFormat.Webp;
        Assert.Null(AnimationEncoder.MissingEncoder(s, false, old));
        s.Animation.Format = AnimationFormat.Ogv;
        Assert.Equal("libtheora", AnimationEncoder.MissingEncoder(s, true, old));
        s.Animation.Format = AnimationFormat.Webm;
        Assert.Equal("libvpx-vp9", AnimationEncoder.MissingEncoder(s, true, old));
        Assert.Equal("libvorbis", AnimationEncoder.AudioEncoder(AnimationFormat.Webm, old)); // no libopus
        Assert.Null(AnimationEncoder.MissingEncoder(s, true, new HashSet<string>())); // unknown: let ffmpeg decide

        var listing = "Encoders:\n V..... = Video\n ------\n V....D libx264              libx264 H.264\n A....D aac                  AAC\n";
        Assert.Equal(["aac", "libx264"], AnimationEncoder.ParseEncoders(listing).Order());
    }

    [Fact]
    public void Audio_comes_from_the_biggest_tile_and_only_where_the_format_allows()
    {
        var s = new SheetSettings { Template = BuiltInTemplates.Find("BigCenter") };
        var l = GridLayout.Create(s, 16.0 / 9, 0);
        var clips = l.Tiles.Select(_ => (IReadOnlyList<byte[]>)[[1]]).ToList();
        var big = Enumerable.Range(0, l.Tiles.Count).MaxBy(i => l.Tiles[i].Width);
        Assert.Equal(big, SheetGenerator.AudioTile(l, clips));
        var plain = GridLayout.Create(new SheetSettings(), 16.0 / 9, 0);
        var plainClips = plain.Tiles.Select((_, i) => (IReadOnlyList<byte[]>)(i == 0 ? [] : [[1]])).ToList();
        Assert.Equal(1, SheetGenerator.AudioTile(plain, plainClips)); // the first tile has no clip

        s.Animation.Format = AnimationFormat.Mp4;
        var args = AnimationEncoder.BuildArguments("o", s, 100, 100, new AudioClip("/v/a.mkv", 12.5, 3));
        Assert.Contains("1:a:0?", args);
        Assert.Contains("aac", args);
        Assert.Equal("12.5", args[args.IndexOf("-ss") + 1]);
        s.Animation.Format = AnimationFormat.Gif;
        Assert.DoesNotContain("-map", AnimationEncoder.BuildArguments("o", s, 100, 100, new AudioClip("/v/a.mkv", 12.5, 3)));
        Assert.False(AnimationSettings.SupportsAudio(AnimationFormat.Webp));
        Assert.True(AnimationSettings.SupportsAudio(AnimationFormat.Ogv));
    }

    [Fact]
    public void Cli_parses_new_animation_formats_and_audio()
    {
        foreach (var (arg, f) in new[] { ("h265", AnimationFormat.H265), ("hevc", AnimationFormat.H265), ("webp", AnimationFormat.Webp),
                                         ("apng", AnimationFormat.Apng), ("ogv", AnimationFormat.Ogv) })
            Assert.Equal(f, ArgParser.ParseAnimationFormat(arg));
        var s = new SheetSettings();
        foreach (var apply in ArgParser.ParseGenerate(["-a", "webm", "--audio"]).Overrides) apply(s);
        Assert.True(s.Animation.Audio);
        foreach (var apply in ArgParser.ParseGenerate(["--no-audio"]).Overrides) apply(s);
        Assert.False(s.Animation.Audio);
    }

    private static FfmpegTools Tools()
    {
        var t = FfmpegLocator.FromPath();
        if (t is null) Assert.Skip("ffmpeg/ffprobe not on PATH");
        return t;
    }

    [Theory]
    [InlineData(AnimationFormat.H265)]
    [InlineData(AnimationFormat.Webp)]
    [InlineData(AnimationFormat.Apng)]
    [InlineData(AnimationFormat.Ogv)]
    public async Task Creates_animated_sheet_in_new_formats(AnimationFormat format)
    {
        var t = Tools();
        var s = new SheetSettings { Columns = 2, Rows = 2, SheetWidth = 401, Gap = 4, Margin = 6 };
        s.Animation.Enabled = true;
        s.Animation.Format = format;
        s.Animation.ClipSeconds = 1;
        s.Animation.FramesPerSecond = 5;
        if (AnimationEncoder.MissingEncoder(s, false, await AnimationEncoder.GetEncodersAsync(t.FfmpegPath)) is { } missing)
            Assert.Skip($"ffmpeg has no {missing}");
        s.TileStyle.Corners = TileCorners.Rounded;
        s.Watermark.Text = "test";
        var dir = TestUtil.TempDir();
        var video = Path.Combine(dir, "clip.mp4");
        Assert.True(TestUtil.Run(t.FfmpegPath, "-v", "error", "-y", "-f", "lavfi", "-i", "testsrc2=size=320x180:rate=25:duration=10", "-pix_fmt", "yuv420p", video));
        var job = new ThumbnailJob(video, dir);
        await new BatchProcessor(t).RunAsync([job], s, new BatchOptions { MaxParallelProcesses = 2 }, TestContext.Current.CancellationToken);

        Assert.True(job.Status == JobStatus.Done, job.Message);
        Assert.EndsWith(".sheet" + SheetRenderer.Extension(format), job.OutputPath);
        var bytes = await File.ReadAllBytesAsync(job.OutputPath!, TestContext.Current.CancellationToken);
        if (format == AnimationFormat.Webp)
        {
            // Older ffprobe builds cannot read animated WebP: count the frame chunks instead.
            Assert.Equal("WEBP", System.Text.Encoding.ASCII.GetString(bytes, 8, 4));
            Assert.Equal(5, CountOf(bytes, "ANMF"u8.ToArray()));
            return;
        }
        var probe = await Core.Util.ProcessRunner.RunAsync(t.FfprobePath,
            ["-v", "error", "-count_frames", "-select_streams", "v:0", "-show_entries", "stream=codec_name,nb_read_frames", "-of", "csv=p=0", job.OutputPath!],
            TestContext.Current.CancellationToken);
        var fields = System.Text.Encoding.UTF8.GetString(probe.StdOut).Trim().Split(',');
        Assert.Equal(format switch { AnimationFormat.H265 => "hevc", AnimationFormat.Apng => "apng", _ => "theora" }, fields[0]);
        Assert.Equal(5, int.Parse(fields[1], CultureInfo.InvariantCulture));
    }

    private static int CountOf(byte[] data, byte[] pattern)
    {
        var n = 0;
        for (var i = 0; i + pattern.Length <= data.Length; i++)
            if (data.AsSpan(i, pattern.Length).SequenceEqual(pattern)) n++;
        return n;
    }

    [Fact]
    public async Task Animated_sheet_with_sound()
    {
        var t = Tools();
        var dir = TestUtil.TempDir();
        var video = Path.Combine(dir, "clip.mp4");
        Assert.True(TestUtil.Run(t.FfmpegPath, "-v", "error", "-y", "-f", "lavfi", "-i", "testsrc2=size=320x180:rate=25:duration=10",
                                 "-f", "lavfi", "-i", "sine=frequency=440:duration=10", "-pix_fmt", "yuv420p", "-c:a", "aac", "-shortest", video));
        var s = new SheetSettings { Columns = 2, Rows = 1, SheetWidth = 400 };
        s.Animation.Enabled = true;
        s.Animation.ClipSeconds = 2;
        s.Animation.FramesPerSecond = 5;
        s.Animation.Audio = true;
        var job = new ThumbnailJob(video, dir);
        await new BatchProcessor(t).RunAsync([job], s, new BatchOptions { MaxParallelProcesses = 2 }, TestContext.Current.CancellationToken);
        Assert.True(job.Status == JobStatus.Done, job.Message);
        var probe = await Core.Util.ProcessRunner.RunAsync(t.FfprobePath,
            ["-v", "error", "-show_entries", "stream=codec_type,codec_name:format=duration", "-of", "csv=p=0", job.OutputPath!], TestContext.Current.CancellationToken);
        var text = System.Text.Encoding.UTF8.GetString(probe.StdOut);
        Assert.Contains("aac,audio", text);
        Assert.Contains("h264,video", text);
        var duration = double.Parse(text.Trim().Split('\n')[^1], CultureInfo.InvariantCulture);
        Assert.InRange(duration, 1.8, 2.3);

        // GIF cannot hold sound: the setting is ignored, the sheet is made.
        s.Animation.Format = AnimationFormat.Gif;
        var gif = new ThumbnailJob(video, dir);
        await new BatchProcessor(t).RunAsync([gif], s, new BatchOptions { MaxParallelProcesses = 2 }, TestContext.Current.CancellationToken);
        Assert.Equal(JobStatus.Done, gif.Status);
    }
}
