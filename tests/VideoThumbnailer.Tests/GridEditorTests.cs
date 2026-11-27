using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using VideoThumbnailer.Cli;
using VideoThumbnailer.Core;
using VideoThumbnailer.Core.Layout;
using VideoThumbnailer.Gui.ViewModels;
using VideoThumbnailer.Gui.Views;

namespace VideoThumbnailer.Tests;

/// <summary>Grid editor and own grids (#36).</summary>
public class GridEditorTests
{
    [Fact]
    public void Dragging_places_square_tiles()
    {
        var store = new TemplateStore(TestUtil.TempDir());
        var e = new GridEditorViewModel(null, 4, 3, store);
        Assert.Equal(12, e.Tiles.Count); // plain grid as start

        Assert.Equal((1, 0, 2), e.Square(1, 0, 2, 1));
        Assert.Equal((0, 0, 2), e.Square(1, 1, 0, 0));   // drag up-left grows towards the end cell
        Assert.Equal((2, 0, 2), e.Square(2, 0, 3, 2));   // only 2 columns of room to the right
        e.Place(1, 0, 2, 1);
        Assert.Equal(9, e.Tiles.Count);                  // 4 small ones replaced by one 2 × 2
        Assert.Equal(2, e.TileAt(2, 1)!.Size);
        Assert.True(e.RemoveAt(2, 1));
        Assert.Equal(4, e.EmptyCells);
        Assert.True(e.IsValid);
        e.Fill();
        Assert.Equal(0, e.EmptyCells);

        e.Columns = 2; // tiles outside are dropped
        Assert.All(e.Tiles, t => Assert.True(t.X + t.Size <= 2));
        e.Clear();
        Assert.False(e.IsValid);

        e.Columns = 3;
        e.Place(0, 0, 1, 1);
        e.Name = "Mein Raster";
        var t = e.ToTemplate();
        Assert.Equal(("Mein Raster", 3, 3, 1), (t.Name, t.Columns, t.Rows, t.Tiles.Count));
        Assert.Null(e.SaveOwn());
        Assert.True(e.IsExistingOwn && e.OwnGridsChanged);
        Assert.True(File.Exists(Path.Combine(store.Directory, "Mein Raster.vtgrid")));
        Assert.Equal(3, store.Find("mein raster")!.Rows);
        e.DeleteOwnCommand.Execute(null);
        Assert.False(e.IsExistingOwn);
        Assert.Empty(store.LoadAll());
    }

    // AvaloniaFact: runs on the UI thread like the GUI tests, which also change AppPaths.SetRootOverride.
    [AvaloniaFact]
    public void Own_grids_work_on_the_command_line()
    {
        var home = TestUtil.TempDir();
        AppPaths.SetRootOverride(home);
        try
        {
            var t = new GridTemplate { Name = "Logo Mitte", Columns = 3, Rows = 3, Tiles = [new(0, 0), new(1, 0), new(2, 0), new(0, 2), new(1, 2), new(2, 2)] };
            new TemplateStore().Save(t);
            var parsed = ArgParser.ParseLayout("Logo Mitte", "--layout")!;
            Assert.Equal(6, parsed.Tiles.Count);
            var file = new TemplateStore().PathFor("Logo Mitte");
            Assert.Equal(3, ArgParser.ParseLayout(file, "--layout")!.Rows);
            var ex = Assert.Throws<UsageException>(() => ArgParser.ParseLayout("nope", "--layout"));
            Assert.Contains("\"Logo Mitte\"", ex.Message);
        }
        finally { AppPaths.SetRootOverride(null); }
    }

    [AvaloniaFact]
    public void Editor_window_and_layout_list()
    {
        var home = TestUtil.TempDir();
        AppPaths.SetRootOverride(home);
        try
        {
            new Core.Settings.AppSettings { FfmpegDownloadAsked = true, CheckFfmpegUpdates = false, Language = "English" }.Save();
            var settings = new SettingsViewModel();
            var before = settings.Layouts.Count;

            var editor = new GridEditorViewModel(BuiltInTemplates.Find("BigTop"), 4, 5) { Name = "Own" };
            Assert.Equal(21, editor.Tiles.Count);
            var window = new GridEditorWindow(editor);
            window.Show();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            if (Environment.GetEnvironmentVariable("VTHUMB_FEATURE_SHOTS") is { Length: > 0 } shots)
            {
#pragma warning disable CS0618
                window.CaptureRenderedFrame()?.Save(Path.Combine(shots, "grid-editor.png"));
#pragma warning restore CS0618
            }
            editor.Place(0, 2, 1, 3);
            Assert.Null(editor.SaveOwn());
            window.Close();

            settings.ReloadOwnLayouts(editor.ToTemplate());
            Assert.Equal(before + 1, settings.Layouts.Count);
            Assert.True(settings.SelectedLayout!.IsOwn);
            Assert.Equal("Own", settings.SelectedLayout.Label);
            var s = settings.ToSettings("x");
            Assert.Equal(18, s.Template!.Tiles.Count);
            // loading a preset with that grid selects the own entry instead of adding another one
            settings.Load(s);
            Assert.Equal(before + 1, settings.Layouts.Count);
            Assert.True(settings.SelectedLayout!.IsOwn);
        }
        finally { AppPaths.SetRootOverride(null); }
    }
}
