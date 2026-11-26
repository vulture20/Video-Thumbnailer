using SkiaSharp;
using VideoThumbnailer.Core.Batch;
using VideoThumbnailer.Core.Ffmpeg;
using VideoThumbnailer.Core.Settings;

namespace VideoThumbnailer.Tests;

/// <summary>End-to-end tests with a real ffmpeg from PATH (skipped when none is installed).</summary>
public class IntegrationTests
{
    private static FfmpegTools Tools()
    {
        var t = FfmpegLocator.FromPath();
        if (t is null) Assert.Skip("ffmpeg/ffprobe not on PATH");
        return t;
    }

    private static string MakeVideo(FfmpegTools t, string dir, string name, string size, int seconds, params string[] extra)
    {
        var path = Path.Combine(dir, name);
        var args = new List<string> { "-v", "error", "-y", "-f", "lavfi", "-i", $"testsrc2=size={size}:rate=25:duration={seconds}" };
        args.AddRange(extra);
        args.Add(path);
        Assert.True(TestUtil.Run(t.FfmpegPath, args.ToArray()), "creating test video failed");
        return path;
    }

    [Theory]
    [InlineData(ImageFormat.Jpeg)]
    [InlineData(ImageFormat.Png)]
    [InlineData(ImageFormat.Webp)]
    public async Task Creates_sheet_with_expected_size(ImageFormat format)
    {
        var t = Tools();
        var dir = TestUtil.TempDir();
        var video = MakeVideo(t, dir, "clip.mp4", "640x360", 12, "-pix_fmt", "yuv420p");
        var s = new SheetSettings { Columns = 3, Rows = 2, SheetWidth = 960, Gap = 4, Margin = 6 };
        s.Output.Format = format;
        s.Output.Location = OutputLocation.SubfolderNextToVideo;

        var job = new ThumbnailJob(video, dir);
        var stats = await new BatchProcessor(t).RunAsync([job], s, new BatchOptions { MaxParallelProcesses = 2 }, TestContext.Current.CancellationToken);

        Assert.Equal(1, stats.Done);
        Assert.Equal(JobStatus.Done, job.Status);
        Assert.True(File.Exists(job.OutputPath), job.Message);
        using var bmp = SKBitmap.Decode(job.OutputPath);
        var tileWidth = (960 - 2 * 6 - 2 * 4) / 3; // 313
        Assert.Equal(2 * 6 + 3 * tileWidth + 2 * 4, bmp.Width);
    }

    [Fact]
    public async Task Skips_existing_and_saves_single_frames_and_handles_portrait()
    {
        var t = Tools();
        var dir = TestUtil.TempDir();
        var video = MakeVideo(t, dir, "portrait.mkv", "360x640", 8);
        var s = new SheetSettings { Columns = 2, Rows = 3, SheetWidth = 600 };
        s.Output.SaveFrames = true;
        s.Output.Format = ImageFormat.Png;

        var first = new ThumbnailJob(video);
        await new BatchProcessor(t).RunAsync([first], s, new BatchOptions(), TestContext.Current.CancellationToken);
        Assert.Equal(JobStatus.Done, first.Status);
        var frames = Directory.GetFiles(Path.Combine(dir, "portrait_frames"));
        Assert.Equal(6, frames.Length);
        using (var f = SKBitmap.Decode(frames[0])) Assert.Equal((360, 640), (f.Width, f.Height)); // full resolution

        using (var sheet = SKBitmap.Decode(first.OutputPath)) Assert.True(sheet.Width < sheet.Height * 2); // 3 columns x 2 rows of portrait tiles

        s.Output.Overwrite = OverwriteMode.Skip;
        var second = new ThumbnailJob(video);
        var stats = await new BatchProcessor(t).RunAsync([second], s, new BatchOptions(), TestContext.Current.CancellationToken);
        Assert.Equal(1, stats.Skipped);
    }

    [Fact]
    public async Task Broken_file_fails_without_stopping_the_batch()
    {
        var t = Tools();
        var dir = TestUtil.TempDir();
        var bad = Path.Combine(dir, "broken.mp4");
        await File.WriteAllTextAsync(bad, "not a video", TestContext.Current.CancellationToken);
        var good = MakeVideo(t, dir, "good.mp4", "320x240", 4, "-pix_fmt", "yuv420p");
        var jobs = new[] { new ThumbnailJob(bad), new ThumbnailJob(good) };
        var stats = await new BatchProcessor(t).RunAsync(jobs, new SheetSettings { Columns = 2, Rows = 2 }, new BatchOptions(), TestContext.Current.CancellationToken);
        Assert.Equal(1, stats.Failed);
        Assert.Equal(1, stats.Done);
        Assert.False(string.IsNullOrEmpty(jobs[0].Message));
    }

    [Fact]
    public async Task Cancel_marks_jobs_cancelled()
    {
        var t = Tools();
        var dir = TestUtil.TempDir();
        var video = MakeVideo(t, dir, "c.mp4", "320x240", 4, "-pix_fmt", "yuv420p");
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var job = new ThumbnailJob(video);
        var stats = await new BatchProcessor(t).RunAsync([job], new SheetSettings(), new BatchOptions(), cts.Token);
        Assert.Equal(1, stats.Cancelled);
        Assert.Equal(JobStatus.Cancelled, job.Status);
    }
}
