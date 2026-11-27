using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using VideoThumbnailer.Core;
using VideoThumbnailer.Core.Layout;
using VideoThumbnailer.Core.Settings;
using VideoThumbnailer.Gui.Localization;
using VideoThumbnailer.Gui.ViewModels;
using VideoThumbnailer.Gui.Views;

namespace VideoThumbnailer.Tests;

/// <summary>GUI side of #21 (URLs), #22 (black frames switch) and #23 (capture times by hand).</summary>
public class GuiCaptureTimeTests
{
    /// <summary>The real window for everything, except that the tile time dialog is answered by the test.</summary>
    private sealed class TestUi(MainWindow window) : IUiServices
    {
        public Func<TileTimeViewModel, TileTimeResult> EditTile { get; set; } = _ => TileTimeResult.Cancel;
        public string? PromptAnswer { get; set; }
        public Task<IReadOnlyList<string>> PickVideoFilesAsync() => window.PickVideoFilesAsync();
        public Task<string?> PickFolderAsync(string title) => window.PickFolderAsync(title);
        public Task<string?> PickPresetFileAsync(bool save, string? suggestedName = null) => window.PickPresetFileAsync(save, suggestedName);
        public Task<string?> PickInfoExportFileAsync(string suggestedName) => Task.FromResult<string?>(null);
        public Task<string?> PickImageFileAsync(string? current, string titleKey) => Task.FromResult<string?>(null);
        public Task<string?> PromptTextAsync(string title, string message, string initial) => Task.FromResult(PromptAnswer);
        public Task<bool> ConfirmAsync(string title, string message, string? yes = null, string? no = null) => Task.FromResult(true);
        public Task ShowMessageAsync(string title, string message) => Task.CompletedTask;
        public Task<string?> PickFontAsync(string? current) => Task.FromResult<string?>(null);
        public Task<TileTimeResult> EditTileTimeAsync(TileTimeViewModel editor) => Task.FromResult(EditTile(editor));
        public void OpenPath(string path) { }
    }

    private static (MainWindow, MainViewModel, TestUi) Open()
    {
        var window = new MainWindow();
        var ui = new TestUi(window);
        var vm = new MainViewModel(ui);
        window.DataContext = vm;
        window.Show();
        return (window, vm, ui);
    }

    [AvaloniaFact]
    public async Task Urls_are_queued_by_dialog_and_shown_without_credentials()
    {
        var home = TestUtil.TempDir();
        AppPaths.SetRootOverride(home);
        try
        {
            new AppSettings { FfmpegDownloadAsked = true, CheckFfmpegUpdates = false, Language = "English" }.Save();
            var (window, vm, ui) = Open();
            Assert.True(window.FindControl<Button>("AddUrlButton")!.IsEffectivelyVisible);

            ui.PromptAnswer = "https://u:p@cdn.example.org/a/Film%201.mkv?sig=x  not-a-url\nhttp://host/b.mp4";
            await vm.AddUrlCommand.ExecuteAsync(null);
            Assert.Equal(["Film 1.mkv", "b.mp4"], vm.Jobs.Select(j => j.FileName));
            Assert.Equal("https://cdn.example.org/a/", vm.Jobs[0].Folder);
            Assert.Equal("https://cdn.example.org/a/Film 1.mkv", vm.Jobs[0].ShownPath);
            Assert.True(vm.Jobs[0].IsRemote);
            Assert.Equal(Loc.T("StatusInvalidUrl", "not-a-url"), vm.StatusText);

            // The same address again is not queued twice.
            vm.AddUrls("http://host/b.mp4");
            Assert.Equal(2, vm.Jobs.Count);
            window.Close();
        }
        finally { AppPaths.SetRootOverride(null); }
    }

    [AvaloniaFact]
    public void Black_frame_switch_reaches_the_settings()
    {
        var home = TestUtil.TempDir();
        AppPaths.SetRootOverride(home);
        try
        {
            new AppSettings { FfmpegDownloadAsked = true, CheckFfmpegUpdates = false }.Save();
            var (window, vm, _) = Open();
            vm.IsAdvanced = true;
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            var check = window.FindControl<CheckBox>("SkipBlackCheck")!;
            Assert.True(check.IsChecked);
            check.IsChecked = false;
            Assert.False(vm.Settings.ToSettings("x").SkipBlackFrames);
            vm.Settings.BlackFrameSearchSeconds = 4;
            Assert.Equal(4, vm.Settings.ToSettings("x").BlackFrameSearchSeconds);
            window.Close();
        }
        finally { AppPaths.SetRootOverride(null); }
    }

    [Fact]
    public void Tile_time_editor_parses_steps_and_reports_changes()
    {
        var editor = new TileTimeViewModel(2, 120, 25, 30, new ManualTime(30), animated: true, defaultClipSeconds: 3);
        var seen = new List<ManualTime>();
        editor.Changed += seen.Add;
        Assert.Contains("3", editor.Title);

        editor.TimeText = "1:05.5";
        Assert.Equal(65.5, editor.Time);
        editor.StepCommand.Execute("10");
        Assert.Equal("00:01:15.500", editor.TimeText);
        editor.StepCommand.Execute("frame");
        Assert.Equal(75.54, editor.Time, 3);
        editor.StepCommand.Execute("-1000");
        Assert.Equal(0, editor.Time);
        editor.TimeText = "3:00";
        Assert.Equal(120, editor.Time); // clamped to the video

        editor.TimeText = "abc";
        Assert.True(editor.TimeInvalid);
        Assert.Null(editor.Current);
        editor.TimeText = "0:20";
        editor.ClipText = "x";
        Assert.False(editor.IsValid);
        editor.ClipText = "2,5";
        Assert.Equal(new ManualTime(20, 2.5), editor.Current);
        Assert.Equal(new ManualTime(20, 2.5), seen[^1]);
        Assert.Contains(new ManualTime(65.5), seen);
    }

    [AvaloniaFact]
    public void Tile_time_window_binds_to_the_editor()
    {
        var editor = new TileTimeViewModel(0, 60, 25, 10, new ManualTime(10), animated: false, defaultClipSeconds: 3);
        var w = new TileTimeWindow(editor);
        w.Show();
        T? Find<T>(string name) where T : Control => w.GetVisualDescendants().OfType<T>().FirstOrDefault(c => c.Name == name);
        var text = Find<TextBox>("TileTimeText")!;
        Assert.Equal("00:00:10", text.Text);
        Find<Slider>("TileTimeSlider")!.Value = 42;
        Assert.Equal("00:00:42", text.Text);
        text.Text = "0:50";
        Assert.Equal(50, editor.Time);
        Assert.Null(Find<TextBox>("TileClipText"));
        text.Text = "zz";
        Assert.False(Find<Button>("TileTimeOk")!.IsEnabled);
        w.Close();
    }

    [AvaloniaFact]
    public async Task Clicked_tile_gets_its_time_by_hand_until_reset()
    {
        var t = PreviewSourceTests.Tools();
        var home = TestUtil.TempDir();
        AppPaths.SetRootOverride(home);
        try
        {
            new AppSettings { FfmpegDownloadAsked = true, CheckFfmpegUpdates = false, Language = "English" }.Save();
            var video = PreviewSourceTests.MakeVideo(t, Directory.CreateDirectory(Path.Combine(home, "v")).FullName);
            var (window, vm, ui) = Open();
            vm.Settings.Load(BuiltInPresets.All.First(p => p.Name == "Compact")); // 3 × 3
            vm.AddPaths([video]);
            await vm.UpdatePreviewAsync();
            Assert.True(vm.CanEditTiles);
            Assert.False(vm.HasPreviewManualTimes);

            // The middle of the sheet is tile 4 (row-major, timeline index 4).
            var index = vm.TileIndexAt(0.5, 0.6);
            Assert.Equal(4, index);
            Assert.Equal(-1, vm.TileIndexAt(0.001, 0.001));

            // Changes while the dialog is open reach the job at once; Cancel restores.
            ui.EditTile = e =>
            {
                Assert.Equal(4, e.Index);
                e.TimeText = "0:01.5";
                Assert.Equal(new ManualTime(1.5), vm.Jobs[0].Job.ManualTimes![4]);
                return TileTimeResult.Cancel;
            };
            await vm.EditTileAtAsync(0.5, 0.6);
            Assert.Null(vm.Jobs[0].Job.ManualTimes);

            ui.EditTile = e => { e.TimeText = "0:01.5"; return TileTimeResult.Ok; };
            await vm.EditTileAsync(4);
            ui.EditTile = e => { e.TimeText = "4"; return TileTimeResult.Ok; };
            await vm.EditTileAsync(0);
            Assert.Equal(2, vm.Jobs[0].ManualTimeCount);
            Assert.True(vm.HasPreviewManualTimes);
            Assert.Contains("2", vm.PreviewManualTimesText);
            Assert.True(window.FindControl<Button>("ResetManualTimesButton")!.IsVisible);
            await vm.UpdatePreviewAsync();
            Assert.Equal(Loc.T("PreviewNoteReal"), vm.PreviewNote);

            // "Automatic" clears one tile, the reset button all of them.
            ui.EditTile = _ => TileTimeResult.Automatic;
            await vm.EditTileAsync(0);
            Assert.True(vm.Jobs[0].Job.ManualTimes!.SequenceEqual([null, null, null, null, new ManualTime(1.5)]));
            vm.ResetManualTimesCommand.Execute(null);
            Assert.Null(vm.Jobs[0].Job.ManualTimes);
            Assert.False(vm.HasPreviewManualTimes);
            window.Close();
        }
        finally { AppPaths.SetRootOverride(null); }
    }
}
