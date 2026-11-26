using VideoThumbnailer.Core.Layout;
using VideoThumbnailer.Core.Rendering;
using VideoThumbnailer.Core.Settings;

namespace VideoThumbnailer.Tests;

public class SheetPreviewTests
{
    [Fact]
    public void Preview_has_the_geometry_of_the_real_sheet()
    {
        var s = BuiltInPresets.Default.Clone();
        var info = SheetPreview.SampleInfo("/videos/Lofoten.mp4");
        using var r = SheetPreview.Render(s, info, maxWidth: 10_000);
        var layout = GridLayout.Create(s, 16.0 / 9.0, SheetRenderer.MeasureHeader(s, info));
        Assert.Equal(layout.SheetWidth, r.SheetWidth);
        Assert.Equal(layout.SheetHeight, r.SheetHeight);
        Assert.Equal(20, r.Tiles);
        Assert.Equal(r.SheetWidth, r.Bitmap.Width);
    }

    [Fact]
    public void Preview_is_scaled_down_but_reports_full_size_and_follows_header_switch()
    {
        var s = BuiltInPresets.Default.Clone();
        s.SheetWidth = 3000;
        var info = SheetPreview.SampleInfo("x.mkv");
        using var with = SheetPreview.Render(s, info, maxWidth: 600);
        Assert.Equal(600, with.Bitmap.Width);
        Assert.InRange(with.SheetWidth, 2990, 3000); // tile widths are whole pixels
        s.Header.Enabled = false;
        using var without = SheetPreview.Render(s, info, maxWidth: 600);
        Assert.True(without.SheetHeight < with.SheetHeight);
    }
}

/// <summary>Preview with real frames (needs ffmpeg on PATH, otherwise skipped).</summary>
public class PreviewSourceTests
{
    internal static Core.Ffmpeg.FfmpegTools Tools()
    {
        var t = Core.Ffmpeg.FfmpegLocator.FromPath();
        if (t is null) Assert.Skip("ffmpeg/ffprobe not on PATH");
        return t;
    }

    internal static string MakeVideo(Core.Ffmpeg.FfmpegTools t, string dir, int seconds = 6)
    {
        var path = Path.Combine(dir, "clip.mp4");
        Assert.True(TestUtil.Run(t.FfmpegPath, "-v", "error", "-y", "-f", "lavfi", "-i", $"testsrc2=size=320x180:rate=25:duration={seconds}", path));
        return path;
    }

    [Fact]
    public async Task Loads_real_info_and_frames_once_and_renders_them()
    {
        var t = Tools();
        var video = MakeVideo(t, TestUtil.TempDir());
        var source = new Core.Extraction.PreviewSource(t, 2);
        var info = await source.GetInfoAsync(video, CancellationToken.None);
        Assert.InRange(info.Duration, 5.5, 6.5);
        Assert.Same(info, source.TryGetInfo(video));

        var s = BuiltInPresets.All.First(p => p.Name == "Compact").Clone();
        var times = SheetPreview.PlanTimes(s, info);
        Assert.Equal(9, times.Length);

        using (var before = SheetPreview.Render(s, info, 1200, x => source.TryGetFrame(video, x)))
            Assert.Equal(0, before.RealFrames);

        var calls = 0;
        await source.LoadFramesAsync(info, times, () => Interlocked.Increment(ref calls), CancellationToken.None);
        Assert.Equal(9, calls);
        using (var after = SheetPreview.Render(s, info, 1200, x => source.TryGetFrame(video, x)))
            Assert.Equal(9, after.RealFrames);

        // Cached: nothing is extracted again.
        calls = 0;
        await source.LoadFramesAsync(info, times, () => Interlocked.Increment(ref calls), CancellationToken.None);
        Assert.Equal(0, calls);
    }

    [Fact]
    public async Task Changed_file_is_forgotten_and_read_again()
    {
        var t = Tools();
        var dir = TestUtil.TempDir();
        var video = MakeVideo(t, dir);
        var source = new Core.Extraction.PreviewSource(t, 2);
        var info = await source.GetInfoAsync(video, CancellationToken.None);
        var times = SheetPreview.PlanTimes(BuiltInPresets.Default, info);
        await source.LoadFramesAsync(info, times, null, CancellationToken.None);
        Assert.NotNull(source.TryGetFrame(video, times[0]));
        Assert.False(source.ForgetIfChanged(video));

        MakeVideo(t, dir, seconds: 3); // same path, other content
        File.SetLastWriteTimeUtc(video, DateTime.UtcNow.AddMinutes(1));
        Assert.Null(source.TryGetInfo(video));
        Assert.Null(source.TryGetFrame(video, times[0]));
        var again = await source.GetInfoAsync(video, CancellationToken.None);
        Assert.InRange(again.Duration, 2.5, 3.5);
    }

    [Fact]
    public async Task Unreadable_file_fails_and_is_retried_later()
    {
        var t = Tools();
        var bad = Path.Combine(TestUtil.TempDir(), "broken.mp4");
        File.WriteAllText(bad, "not a video");
        var source = new Core.Extraction.PreviewSource(t);
        await Assert.ThrowsAnyAsync<Exception>(() => source.GetInfoAsync(bad, CancellationToken.None));
        Assert.Null(source.TryGetInfo(bad));
    }
}
