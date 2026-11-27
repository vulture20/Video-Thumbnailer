using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using VideoThumbnailer.Core;
using VideoThumbnailer.Core.Settings;
using VideoThumbnailer.Gui.Input;
using VideoThumbnailer.Gui.Localization;
using VideoThumbnailer.Gui.ViewModels;
using VideoThumbnailer.Gui.Views;

namespace VideoThumbnailer.Tests;

/// <summary>Shortcuts (#34), preview clip and WebVTT in the GUI (#31, #32), handing files to a running window (#33), queue buttons.</summary>
public class GuiShortcutTests
{
    private sealed class TestUi(MainWindow window) : IUiServices
    {
        public int FilePicks;
        public string? InfoFile { get; set; }
        public Task<IReadOnlyList<string>> PickVideoFilesAsync() { FilePicks++; return Task.FromResult<IReadOnlyList<string>>([]); }
        public Task<string?> PickFolderAsync(string title) => Task.FromResult<string?>(null);
        public Task<string?> PickPresetFileAsync(bool save, string? suggestedName = null) => window.PickPresetFileAsync(save, suggestedName);
        public Task<string?> PickInfoExportFileAsync(string suggestedName) => Task.FromResult(InfoFile);
        public Task<string?> PickImageFileAsync(string? current, string titleKey) => Task.FromResult<string?>(null);
        public Task<string?> PromptTextAsync(string title, string message, string initial) => Task.FromResult<string?>(null);
        public Task<bool> ConfirmAsync(string title, string message, string? yes = null, string? no = null) => Task.FromResult(true);
        public Task ShowMessageAsync(string title, string message) => Task.CompletedTask;
        public Task<string?> PickFontAsync(string? current) => Task.FromResult<string?>(null);
        public Task<TileTimeResult> EditTileTimeAsync(TileTimeViewModel editor) => Task.FromResult(TileTimeResult.Cancel);
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

    private static void Press(MainWindow w, Key key, RawInputModifiers mods = RawInputModifiers.None)
    {
        w.KeyPressQwerty(PhysicalKeyFromKey(key), mods);
        w.KeyReleaseQwerty(PhysicalKeyFromKey(key), mods);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
    }

    private static PhysicalKey PhysicalKeyFromKey(Key k) => k switch
    {
        Key.E => PhysicalKey.E, Key.L => PhysicalKey.L, Key.B => PhysicalKey.B, Key.O => PhysicalKey.O,
        Key.Delete => PhysicalKey.Delete, Key.A => PhysicalKey.A, Key.Escape => PhysicalKey.Escape,
        _ => throw new ArgumentOutOfRangeException(nameof(k)),
    };

    [Fact]
    public void Shortcut_texts_follow_language_and_platform()
    {
        var mac = Shortcuts.IsMac;
        try
        {
            Shortcuts.IsMac = false;
            Assert.Equal("Strg+Umschalt+O", Shortcuts.Display("AddFolder", "de"));
            Assert.Equal("Ctrl+Enter / F5", Shortcuts.Display("Start", "en"));
            Assert.Equal("Entf", Shortcuts.Display("RemoveSelected", "de"));
            Assert.Equal(KeyModifiers.Control, Shortcuts.Gesture(Shortcuts.All["AddFiles"][0]).KeyModifiers);
            Shortcuts.IsMac = true;
            Assert.Equal("⇧⌘O", Shortcuts.Display("AddFolder", "de"));
            Assert.Equal(KeyModifiers.Meta, Shortcuts.Gesture(Shortcuts.All["AddFiles"][0]).KeyModifiers);
        }
        finally { Shortcuts.IsMac = mac; }
    }

    [AvaloniaFact]
    public void Keys_run_the_commands_and_tooltips_show_them()
    {
        var home = TestUtil.TempDir();
        AppPaths.SetRootOverride(home);
        try
        {
            new AppSettings { FfmpegDownloadAsked = true, CheckFfmpegUpdates = false, Language = "German" }.Save();
            var (window, vm, ui) = Open();
            var cmd = OperatingSystem.IsMacOS() ? RawInputModifiers.Meta : RawInputModifiers.Control;

            var advanced = vm.IsAdvanced;
            Press(window, Key.E, cmd);
            Assert.Equal(!advanced, vm.IsAdvanced);
            var live = vm.LivePreview;
            Press(window, Key.L, cmd);
            Assert.Equal(!live, vm.LivePreview);
            vm.LivePreview = live;
            var collapsed = vm.IsQueueCollapsed;
            Press(window, Key.B, cmd);
            Assert.Equal(!collapsed, vm.IsQueueCollapsed);
            vm.IsQueueCollapsed = false;
            Press(window, Key.O, cmd);
            Assert.Equal(1, ui.FilePicks);

            // Del and Ctrl+A in the queue.
            var dir = TestUtil.TempDir();
            foreach (var n in new[] { "a.mp4", "b.mp4", "c.mp4" }) File.WriteAllBytes(Path.Combine(dir, n), [0]);
            vm.AddPaths([dir]);
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            var list = window.FindControl<ListBox>("QueueList")!;
            window.UpdateLayout();
            Assert.True(list.ContainerFromIndex(0)?.Focus() == true || list.Focus());
            Press(window, Key.A, cmd);
            Assert.Equal(3, vm.SelectedJobs.Count);
            Press(window, Key.Delete);
            Assert.Empty(vm.Jobs);

            var start = window.FindControl<Button>("StartButton")!;
            var tip = ToolTip.GetTip(start) as string;
            Assert.Contains(OperatingSystem.IsMacOS() ? "⌘" : "Strg+Eingabe / F5", tip);
            Assert.Contains("(", ToolTip.GetTip(window.FindControl<Button>("AddUrlButton")!) as string);
            window.Close();
        }
        finally { AppPaths.SetRootOverride(null); }
    }

    [AvaloniaFact]
    public void Url_button_is_as_high_as_the_buttons_next_to_it()
    {
        var home = TestUtil.TempDir();
        AppPaths.SetRootOverride(home);
        try
        {
            new AppSettings { FfmpegDownloadAsked = true, CheckFfmpegUpdates = false, Language = "English" }.Save();
            var (window, _, _) = Open();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            var url = window.FindControl<Button>("AddUrlButton")!.Bounds;
            var files = window.FindControl<Button>("AddFilesButton")!.Bounds;
            var folder = window.FindControl<Button>("AddFolderButton")!.Bounds;
            Assert.True(files.Height > 20);
            Assert.Equal(files.Height, url.Height, 0.5);
            Assert.Equal(folder.Height, url.Height, 0.5);
            Assert.Equal(files.Y, url.Y, 0.5);
            window.Close();
        }
        finally { AppPaths.SetRootOverride(null); }
    }

    [AvaloniaFact]
    public async Task Preview_clip_webvtt_and_info_export_in_the_gui()
    {
        var home = TestUtil.TempDir();
        AppPaths.SetRootOverride(home);
        try
        {
            new AppSettings { FfmpegDownloadAsked = true, CheckFfmpegUpdates = false, Language = "English" }.Save();
            var (window, vm, ui) = Open();
            vm.IsAdvanced = true;
            var options = window.FindControl<Grid>("TrailerOptions")!;
            Assert.False(options.IsEffectivelyVisible);
            vm.Settings.TrailerEnabled = true;
            vm.Settings.TrailerFormat = AnimationFormat.Gif;
            vm.Settings.TrailerClips = 6;
            vm.Settings.TrailerOnly = true;
            vm.Settings.WebVtt = true;
            vm.Settings.WebVttTileWidth = 120;
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            Assert.True(options.IsEffectivelyVisible);
            Assert.False(vm.Settings.TrailerCanHaveAudio);
            if (Environment.GetEnvironmentVariable("VTHUMB_FEATURE_SHOTS") is { Length: > 0 } shots)
            {
                window.FindControl<CheckBox>("WebVttCheck")!.BringIntoView();
                options.BringIntoView();
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
#pragma warning disable CS0618
                window.CaptureRenderedFrame()?.Save(Path.Combine(shots, "trailer-advanced.png"));
#pragma warning restore CS0618
            }
            var s = vm.Settings.ToSettings("x");
            Assert.True(s.Trailer.Enabled && s.Trailer.Only);
            Assert.Equal((AnimationFormat.Gif, 6, true, 120), (s.Trailer.Format, s.Trailer.Clips, s.Output.WebVtt, s.Output.WebVttTileWidth));
            vm.Settings.Load(s);
            Assert.Equal(AnimationFormat.Gif, vm.Settings.TrailerFormat);

            // Export needs ffprobe; without one on PATH only the empty-queue message is checked.
            await vm.ExportInfoCommand.ExecuteAsync(null);
            Assert.Equal(Loc.T("StatusNothingToExport"), vm.StatusText);
            if (Core.Ffmpeg.FfmpegLocator.FromPath() is not null)
            {
                var dir = TestUtil.TempDir();
                var video = Path.Combine(dir, "clip.mp4");
                Assert.True(TestUtil.Run("ffmpeg", "-v", "error", "-y", "-f", "lavfi", "-i", "testsrc2=size=320x180:rate=25:duration=2", "-pix_fmt", "yuv420p", video));
                vm.AddPaths([video]);
                ui.InfoFile = Path.Combine(dir, "data.csv");
                await vm.ExportInfoCommand.ExecuteAsync(null);
                var lines = File.ReadAllLines(ui.InfoFile);
                Assert.Equal(2, lines.Length);
                Assert.StartsWith("clip.mp4,clip,mp4,", lines[1]);
            }
            window.Close();
        }
        finally { AppPaths.SetRootOverride(null); }
    }

    [AvaloniaFact]
    public void Preset_file_from_a_later_start_selects_the_preset()
    {
        var home = TestUtil.TempDir();
        AppPaths.SetRootOverride(home);
        try
        {
            new AppSettings { FfmpegDownloadAsked = true, CheckFfmpegUpdates = false, Language = "English" }.Save();
            var (window, vm, _) = Open();
            var file = Path.Combine(TestUtil.TempDir(), "Shared" + PresetStore.ShareExtension);
            PresetStore.ExportFile(new SheetSettings { Name = "Shared", Columns = 7 }, file);
            window.ReceiveArguments([file]);
            Assert.Equal("Shared", vm.SelectedPreset?.Name);
            Assert.Equal(7, vm.Settings.Columns);
            Assert.Equal(Loc.T("StatusPresetOpened", "Shared"), vm.StatusText);
            window.Close();
        }
        finally { AppPaths.SetRootOverride(null); }
    }
}

/// <summary>Handing arguments from a second start to the running program (#33).</summary>
public class SingleInstanceTests
{
    [Fact]
    public async Task Second_start_hands_its_files_to_the_first()
    {
        var name = "vt-test-" + Guid.NewGuid().ToString("N")[..8];
        Assert.False(SingleInstance.TrySend(name, ["x"]));
        var got = new TaskCompletionSource<string[]>();
        using var first = SingleInstance.Listen(name, a => got.TrySetResult(a));
        Assert.NotNull(first);
        // Another instance must not take the pipe away.
        Assert.Null(SingleInstance.Listen(name, _ => { }));

        var file = Path.Combine(TestUtil.TempDir(), "p.vtpreset");
        File.WriteAllText(file, "{}");
        var relative = Path.GetRelativePath(Environment.CurrentDirectory, file);
        // Right after the probe above the listener opens its pipe again; give it a moment.
        var sent = false;
        for (var i = 0; i < 20 && !sent; i++)
        {
            sent = SingleInstance.TrySend(name, [relative, "https://host/v.mp4"]);
            if (!sent) await Task.Delay(100, TestContext.Current.CancellationToken);
        }
        Assert.True(sent);
        var args = await got.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        Assert.Equal([Path.GetFullPath(file), "https://host/v.mp4"], args);
    }

    [Fact]
    public void Preset_files_are_recognized_by_both_extensions()
    {
        Assert.True(PresetStore.IsPresetFile("/a/My.vtpreset"));
        Assert.True(PresetStore.IsPresetFile("/a/My.VTPRESET.json"));
        Assert.False(PresetStore.IsPresetFile("/a/My.json"));
        Assert.Equal("My.vtpreset", PresetStore.SafeFileNameForExport("My"));
        var file = Path.Combine(TestUtil.TempDir(), "Unnamed.vtpreset");
        File.WriteAllText(file, """{"name":""}""");
        Assert.Equal("Unnamed", PresetStore.LoadFile(file).Name);
    }
}
