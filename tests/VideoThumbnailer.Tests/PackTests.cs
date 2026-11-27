using SkiaSharp;
using VideoThumbnailer.Cli;
using VideoThumbnailer.Core.Batch;
using VideoThumbnailer.Core.Ffmpeg;
using VideoThumbnailer.Core.Settings;

namespace VideoThumbnailer.Tests;

/// <summary>Pack picture of many videos (#37).</summary>
public class PackTests
{
    private static FfmpegTools Tools()
    {
        var t = FfmpegLocator.FromPath();
        if (t is null) Assert.Skip("ffmpeg/ffprobe not on PATH");
        return t;
    }

    private static string MakeVideo(FfmpegTools t, string dir, string name, string size, int seconds)
    {
        var path = Path.Combine(dir, name);
        Assert.True(TestUtil.Run(t.FfmpegPath, "-v", "error", "-y", "-f", "lavfi", "-i", $"testsrc2=size={size}:rate=25:duration={seconds}", "-pix_fmt", "yuv420p", path));
        return path;
    }

    [Fact]
    public void Geometry_and_splitting()
    {
        var s = new SheetSettings { Margin = 10, Gap = 6 };
        var g = PackGenerator.Measure(s, 6, 1200, ImageFormat.Webp);
        Assert.Equal(6, g.TilesPerRow);
        Assert.Equal((1200 - 20 - 30) / 6, g.TileWidth);
        Assert.Equal(g.Margin * 2 + 6 * g.TileWidth + 5 * g.Gap, g.Width);
        Assert.True(g.RowsPerFile * g.RowHeight <= 16383);
        Assert.True(PackGenerator.Measure(s, 6, 1200, ImageFormat.Jpeg).RowsPerFile > g.RowsPerFile);

        Assert.Equal(["/p/pack.jpg"], PackGenerator.FileNames("/p/pack.jpg", 1).Select(f => f.Replace('\\', '/')));
        Assert.Equal(["/p/pack-1.webp", "/p/pack-2.webp"], PackGenerator.FileNames("/p/pack.webp", 2).Select(f => f.Replace('\\', '/')));
        Assert.Equal(ImageFormat.Png, PackGenerator.FormatFor("a.PNG"));
        Assert.Equal((100, 56), PackGenerator.Fit(16.0 / 9, 100, 56));
        Assert.Equal((32, 56), PackGenerator.Fit(9.0 / 16, 100, 56)); // portrait: fitted to the height
    }

    [Fact]
    public async Task Pack_of_three_videos_with_a_broken_one()
    {
        var t = Tools();
        var dir = TestUtil.TempDir();
        var a = MakeVideo(t, dir, "Folge 1.mp4", "320x180", 6);
        var b = MakeVideo(t, dir, "Folge 2.mp4", "180x320", 6);
        var broken = Path.Combine(dir, "kaputt.mp4");
        File.WriteAllText(broken, "no video");
        var s = new SheetSettings { Margin = 8, Gap = 4 };
        var target = Path.Combine(dir, "out", "pack.png");
        using var limiter = new SemaphoreSlim(4);
        var result = await new PackGenerator(t, limiter).GenerateAsync([a, broken, b], s, target, 4, 800, null, TestContext.Current.CancellationToken);
        Assert.Equal([target], result.Files);
        Assert.Equal(2, result.Videos);
        Assert.Single(result.Failed);
        using var bmp = SKBitmap.Decode(target);
        var g = PackGenerator.Measure(s, 4, 800, ImageFormat.Png);
        Assert.Equal((g.Width, 2 * g.Margin + 2 * g.RowHeight - g.Gap), (bmp.Width, bmp.Height));
    }

    // AvaloniaFact: runs on the UI thread like the GUI tests, which also change AppPaths.SetRootOverride.
    [Avalonia.Headless.XUnit.AvaloniaFact]
    public async Task Pack_on_the_command_line()
    {
        var t = Tools();
        var dir = TestUtil.TempDir();
        MakeVideo(t, dir, "a.mp4", "320x180", 4);
        MakeVideo(t, dir, "b.mp4", "320x180", 4);
        var target = Path.Combine(dir, "season.jpg");
        Core.AppPaths.SetRootOverride(TestUtil.TempDir());
        try
        {
            var code = await CliApp.RunAsync(["pack", dir, "-o", target, "--tiles", "3", "-w", "600", "--ffmpeg", Path.GetDirectoryName(t.FfmpegPath)!, "--quiet"]);
            Assert.Equal(0, code);
            using var bmp = SKBitmap.Decode(target);
            Assert.Equal(PackGenerator.Measure(BuiltInPresets.Default, 3, 600, ImageFormat.Jpeg).Width, bmp.Width);
            Assert.Equal(2, await CliApp.RunAsync(["pack", dir]));
        }
        finally { Core.AppPaths.SetRootOverride(null); }
    }
}
