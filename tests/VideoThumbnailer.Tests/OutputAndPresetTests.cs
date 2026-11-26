using VideoThumbnailer.Core.Output;
using VideoThumbnailer.Core.Settings;

namespace VideoThumbnailer.Tests;

public class OutputAndPresetTests
{
    private static readonly string Root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "vroot"));
    private static readonly string Video = Path.Combine(Root, "Season 1", "ep01.mkv");

    [Fact]
    public void Next_to_video()
    {
        var s = new SheetSettings();
        s.Output.Location = OutputLocation.NextToVideo;
        Assert.Equal(Path.Combine(Root, "Season 1", "ep01.jpg"), OutputPathResolver.ResolveSheetPath(s, Video, Root));
    }

    [Fact]
    public void Subfolder_next_to_video_with_pattern_and_format()
    {
        var s = new SheetSettings { Columns = 3, Rows = 7 };
        s.Output.Location = OutputLocation.SubfolderNextToVideo;
        s.Output.SubfolderName = "thumbs";
        s.Output.FileNamePattern = "{name}_{cols}x{rows}";
        s.Output.Format = ImageFormat.Webp;
        Assert.Equal(Path.Combine(Root, "Season 1", "thumbs", "ep01_3x7.webp"), OutputPathResolver.ResolveSheetPath(s, Video, Root));
    }

    [Fact]
    public void Output_folder_keeps_structure()
    {
        var outDir = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "vout"));
        var s = new SheetSettings();
        s.Output.Location = OutputLocation.OutputFolder;
        s.Output.Folder = outDir;
        Assert.Equal(Path.Combine(outDir, "ep01.jpg"), OutputPathResolver.ResolveSheetPath(s, Video, Root));
        s.Output.KeepFolderStructure = true;
        Assert.Equal(Path.Combine(outDir, "Season 1", "ep01.jpg"), OutputPathResolver.ResolveSheetPath(s, Video, Root));
    }

    [Fact]
    public void Pattern_strips_path_separators()
    {
        var s = new SheetSettings { Name = "a/b" };
        Assert.Equal("ep01_a_b", OutputPathResolver.ExpandPattern("{name}_{preset}", Video, s));
    }

    [Fact]
    public void Make_unique_appends_counter()
    {
        var dir = TestUtil.TempDir();
        var f = Path.Combine(dir, "x.jpg");
        File.WriteAllText(f, "");
        Assert.Equal(Path.Combine(dir, "x (2).jpg"), OutputPathResolver.MakeUnique(f));
    }

    [Fact]
    public void Preset_roundtrip_and_store()
    {
        var store = new PresetStore(TestUtil.TempDir());
        var p = BuiltInPresets.Default;
        p.Name = "Mine";
        p.Columns = 7;
        p.Order = TileOrder.Random;
        p.Output.Format = ImageFormat.Png;
        var path = store.Save(p);
        Assert.EndsWith(PresetStore.FileExtension, path);
        Assert.Contains("\"random\"", File.ReadAllText(path), StringComparison.OrdinalIgnoreCase);

        var loaded = store.Find("mine")!;
        Assert.Equal(7, loaded.Columns);
        Assert.Equal(TileOrder.Random, loaded.Order);
        Assert.Equal(ImageFormat.Png, loaded.Output.Format);
        Assert.Contains(store.LoadAll(), x => x.Name == "Standard");
        Assert.True(store.Delete("Mine"));
        Assert.Null(store.Find("Mine"));
    }

    [Fact]
    public void Normalize_clamps_and_reports_missing_output_folder()
    {
        var s = new SheetSettings { Columns = 0, Rows = 1000 };
        s.Output.Location = OutputLocation.OutputFolder;
        var errors = s.Normalize();
        Assert.Equal(1, s.Columns);
        Assert.Equal(100, s.Rows);
        Assert.Single(errors);
    }
}
