using SkiaSharp;
using VideoThumbnailer.Cli;
using VideoThumbnailer.Core.Batch;
using VideoThumbnailer.Core.Ffmpeg;
using VideoThumbnailer.Core.Layout;
using VideoThumbnailer.Core.Rendering;
using VideoThumbnailer.Core.Settings;

namespace VideoThumbnailer.Tests;

/// <summary>#24 special grids, #25 fit to screen, #26 background picture.</summary>
public class SheetLayoutFeatureTests
{
    // ------------------------------------------------------------------ #24 special grids

    [Fact]
    public void Built_in_templates_are_valid_and_cover_their_grid()
    {
        foreach (var t in BuiltInTemplates.All)
        {
            Assert.Empty(t.Validate());
            Assert.Equal(t.Columns * t.Rows, t.Tiles.Sum(x => x.Size * x.Size));
            Assert.Contains(t.Tiles, x => x.Size > 1);
        }
    }

    [Fact]
    public void Template_validation_finds_overlaps_and_tiles_outside()
    {
        var overlap = new GridTemplate { Name = "x", Columns = 3, Rows = 2, Tiles = [new(0, 0, 2), new(1, 1)] };
        Assert.Single(overlap.Validate());
        var outside = new GridTemplate { Name = "x", Columns = 2, Rows = 2, Tiles = [new(1, 1, 2)] };
        Assert.Single(outside.Validate());
        Assert.NotEmpty(new GridTemplate { Name = "x", Columns = 2, Rows = 2 }.Validate());
        Assert.NotEmpty(new SheetSettings { Template = overlap }.Normalize());
        // An unusable template never breaks the layout: the plain grid is used.
        var l = GridLayout.Create(new SheetSettings { Columns = 2, Rows = 2, Template = overlap }, 16.0 / 9, 0);
        Assert.Equal(4, l.Tiles.Count);
        Assert.Null(l.Template);
    }

    [Fact]
    public void Big_tile_spans_cells_and_gaps_and_takes_its_turn_where_it_starts()
    {
        var s = new SheetSettings { Template = BuiltInTemplates.Find("BigTop"), SheetWidth = 1000, Gap = 10, Margin = 20 };
        var l = GridLayout.Create(s, 16.0 / 9, 0);
        Assert.Equal(21, l.Tiles.Count);
        Assert.Equal((4, 6), (l.Columns, l.Rows));
        // (1000 - 40 - 30) / 4 = 232 per cell
        Assert.Equal(232, l.TileWidth);
        // Reading order: small (0,0), big (1,0), small (3,0), small (0,1), small (3,1), then the rows below.
        var big = l.Tiles[1];
        Assert.Equal(2 * 232 + 10, big.Width);
        Assert.Equal(2 * l.TileHeight + 10, big.Height);
        Assert.Equal(20 + 232 + 10, big.X);
        Assert.Equal(l.Tiles[0].Y, big.Y);
        Assert.True(l.Tiles[3].Y > l.Tiles[0].Y && l.Tiles[3].Y < l.Tiles[5].Y);
        // Frames for the big tile are captured big, with the video's aspect ratio.
        Assert.Equal((big.Width, GridLayout.ComputeTileHeight(big.Width, 16.0 / 9)), l.FrameSize(1));
        Assert.Equal((l.TileWidth, l.TileHeight), l.FrameSize(0));
        // Tiles do not overlap.
        for (var i = 0; i < l.Tiles.Count; i++)
            for (var j = i + 1; j < l.Tiles.Count; j++)
            {
                var a = l.Tiles[i]; var b = l.Tiles[j];
                Assert.False(a.X < b.X + b.Width && b.X < a.X + a.Width && a.Y < b.Y + b.Height && b.Y < a.Y + a.Height, $"{i} overlaps {j}");
            }
    }

    [Fact]
    public void Template_is_turned_for_portrait_videos_and_follows_column_order()
    {
        var s = new SheetSettings { Template = BuiltInTemplates.Find("BigTop") };
        Assert.Equal((6, 4), GridLayout.EffectiveGrid(s, 9.0 / 16));
        Assert.Equal((4, 6), GridLayout.EffectiveGrid(s, 16.0 / 9));
        s.PortraitSwapGrid = false;
        Assert.Equal((4, 6), GridLayout.EffectiveGrid(s, 9.0 / 16));

        s.Order = TileOrder.ColumnMajor;
        var l = GridLayout.Create(s, 16.0 / 9, 0);
        // Column by column: (0,0), (0,1), (0,2)… the big tile starts in column 1.
        Assert.Equal(l.Tiles[0].X, l.Tiles[1].X);
        Assert.True(l.Tiles.Select((t, i) => (t, i)).First(x => x.t.Width > l.TileWidth).i == 6);
    }

    [Fact]
    public void Template_round_trips_through_preset_json_and_loads_from_file()
    {
        var dir = TestUtil.TempDir();
        var s = new SheetSettings { Name = "T", Template = new GridTemplate { Name = "Mine", Columns = 3, Rows = 2, Tiles = [new(0, 0, 2), new(2, 0), new(2, 1)] } };
        var file = Path.Combine(dir, "t" + PresetStore.FileExtension);
        PresetStore.SaveFile(s, file);
        var back = PresetStore.LoadFile(file);
        Assert.True(BuiltInTemplates.SameTiles(s.Template, back.Template!));
        Assert.Equal("Mine", back.Template!.Name);
        // --layout accepts a whole preset or a file with just the template.
        Assert.True(BuiltInTemplates.SameTiles(s.Template, GridTemplate.LoadFile(file)));
        var only = Path.Combine(dir, "only.json");
        File.WriteAllText(only, """{ "columns": 2, "rows": 1, "tiles": [ { "x": 0, "y": 0 }, { "x": 1, "y": 0 } ] }""");
        var t = GridTemplate.LoadFile(only);
        Assert.Equal("only", t.Name);
        Assert.Equal(1, t.Tiles[1].Size);
    }

    [Fact]
    public void Cli_layout_option()
    {
        Assert.Equal("BigCenter", ArgParser.ParseLayout("big-center", "--layout")!.Name);
        Assert.Equal("TwoBig", ArgParser.ParseLayout("TwoBig", "--layout")!.Name);
        Assert.Null(ArgParser.ParseLayout("grid", "--layout"));
        Assert.Throws<UsageException>(() => ArgParser.ParseLayout("nope", "--layout"));
        Assert.Equal("big-first", ArgParser.KebabCase("BigFirst"));
        var s = new SheetSettings();
        foreach (var apply in ArgParser.ParseGenerate(["--layout", "big-first", "-c", "3"]).Overrides) apply(s);
        Assert.Equal("BigFirst", s.Template!.Name);
        // {cols}/{rows} in the file name follow the special grid.
        Assert.Equal("5x5", Core.Output.OutputPathResolver.ExpandPattern("{cols}x{rows}", "/v/a.mp4", s));
    }

    // ------------------------------------------------------------------ #25 fit to screen

    [Fact]
    public void Fit_to_screen_width_uses_screen_width()
    {
        var s = new SheetSettings { Columns = 4, Rows = 5, SheetWidth = 1000, Gap = 10, Margin = 20, FitToScreen = ScreenFit.Width };
        var l = GridLayout.Create(s, 16.0 / 9, 50, screen: new ScreenSize(2560, 1400));
        Assert.Equal((2560 - 40 - 30) / 4, l.TileWidth);
        Assert.True(l.SheetWidth <= 2560 && l.SheetWidth > 2550);
        // Without a known screen the fixed width applies.
        var fixedLayout = GridLayout.Create(new SheetSettings { Columns = 4, Rows = 5, SheetWidth = 1000, Gap = 10, Margin = 20 }, 16.0 / 9, 50);
        Assert.Equal(232, fixedLayout.TileWidth);
    }

    [Fact]
    public void Fit_to_screen_width_and_height_fits_the_whole_sheet()
    {
        var s = new SheetSettings { Columns = 4, Rows = 5, Gap = 6, Margin = 10, FitToScreen = ScreenFit.WidthAndHeight };
        var screen = new ScreenSize(1920, 1040);
        foreach (var aspect in new[] { 16.0 / 9, 4.0 / 3, 2.39, 1.0 })
        {
            var l = GridLayout.Create(s, aspect, 120, screen: screen);
            Assert.True(l.SheetWidth <= screen.Width, $"{aspect}: width {l.SheetWidth}");
            Assert.True(l.SheetHeight <= screen.Height, $"{aspect}: height {l.SheetHeight}");
            // As large as possible: one pixel more per tile would not fit any more.
            var bigger = GridLayout.ComputeTileHeight(l.TileWidth + 1, aspect);
            Assert.True(l.TileWidth + 1 > (screen.Width - 20 - 18) / 4 || 5 * bigger + 4 * 6 + 20 + 126 > screen.Height, $"{aspect}: not maximal");
        }
        // Wide screen, 4x5 grid of 16:9: height is the limit, the sheet is narrower than the screen.
        Assert.True(GridLayout.Create(s, 16.0 / 9, 120, screen: screen).SheetWidth < 1900);
    }

    [Fact]
    public void Screen_size_parses_and_cli_options()
    {
        Assert.True(ScreenSize.TryParse("2560x1440", out var a));
        Assert.Equal(new ScreenSize(2560, 1440), a);
        Assert.True(ScreenSize.TryParse("1920 × 1080", out _));
        Assert.False(ScreenSize.TryParse("big", out _));
        Assert.False(ScreenSize.TryParse("10x10", out _));
        var o = ArgParser.ParseGenerate(["--fit-screen", "width", "--screen", "3840x2100"]);
        Assert.Equal(new ScreenSize(3840, 2100), o.Screen);
        var s = new SheetSettings();
        foreach (var apply in o.Overrides) apply(s);
        Assert.Equal(ScreenFit.Width, s.FitToScreen);
        Assert.Throws<UsageException>(() => ArgParser.ParseGenerate(["--screen", "x"]));
        Assert.Throws<UsageException>(() => ArgParser.ParseGenerate(["--fit-screen", "maybe"]));
    }

    // ------------------------------------------------------------------ #26 background picture

    private static string MakeImage(string dir, string name = "bg.png", int w = 40, int h = 20)
    {
        using var bmp = new SKBitmap(w, h);
        using (var c = new SKCanvas(bmp))
        {
            c.Clear(new SKColor(255, 0, 0));
            using var p = new SKPaint { Color = new SKColor(0, 0, 255) };
            c.DrawRect(w / 2f, 0, w / 2f, h, p); // left half red, right half blue
        }
        var path = Path.Combine(dir, name);
        using var fs = File.Create(path);
        bmp.Encode(fs, SKEncodedImageFormat.Png, 100);
        return path;
    }

    private static SKBitmap RenderSheet(SheetSettings s)
    {
        s.Header.Enabled = false;
        s.Timestamp.Enabled = false;
        var info = TestUtil.SampleInfo();
        var layout = GridLayout.Create(s, 16.0 / 9, 0);
        var frames = layout.Tiles.Select(_ => new CapturedFrame(1, null)).ToList();
        return SheetRenderer.Render(s, info, layout, frames);
    }

    [Theory]
    [InlineData(BackgroundImageMode.Fill)]
    [InlineData(BackgroundImageMode.Stretch)]
    [InlineData(BackgroundImageMode.Tile)]
    [InlineData(BackgroundImageMode.Center)]
    public void Background_picture_shows_in_margin_blended_with_the_color(BackgroundImageMode mode)
    {
        var dir = TestUtil.TempDir();
        var s = new SheetSettings { Columns = 1, Rows = 1, SheetWidth = 400, Margin = 60, BackgroundColor = "#FF000000" };
        s.BackgroundImage = new BackgroundImageSettings { Path = MakeImage(dir), Mode = mode, Opacity = 0.5 };
        using var sheet = RenderSheet(s);
        // Left margin, vertical middle: red picture at half strength over black (centered: the picture is
        // smaller than the sheet, so the margin there shows the background color only).
        var left = sheet.GetPixel(5, sheet.Height / 2);
        if (mode == BackgroundImageMode.Center)
        {
            Assert.Equal(new SKColor(0, 0, 0), left);
        }
        else
        {
            Assert.InRange(left.Red, 110, 145);
            Assert.True(left.Blue < 20, left.ToString());
        }
    }

    [Fact]
    public void Missing_background_picture_falls_back_to_the_color()
    {
        var s = new SheetSettings { Columns = 1, Rows = 1, SheetWidth = 300, Margin = 40, BackgroundColor = "#FF102030" };
        s.BackgroundImage.Path = "/does/not/exist.png";
        using var sheet = RenderSheet(s);
        Assert.Equal(new SKColor(0x10, 0x20, 0x30), sheet.GetPixel(3, 3));
        Assert.Null(BackgroundImageCache.Get(s.BackgroundImage.Path));
    }

    [Fact]
    public void Background_picture_path_is_relative_to_an_exported_preset()
    {
        var dir = TestUtil.TempDir();
        var img = MakeImage(Directory.CreateDirectory(Path.Combine(dir, "pics")).FullName);
        var s = new SheetSettings { Name = "Bg" };
        s.BackgroundImage.Path = img;
        var file = Path.Combine(dir, "bg" + PresetStore.FileExtension);
        PresetStore.ExportFile(s, file);
        Assert.Contains("\"pics/bg.png\"", File.ReadAllText(file));
        Assert.Equal(img, PresetStore.LoadFile(file).BackgroundImage.Path);

        // Elsewhere: the absolute path is kept.
        var other = Path.Combine(Directory.CreateDirectory(Path.Combine(TestUtil.TempDir(), "x")).FullName, "o" + PresetStore.FileExtension);
        PresetStore.ExportFile(s, other);
        Assert.Equal(img, PresetStore.LoadFile(other).BackgroundImage.Path);
    }

    [Fact]
    public void Cli_background_options()
    {
        var dir = TestUtil.TempDir();
        var img = MakeImage(dir);
        var s = new SheetSettings();
        foreach (var apply in ArgParser.ParseGenerate(["--background-image", img, "--background-mode", "stretch", "--background-opacity", "30"]).Overrides) apply(s);
        Assert.Equal(img, s.BackgroundImage.Path);
        Assert.Equal(BackgroundImageMode.Stretch, s.BackgroundImage.Mode);
        Assert.Equal(0.3, s.BackgroundImage.Opacity, 3);
        foreach (var apply in ArgParser.ParseGenerate(["--background-image", "none"]).Overrides) apply(s);
        Assert.Null(s.BackgroundImage.Path);
        Assert.Throws<UsageException>(() => ArgParser.ParseGenerate(["--background-image", Path.Combine(dir, "missing.png")]));
        Assert.Throws<UsageException>(() => ArgParser.ParseGenerate(["--background-mode", "zoom"]));
    }

    // ------------------------------------------------------------------ end to end

    private static FfmpegTools Tools()
    {
        var t = FfmpegLocator.FromPath();
        if (t is null) Assert.Skip("ffmpeg/ffprobe not on PATH");
        return t;
    }

    [Fact]
    public async Task Creates_sheet_with_special_grid_and_background_picture()
    {
        var t = Tools();
        var dir = TestUtil.TempDir();
        var video = Path.Combine(dir, "clip.mp4");
        Assert.True(TestUtil.Run(t.FfmpegPath, "-v", "error", "-y", "-f", "lavfi", "-i", "testsrc2=size=640x360:rate=25:duration=8", "-pix_fmt", "yuv420p", video));
        var s = new SheetSettings { Template = BuiltInTemplates.Find("BigCenter"), SheetWidth = 800, Margin = 30 };
        s.BackgroundImage = new BackgroundImageSettings { Path = MakeImage(dir), Opacity = 1 };
        s.Output.Format = ImageFormat.Png;
        var job = new ThumbnailJob(video, dir);
        var stats = await new BatchProcessor(t).RunAsync([job], s, new BatchOptions { MaxParallelProcesses = 2 }, TestContext.Current.CancellationToken);
        Assert.Equal(1, stats.Done);
        Assert.True(string.IsNullOrEmpty(job.Message), job.Message);
        using var bmp = SKBitmap.Decode(job.OutputPath);
        var cell = (800 - 60 - 3 * 6) / 4;
        Assert.Equal(60 + 4 * cell + 3 * 6, bmp.Width);
        // Margin at the left shows the red half of the picture.
        Assert.True(bmp.GetPixel(4, bmp.Height / 2).Red > 200);

        // A picture that has gone missing does not fail the sheet, it is mentioned.
        s.BackgroundImage.Path = Path.Combine(dir, "gone.png");
        var job2 = new ThumbnailJob(video, dir);
        await new BatchProcessor(t).RunAsync([job2], s, new BatchOptions { MaxParallelProcesses = 2 }, TestContext.Current.CancellationToken);
        Assert.Equal(JobStatus.Done, job2.Status);
        Assert.Contains("gone.png", job2.Message);
    }
}
