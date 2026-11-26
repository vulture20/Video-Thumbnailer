using SkiaSharp;
using VideoThumbnailer.Cli;
using VideoThumbnailer.Core.Batch;
using VideoThumbnailer.Core.Extraction;
using VideoThumbnailer.Core.Ffmpeg;
using VideoThumbnailer.Core.Layout;
using VideoThumbnailer.Core.Media;
using VideoThumbnailer.Core.Rendering;
using VideoThumbnailer.Core.Settings;

namespace VideoThumbnailer.Tests;

/// <summary>Issue #22 (black frames) and #23 (capture times by hand).</summary>
public class CaptureTimeTests
{
    // ------------------------------------------------------------------ #23 parsing and planning

    [Fact]
    public void Parses_time_lists_for_the_command_line()
    {
        var list = ManualTime.ParseList("1:00, 5:30+2 ,-, auto,1:02:10.5, 90,5:7");
        Assert.Equal(7, list.Length);
        Assert.Equal(new ManualTime(60), list[0]);
        Assert.Equal(new ManualTime(330, 2), list[1]);
        Assert.Null(list[2]);
        Assert.Null(list[3]);
        Assert.Equal(3730.5, list[4]!.Time, 3);
        Assert.Equal(90, list[5]!.Time);
        Assert.Equal(307, list[6]!.Time);
        Assert.True(ManualTime.ParseList("10,-,").SequenceEqual([new ManualTime(10)]));
        Assert.Throws<FormatException>(() => ManualTime.ParseList("1:00,x"));
        Assert.Throws<FormatException>(() => ManualTime.ParseList("0:61"));
        Assert.True(ManualTime.TryParseTime("12,5", out var comma));
        Assert.Equal(12.5, comma);
        Assert.Equal("00:05:30+2", new ManualTime(330, 2).ToString());
        Assert.Equal("01:02:10.500", ManualTime.FormatTime(3730.5));
        Assert.Equal("00:01:00,-,00:00:03.250", ManualTime.FormatList([new ManualTime(60), null, new ManualTime(3.25)]));

        var o = ArgParser.ParseGenerate(["--times", "0:10,-,0:30", "a.mp4"]);
        Assert.True(o.ManualTimes!.SequenceEqual([new ManualTime(10), null, new ManualTime(30)]));
    }

    [Fact]
    public void Manual_times_replace_planned_ones_by_tile_and_are_clamped()
    {
        var plan = TimestampPlanner.Plan(100, 4, 0, 0, [null, new ManualTime(70, 2), null, new ManualTime(500), new ManualTime(1)]);
        Assert.Equal([12.5, 70, 62.5, 100], plan.Select(p => p.Time));
        Assert.Equal([false, true, false, true], plan.Select(p => p.Manual));
        Assert.Equal(2, plan[1].ClipSeconds);
        // The fifth entry has no tile: ignored.
        Assert.Equal(4, plan.Length);
        Assert.Equal(TimestampPlanner.Plan(100, 4, 0, 0), TimestampPlanner.Plan(100, 4, 0, 0, null).Select(p => p.Time));
    }

    [Fact]
    public void Preview_shows_manual_times_and_reports_tile_rectangles()
    {
        var info = TestUtil.SampleInfo();
        var s = new SheetSettings { Columns = 2, Rows = 2 };
        var plan = SheetPreview.PlanTimes(s, info, [new ManualTime(42)]);
        Assert.Equal(42, plan[0].Time);
        CapturedFrame? Seen(PlannedTime p) { Assert.True(p.Manual == (p.Time == 42)); return null; }
        using var r = SheetPreview.Render(s, info, 2000, Seen, [new ManualTime(42)]);
        Assert.Equal(4, r.TileRects!.Count);
        Assert.True(r.TileRects[1].X > r.TileRects[0].X);
    }

    // ------------------------------------------------------------------ #22 detection

    private static SKBitmap Solid(SKColor c, int w = 64, int h = 36)
    {
        var b = new SKBitmap(w, h);
        b.Erase(c);
        return b;
    }

    [Fact]
    public void Detects_black_and_single_colored_frames_but_not_dark_scenes_with_content()
    {
        using (var black = Solid(SKColors.Black)) Assert.True(BlankFrames.IsBlank(black));
        using (var grey = Solid(new SKColor(10, 10, 10))) Assert.True(BlankFrames.IsBlank(grey));
        using (var white = Solid(SKColors.White)) Assert.True(BlankFrames.IsBlank(white));
        using (var blue = Solid(new SKColor(20, 60, 200))) Assert.True(BlankFrames.IsBlank(blue));
        using (var credits = Solid(SKColors.Black, 200, 100))
        {
            // White text lines on black (credits) are content.
            using var canvas = new SKCanvas(credits);
            using var paint = new SKPaint { Color = SKColors.White };
            for (var y = 10; y < 100; y += 20) canvas.DrawRect(30, y, 140, 6, paint);
            Assert.False(BlankFrames.IsBlank(credits));
        }
        using (var scene = SheetPreview.Scene(3, 160, 90)) Assert.False(BlankFrames.IsBlank(scene));

        Assert.Equal([10.5, 9.5, 11, 9, 11.5, 8.5, 12, 8], BlankFrames.Candidates(10, 2, 100));
        Assert.Equal([0.5, 1], BlankFrames.Candidates(0, 1, 100));
        Assert.Equal([9.5, 9], BlankFrames.Candidates(10, 1, 10));
        Assert.Empty(BlankFrames.Candidates(10, 0, 100));
    }

    // ------------------------------------------------------------------ integration with ffmpeg

    /// <summary>12 s test picture that is completely black from 4.2 s to 5.8 s (a "fade").</summary>
    private static string MakeVideoWithBlackPart(FfmpegTools t, string dir)
    {
        var path = Path.Combine(dir, "with-black.mp4");
        Assert.True(TestUtil.Run(t.FfmpegPath, "-v", "error", "-y", "-f", "lavfi", "-i", "testsrc2=size=320x180:rate=25:duration=12",
            "-vf", "drawbox=x=0:y=0:w=iw:h=ih:color=black:t=fill:enable='between(t,4.2,5.8)'", "-pix_fmt", "yuv420p", path));
        return path;
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Sheet_skips_black_frames_unless_switched_off_and_keeps_times_chosen_by_hand(bool skipBlack)
    {
        var t = PreviewSourceTests.Tools();
        var dir = TestUtil.TempDir();
        var video = MakeVideoWithBlackPart(t, dir);
        var s = new SheetSettings { Columns = 2, Rows = 1, SheetWidth = 660, Margin = 10, Gap = 0, SkipStartPercent = 0, SkipEndPercent = 0, SkipBlackFrames = skipBlack };
        s.Header.Enabled = false;
        s.Timestamp.Enabled = false;
        s.Output.Format = ImageFormat.Png;
        s.Output.SaveFrames = true;
        s.Output.Location = OutputLocation.OutputFolder;
        s.Output.Folder = Path.Combine(dir, "out");

        // Tile 1 by hand at 5 s (black, but kept as chosen), tile 2 automatic at 9 s.
        var job = new ThumbnailJob(video) { ManualTimes = [new ManualTime(5)] };
        var stats = await new BatchProcessor(t).RunAsync([job], s, new BatchOptions(), TestContext.Current.CancellationToken);
        Assert.True(stats.Done == 1, job.Message);
        using (var sheet = SKBitmap.Decode(job.OutputPath))
        {
            using var left = sheet.ExtractTile(0);
            Assert.True(BlankFrames.IsBlank(left), "the time chosen by hand is kept even though it is black");
        }
        var frames = Directory.GetFiles(Path.Combine(s.Output.Folder, "with-black_frames")).Select(Path.GetFileName).Order().ToList();
        Assert.Equal(["with-black_001_00-00-05.png", "with-black_002_00-00-09.png"], frames);

        // Automatic tile planned on the black part: replaced only when the setting is on.
        var auto = new ThumbnailJob(video);
        s.Columns = 1;
        s.SheetWidth = 340;
        s.SkipEndPercent = 100.0 * 2 / 12; // usable part 0..10 s, the single tile sits in the middle at 5 s
        s.Output.Folder = Path.Combine(dir, "out2");
        stats = await new BatchProcessor(t).RunAsync([auto], s, new BatchOptions(), TestContext.Current.CancellationToken);
        Assert.True(stats.Done == 1, auto.Message);
        var single = Directory.GetFiles(Path.Combine(s.Output.Folder, "with-black_frames")).Select(Path.GetFileName).Single();
        Assert.Equal(skipBlack ? "with-black_001_00-00-06.png" : "with-black_001_00-00-05.png", single);
    }

    [Fact]
    public async Task Animated_sheet_moves_a_clip_off_black_and_uses_clip_lengths_chosen_by_hand()
    {
        var t = PreviewSourceTests.Tools();
        var dir = TestUtil.TempDir();
        var video = MakeVideoWithBlackPart(t, dir);
        var s = new SheetSettings { Columns = 2, Rows = 1, SheetWidth = 340, SkipStartPercent = 0, SkipEndPercent = 0 };
        s.Header.Enabled = false;
        s.Animation.Enabled = true;
        s.Animation.Format = AnimationFormat.Mp4;
        s.Animation.ClipSeconds = 1;
        s.Animation.FramesPerSecond = 5;
        s.Output.Location = OutputLocation.OutputFolder;
        s.Output.Folder = dir;
        var job = new ThumbnailJob(video) { ManualTimes = [new ManualTime(1, 2.4)] };
        var stats = await new BatchProcessor(t).RunAsync([job], s, new BatchOptions(), TestContext.Current.CancellationToken);
        Assert.True(stats.Done == 1, job.Message);
        var info = await FfprobeReader.ReadAsync(t.FfprobePath, job.OutputPath!, TestContext.Current.CancellationToken);
        // The longest clip (2.4 s chosen by hand) sets the length: 12 frames at 5 fps.
        Assert.InRange(info.Duration, 2.2, 2.6);
    }
}

internal static class BitmapTestExtensions
{
    /// <summary>Left or right half of a two-tile sheet without header (10 px margin).</summary>
    public static SKBitmap ExtractTile(this SKBitmap sheet, int index)
    {
        var w = (sheet.Width - 20) / 2;
        var tile = new SKBitmap(w, sheet.Height - 20);
        sheet.ExtractSubset(tile, new SKRectI(10 + index * w, 10, 10 + (index + 1) * w, sheet.Height - 10));
        return tile;
    }
}
