using System.Globalization;
using VideoThumbnailer.Cli;
using VideoThumbnailer.Core.Batch;
using VideoThumbnailer.Core.Extraction;
using VideoThumbnailer.Core.Ffmpeg;
using VideoThumbnailer.Core.Input;
using VideoThumbnailer.Core.Output;
using VideoThumbnailer.Core.Rendering;
using VideoThumbnailer.Core.Settings;

namespace VideoThumbnailer.Tests;

public class AnimationTests
{
    [Fact]
    public void Splits_concatenated_jpegs_at_end_markers()
    {
        byte[] a = [0xFF, 0xD8, 0x01, 0xFF, 0x00, 0x02, 0xFF, 0xD9];
        byte[] b = [0xFF, 0xD8, 0x03, 0xFF, 0xD9];
        var parts = ClipExtractor.SplitJpegs([.. a, .. b]);
        Assert.Equal(2, parts.Count);
        Assert.Equal(a, parts[0]);
        Assert.Equal(b, parts[1]);
        Assert.Empty(ClipExtractor.SplitJpegs([0x00, 0x01]));
    }

    [Theory]
    [InlineData(AnimationFormat.Mp4, "libx264", ".mp4")]
    [InlineData(AnimationFormat.Webm, "libvpx-vp9", ".webm")]
    [InlineData(AnimationFormat.Gif, "gif", ".gif")]
    public void Encoder_arguments_and_extension_match_the_format(AnimationFormat format, string expected, string ext)
    {
        var s = new SheetSettings();
        s.Animation.Enabled = true;
        s.Animation.Format = format;
        var args = AnimationEncoder.BuildArguments("out.part", s, 961, 541);
        Assert.Contains(expected, args);
        Assert.Contains("961x541", args);
        Assert.Equal("out.part", args[^1]);
        Assert.Equal(ext, SheetRenderer.Extension(s));
        if (format != AnimationFormat.Gif) Assert.Contains(args, a => a.StartsWith("pad=ceil(iw/2)*2", StringComparison.Ordinal));
    }

    [Fact]
    public void Animated_sheets_get_a_marker_and_are_skipped_in_folder_scans()
    {
        var dir = TestUtil.TempDir();
        var video = Path.Combine(dir, "movie.mp4");
        File.WriteAllText(video, "");
        var s = new SheetSettings();
        s.Animation.Enabled = true;
        var sheet = OutputPathResolver.ResolveSheetPath(s, video, dir);
        Assert.Equal(Path.Combine(dir, "movie.sheet.mp4"), sheet);
        File.WriteAllText(sheet, "");
        File.WriteAllText(Path.Combine(dir, "movie.sheet (2).webm"), "");

        var found = VideoScanner.Scan([dir], recursive: false);
        Assert.Equal([video], found.Select(f => f.Path));
        Assert.Equal(2, VideoScanner.Scan([video, sheet], recursive: false).Count); // explicit files are always taken
    }

    [Fact]
    public void Cli_parses_animation_options()
    {
        var o = ArgParser.ParseGenerate(["-a", "gif", "--clip-seconds", "2.5", "--fps", "8", "x.mp4"]);
        var s = new SheetSettings();
        foreach (var apply in o.Overrides) apply(s);
        Assert.True(s.Animation.Enabled);
        Assert.Equal(AnimationFormat.Gif, s.Animation.Format);
        Assert.Equal(2.5, s.Animation.ClipSeconds);
        Assert.Equal(20, s.Animation.FrameCount);
        Assert.Throws<UsageException>(() => ArgParser.ParseGenerate(["--animated", "avi", "x.mp4"]));
    }

    [Theory]
    [InlineData(AnimationFormat.Mp4)]
    [InlineData(AnimationFormat.Gif)]
    public async Task Creates_animated_sheet_with_expected_frames(AnimationFormat format)
    {
        var t = FfmpegLocator.FromPath();
        if (t is null) Assert.Skip("ffmpeg/ffprobe not on PATH");
        var dir = TestUtil.TempDir();
        var video = Path.Combine(dir, "clip.mp4");
        Assert.True(TestUtil.Run(t.FfmpegPath, "-v", "error", "-y", "-f", "lavfi", "-i", "testsrc2=size=640x360:rate=25:duration=20", "-pix_fmt", "yuv420p", video));

        var s = new SheetSettings { Columns = 3, Rows = 2, SheetWidth = 641, Gap = 4, Margin = 6 };
        s.Animation.Enabled = true;
        s.Animation.Format = format;
        s.Animation.ClipSeconds = 2;
        s.Animation.FramesPerSecond = 5;
        var job = new ThumbnailJob(video, dir);
        await new BatchProcessor(t).RunAsync([job], s, new BatchOptions { MaxParallelProcesses = 2 }, TestContext.Current.CancellationToken);

        Assert.Equal(JobStatus.Done, job.Status);
        Assert.EndsWith(".sheet" + SheetRenderer.Extension(format), job.OutputPath);
        Assert.False(File.Exists(job.OutputPath + ".part"));
        var probe = await Core.Util.ProcessRunner.RunAsync(t.FfprobePath,
            ["-v", "error", "-count_frames", "-select_streams", "v:0", "-show_entries", "stream=nb_read_frames,width", "-of", "csv=p=0", job.OutputPath!],
            TestContext.Current.CancellationToken);
        var fields = System.Text.Encoding.UTF8.GetString(probe.StdOut).Trim().Split(',');
        var width = int.Parse(fields[0], CultureInfo.InvariantCulture);
        Assert.InRange(width, 640, 642); // odd widths are padded to even for MP4
        Assert.Equal(10, int.Parse(fields[1], CultureInfo.InvariantCulture));
    }
}
