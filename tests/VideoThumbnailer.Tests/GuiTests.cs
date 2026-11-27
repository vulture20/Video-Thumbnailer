using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using VideoThumbnailer.Core;
using VideoThumbnailer.Core.Settings;
using VideoThumbnailer.Gui.Controls;
using VideoThumbnailer.Gui.Localization;
using VideoThumbnailer.Gui.ViewModels;
using VideoThumbnailer.Gui.Views;

[assembly: AvaloniaTestApplication(typeof(VideoThumbnailer.Tests.TestAppBuilder))]

namespace VideoThumbnailer.Tests;

public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<VideoThumbnailer.Gui.App>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}

public class GuiTests
{
    [AvaloniaFact]
    public void Main_window_loads_presets_and_queues_dropped_paths()
    {
        var home = TestUtil.TempDir();
        AppPaths.SetRootOverride(home);
        try
        {
            new AppSettings { FfmpegDownloadAsked = true, CheckFfmpegUpdates = false }.Save();
            var videos = Directory.CreateDirectory(Path.Combine(home, "videos")).FullName;
            File.WriteAllText(Path.Combine(videos, "a.mp4"), "");
            File.WriteAllText(Path.Combine(videos, "b.mkv"), "");
            File.WriteAllText(Path.Combine(videos, "notes.txt"), "");

            var window = new MainWindow();
            var vm = new MainViewModel(window);
            window.DataContext = vm;
            window.Show();

            Assert.Contains(vm.Presets, p => p.Name == "Standard");
            Assert.Equal(4, vm.Settings.Columns);

            // The live preview is rendered from the start (sample name until a video is queued).
            Assert.NotNull(vm.PreviewImage);
            Assert.Contains("4 × 5", vm.PreviewCaption);

            vm.AddPaths([videos]);
            Assert.Equal(2, vm.Jobs.Count);
            Assert.True(vm.StartCommand.CanExecute(null));
            Assert.Equal("a.mp4", vm.PreviewTitle);
            Assert.Contains("2", vm.QueueTitle);

            vm.SelectedPreset = vm.Presets.First(p => p.Name == "Compact");
            Assert.Equal(3, vm.Settings.Columns);
            Assert.Equal(3, vm.Settings.ToSettings("x").Rows);
            vm.RenderPreviewNow();
            Assert.Contains("3 × 3 = 9", vm.PreviewCaption);

            // Preset tiles select presets; output format buttons and quality slider reach the settings.
            var tiles = window.FindControl<ListBox>("PresetTiles")!;
            Assert.Same(vm.SelectedPreset, tiles.SelectedItem);
            window.FindControl<RadioButton>("FormatPng")!.IsChecked = true;
            Assert.Equal(ImageFormat.Png, vm.Settings.ToSettings("x").Output.Format);
            Assert.False(vm.Settings.HasQuality);
            window.FindControl<RadioButton>("FormatJpeg")!.IsChecked = true;
            Assert.Equal(ImageFormat.Jpeg, vm.Settings.Format);
            window.FindControl<Slider>("QualitySlider")!.Value = 75;
            Assert.Equal(75, vm.Settings.ToSettings("x").Output.Quality);
            window.FindControl<ToggleSwitch>("TimestampSwitch")!.IsChecked = false;
            Assert.False(vm.Settings.ToSettings("x").Timestamp.Enabled);
            window.FindControl<ToggleSwitch>("TimestampSwitch")!.IsChecked = true;

            // Animated sheet: the switch replaces the image formats by MP4/WebM/GIF.
            var jpeg = window.FindControl<RadioButton>("FormatJpeg")!;
            var gif = window.FindControl<RadioButton>("FormatGif")!;
            Assert.False(gif.IsEffectivelyVisible);
            window.FindControl<ToggleSwitch>("AnimatedSwitch")!.IsChecked = true;
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            Assert.True(gif.IsEffectivelyVisible);
            Assert.False(jpeg.IsEffectivelyVisible);
            gif.IsChecked = true;
            Assert.False(vm.Settings.HasQuality);
            var animated = vm.Settings.ToSettings("x");
            Assert.True(animated.Animation.Enabled);
            Assert.Equal(AnimationFormat.Gif, animated.Animation.Format);
            var anim = Environment.GetEnvironmentVariable("VTHUMB_SCREENSHOT_ANIM");
            if (!string.IsNullOrEmpty(anim))
            {
#pragma warning disable CS0618
                window.CaptureRenderedFrame()?.Save(anim);
#pragma warning restore CS0618
            }
            window.FindControl<RadioButton>("FormatMp4")!.IsChecked = true;
            window.FindControl<ToggleSwitch>("AnimatedSwitch")!.IsChecked = false;
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            Assert.True(jpeg.IsEffectivelyVisible);

            var list = window.FindControl<ListBox>("QueueList");
            Assert.NotNull(list);

            // Screenshot for documentation/CI artifacts.
            var shot = Environment.GetEnvironmentVariable("VTHUMB_SCREENSHOT");
            if (!string.IsNullOrEmpty(shot))
            {
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
#pragma warning disable CS0618 // simple PNG save is enough here
                window.CaptureRenderedFrame()?.Save(shot);
#pragma warning restore CS0618
            }

            // Language switch updates bound texts and the enum labels live.
            vm.Language = UiLanguage.German;
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            var addFiles = window.GetVisualDescendants().OfType<Button>().First(b => b.Command == vm.AddFilesCommand && !Equals(b.Content, "+")); // not the one in the folded strip
            Assert.Equal("+ Dateien…", addFiles.Content);
            Assert.Equal("Zeilenweise", SettingsViewModel.Orders[0].Label);
            Assert.Equal("Pause", vm.PauseButtonText);
            var de = Environment.GetEnvironmentVariable("VTHUMB_SCREENSHOT_DE");
            if (!string.IsNullOrEmpty(de))
            {
#pragma warning disable CS0618
                window.CaptureRenderedFrame()?.Save(de);
#pragma warning restore CS0618
            }
            vm.Language = UiLanguage.English;
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            Assert.Equal("+ Files…", addFiles.Content);
            Assert.Equal("English", AppSettings.Load().Language ?? "English"); // saved on close below

            // Changing a combo box value through the bound Choice list reaches the settings.
            vm.Settings.Order = TileOrder.ColumnMajor;
            Assert.Equal(TileOrder.ColumnMajor, vm.Settings.ToSettings("x").Order);

            // Simple mode is the default and hides advanced settings; the choice and the theme are saved.
            Assert.True(vm.IsSimple);
            var processes = window.FindControl<NumericUpDown>("ParallelProcesses")!;
            Assert.False(processes.IsEffectivelyVisible);
            window.FindControl<RadioButton>("AdvancedMode")!.IsChecked = true;
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            Assert.True(vm.IsAdvanced);
            Assert.True(processes.IsEffectivelyVisible);

            vm.Theme = UiTheme.Dark;
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            Assert.Equal(Avalonia.Styling.ThemeVariant.Dark, window.ActualThemeVariant);
            var dark = Environment.GetEnvironmentVariable("VTHUMB_SCREENSHOT_DARK");
            if (!string.IsNullOrEmpty(dark))
            {
#pragma warning disable CS0618
                window.CaptureRenderedFrame()?.Save(dark);
#pragma warning restore CS0618
            }
            vm.SaveAppSettings();
            var saved = AppSettings.Load();
            Assert.True(saved.AdvancedMode);
            Assert.Equal("Dark", saved.Theme);
            vm.Theme = UiTheme.System;
            vm.IsSimple = true;
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            Assert.False(processes.IsEffectivelyVisible);

            vm.ClearAllCommand.Execute(null);
            Assert.Empty(vm.Jobs);
            window.Close();
        }
        finally
        {
            AppPaths.SetRootOverride(null);
        }
    }
}

public class GuiPreviewTests
{
    /// <summary>
    /// Renders the README screenshots (docs/images) when VTHUMB_README_SHOTS points to a folder: light "Simple" mode,
    /// dark "Advanced" mode scrolled to the timestamp colors, a narrow window with the folded queue, the grid editor
    /// and the viewer, all with real frames. VTHUMB_README_VIDEO names the video to show (tools/readme-images.sh makes
    /// a colourful demo reel); without it a test clip is used. Without VTHUMB_README_SHOTS it only checks that the
    /// scenes come up.
    /// </summary>
    [AvaloniaFact]
    public async Task Readme_screenshots()
    {
        var t = PreviewSourceTests.Tools();
        var home = TestUtil.TempDir();
        AppPaths.SetRootOverride(home);
        try
        {
            new AppSettings { FfmpegDownloadAsked = true, CheckFfmpegUpdates = false, Language = "English" }.Save();
            var dir = Directory.CreateDirectory(Path.Combine(home, "Videos")).FullName;
            var demo = Environment.GetEnvironmentVariable("VTHUMB_README_VIDEO");
            string video;
            if (string.IsNullOrEmpty(demo)) video = PreviewSourceTests.MakeVideo(t, dir);
            else File.Copy(demo, video = Path.Combine(dir, "demo-reel.mp4"));
            File.Copy(video, Path.Combine(dir, "holiday-2026.mp4"));
            File.Copy(video, Path.Combine(dir, "interview.mkv"));
            var outDir = Environment.GetEnvironmentVariable("VTHUMB_README_SHOTS");
            void Shot(Window w, string name)
            {
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                if (string.IsNullOrEmpty(outDir)) return;
#pragma warning disable CS0618
                w.CaptureRenderedFrame()?.Save(Path.Combine(outDir, name));
#pragma warning restore CS0618
            }

            var window = new MainWindow();
            var vm = new MainViewModel(window);
            window.DataContext = vm;
            window.Show();
            vm.AddPaths([video, Path.Combine(dir, "holiday-2026.mp4"), Path.Combine(dir, "interview.mkv")]);
            await vm.UpdatePreviewAsync();
            Assert.Equal(Loc.T("PreviewNoteReal"), vm.PreviewNote);
            Shot(window, "gui.png");

            vm.Language = UiLanguage.German;
            await vm.UpdatePreviewAsync();
            Shot(window, "gui-de.png");
            vm.Language = UiLanguage.English;

            vm.IsAdvanced = true;
            vm.Theme = UiTheme.Dark;
            await vm.UpdatePreviewAsync();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            window.FindControl<CheckBox>("MillisecondsCheck")!.BringIntoView();
            Assert.True(window.FindControl<ColorField>("OutlineColorField")!.IsEffectivelyVisible);
            Shot(window, "gui-dark.png");

            vm.IsAdvanced = false;
            vm.Theme = UiTheme.Light;
            window.Width = 980;
            await vm.UpdatePreviewAsync();
            Assert.True(vm.IsQueueCollapsed);
            Shot(window, "gui-narrow.png");
            window.Close();

            // Grid editor with the "big image on top" grid
            var editor = new GridEditorViewModel(Core.Layout.BuiltInTemplates.Find("BigTop"), 4, 5) { Name = "My grid" };
            var editorWindow = new GridEditorWindow(editor);
            editorWindow.Show();
            Shot(editorWindow, "grid-editor.png");
            editorWindow.Close();

            // Viewer of a sheet with sidecar, mouse over a tile
            var s = BuiltInPresets.All.First(p => p.Name == "Standard").Clone();
            s.Output.Sidecar = true;
            s.Output.Location = OutputLocation.OutputFolder;
            s.Output.Folder = Directory.CreateDirectory(Path.Combine(home, "Sheets")).FullName;
            var job = new Core.Batch.ThumbnailJob(video);
            await new Core.Batch.BatchProcessor(t).RunAsync([job], s, new Core.Batch.BatchOptions(), TestContext.Current.CancellationToken);
            Assert.Equal(Core.Batch.JobStatus.Done, job.Status);
            var viewer = await ViewerViewModel.OpenAsync(job.OutputPath!, new AppSettings(), t);
            viewer.Hover(viewer.TileAt(0.4, 0.45));
            var viewerWindow = new ViewerWindow(viewer);
            viewerWindow.Show();
            Shot(viewerWindow, "viewer.png");
            viewerWindow.Close();
        }
        finally
        {
            AppPaths.SetRootOverride(null);
        }
    }

    [AvaloniaFact]
    public async Task Preview_shows_real_frames_and_video_data_of_the_selected_video()
    {
        var t = PreviewSourceTests.Tools();
        var home = TestUtil.TempDir();
        AppPaths.SetRootOverride(home);
        try
        {
            new AppSettings { FfmpegDownloadAsked = true, CheckFfmpegUpdates = false }.Save();
            var video = PreviewSourceTests.MakeVideo(t, Directory.CreateDirectory(Path.Combine(home, "v")).FullName);
            var window = new MainWindow();
            var vm = new MainViewModel(window);
            window.DataContext = vm;
            window.Show();
            Assert.True(vm.Ffmpeg.IsReady);

            vm.AddPaths([video]);
            await vm.UpdatePreviewAsync();
            Assert.Equal(Loc.T("PreviewNoteReal"), vm.PreviewNote);
            Assert.Equal("clip.mp4", vm.PreviewTitle);

            var shot = Environment.GetEnvironmentVariable("VTHUMB_SCREENSHOT_PREVIEW");
            if (!string.IsNullOrEmpty(shot))
            {
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
#pragma warning disable CS0618
                window.CaptureRenderedFrame()?.Save(shot);
#pragma warning restore CS0618
            }

            // Live preview off: image freed, nothing rendered or extracted, choice saved; on again renders.
            window.FindControl<ToggleSwitch>("LivePreviewSwitch")!.IsChecked = false;
            Assert.False(vm.LivePreview);
            Assert.Null(vm.PreviewImage);
            vm.Settings.Columns = 3;
            await vm.UpdatePreviewAsync();
            Assert.Null(vm.PreviewImage);
            Assert.True(window.FindControl<StackPanel>("PreviewOff")!.IsEffectivelyVisible);
            vm.SaveAppSettings();
            Assert.False(AppSettings.Load().LivePreview);
            vm.LivePreview = true;
            await vm.UpdatePreviewAsync();
            Assert.NotNull(vm.PreviewImage);

            // Fewer tiles: the frames for the new capture times are loaded, still all real.
            vm.Settings.Columns = 2;
            vm.Settings.Rows = 2;
            await vm.UpdatePreviewAsync();
            Assert.Equal(Loc.T("PreviewNoteReal"), vm.PreviewNote);
            Assert.Contains("2 × 2 = 4", vm.PreviewCaption);
            window.Close();
        }
        finally
        {
            AppPaths.SetRootOverride(null);
        }
    }
}

public class GuiQueueTests
{
    [AvaloniaFact]
    public void Queue_folds_in_narrow_windows_and_by_hand()
    {
        var home = TestUtil.TempDir();
        AppPaths.SetRootOverride(home);
        try
        {
            new AppSettings { FfmpegDownloadAsked = true, CheckFfmpegUpdates = false }.Save();
            var window = new MainWindow();
            var vm = new MainViewModel(window);
            window.DataContext = vm;
            window.Show();
            var strip = window.FindControl<Border>("QueueStrip")!;
            var panel = window.FindControl<Border>("QueuePanel")!;
            Assert.False(vm.IsQueueCollapsed);
            Assert.True(panel.IsEffectivelyVisible);

            // Narrow window: folds by itself; wide again: unfolds.
            window.Width = 900;
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            Assert.True(vm.IsQueueCollapsed);
            Assert.True(strip.IsEffectivelyVisible);
            Assert.False(panel.IsEffectivelyVisible);
            var shot = Environment.GetEnvironmentVariable("VTHUMB_SCREENSHOT_NARROW");
            if (!string.IsNullOrEmpty(shot))
            {
#pragma warning disable CS0618
                window.CaptureRenderedFrame()?.Save(shot);
#pragma warning restore CS0618
            }
            window.Width = 1320;
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            Assert.False(vm.IsQueueCollapsed);

            // By hand: stays folded when the window gets narrow and wide again.
            window.FindControl<Button>("CollapseQueueButton")!.Command!.Execute(null);
            Assert.True(vm.IsQueueCollapsed);
            vm.OnWindowWidthChanged(900);
            vm.OnWindowWidthChanged(1300);
            Assert.True(vm.IsQueueCollapsed);
            // Unfolded by hand while narrow: stays open until the next crossing.
            vm.OnWindowWidthChanged(900);
            vm.ToggleQueueCommand.Execute(null);
            vm.OnWindowWidthChanged(950);
            Assert.False(vm.IsQueueCollapsed);
            window.Close();
        }
        finally
        {
            AppPaths.SetRootOverride(null);
        }
    }
}

public class GuiFineOptionTests
{
    [AvaloniaFact]
    public void Advanced_mode_edits_outline_shadow_header_colors_and_milliseconds()
    {
        var home = TestUtil.TempDir();
        AppPaths.SetRootOverride(home);
        try
        {
            new AppSettings { FfmpegDownloadAsked = true, CheckFfmpegUpdates = false, AdvancedMode = true }.Save();
            var window = new MainWindow { Height = 2200 };
            var vm = new MainViewModel(window);
            window.DataContext = vm;
            window.Show();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            Assert.True(vm.IsAdvanced);

            var outline = window.FindControl<ColorField>("OutlineColorField")!;
            var shadow = window.FindControl<ColorField>("ShadowColorField")!;
            var title = window.FindControl<ColorField>("HeaderTitleColorField")!;
            var text = window.FindControl<ColorField>("HeaderColorField")!;
            Assert.True(outline.IsEffectivelyVisible);
            Assert.Equal("#FF000000", outline.Text);   // loaded from the preset
            Assert.True(outline.IsValid);

            outline.Box.Text = "#FF102030";
            shadow.Box.Text = "#80FF0000";
            title.Box.Text = "#FFFFCC00";
            text.Box.Text = "#FFCCCCCC";
            window.FindControl<CheckBox>("MillisecondsCheck")!.IsChecked = true;
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();

            var s = vm.Settings.ToSettings("x");
            Assert.Equal("#FF102030", s.Timestamp.OutlineColor);
            Assert.Equal("#80FF0000", s.Timestamp.ShadowColor);
            Assert.Equal("#FFFFCC00", s.Header.TitleColor);
            Assert.Equal("#FFCCCCCC", s.Header.Color);
            Assert.True(s.Timestamp.ShowMilliseconds);

            // Shadow color only editable with a shadow; nonsense is flagged but kept as typed.
            vm.Settings.TimestampShadow = false;
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            Assert.False(shadow.IsEffectivelyEnabled);
            text.Box.Text = "grün";
            Assert.False(text.IsValid);
            Assert.True(ColorField.TryParse("#abc", out _));

            var shot = Environment.GetEnvironmentVariable("VTHUMB_SCREENSHOT_FINE");
            if (!string.IsNullOrEmpty(shot))
            {
#pragma warning disable CS0618
                window.CaptureRenderedFrame()?.Save(shot);
#pragma warning restore CS0618
            }
            window.Close();
        }
        finally
        {
            AppPaths.SetRootOverride(null);
        }
    }
}

public class GuiPickerTests
{
    [AvaloniaFact]
    public void Color_picker_writes_hex_and_follows_the_text()
    {
        var window = new Window { Width = 600, Height = 600 };
        var field = new ColorField { Text = "#FF102030", Width = 200, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top };
        window.Content = field;
        window.Show();
        field.PickerButton.Flyout!.ShowAt(field.PickerButton);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.Equal(Avalonia.Media.Color.FromArgb(0xFF, 0x10, 0x20, 0x30), field.Picker.Color);

        field.Picker.Color = Avalonia.Media.Color.FromArgb(0x80, 0xFF, 0x00, 0x00);
        Assert.Equal("#80FF0000", field.Text);
        field.Box.Text = "#FF00FF00";
        Assert.Equal(Avalonia.Media.Color.FromArgb(0xFF, 0x00, 0xFF, 0x00), field.Picker.Color);

        var shot = Environment.GetEnvironmentVariable("VTHUMB_SCREENSHOT_COLOR");
        if (!string.IsNullOrEmpty(shot))
        {
#pragma warning disable CS0618
            window.CaptureRenderedFrame()?.Save(shot);
#pragma warning restore CS0618
        }
        window.Close();
    }

    [AvaloniaFact]
    public async Task Font_picker_lists_installed_fonts_filters_and_returns_choice()
    {
        var owner = new Window();
        owner.Show();
        var families = VideoThumbnailer.Gui.Views.FontPickerWindow.InstalledFamilies();
        var picker = new VideoThumbnailer.Gui.Views.FontPickerWindow(null);
        var result = picker.ShowDialog<string?>(owner);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        // Built-in font first and selected when nothing is set; all installed families follow.
        Assert.Null(((VideoThumbnailer.Gui.Views.FontPickerWindow.Entry)picker.List.SelectedItem!).Family);
        Assert.Equal(families.Count + 1, picker.Visible.Count);

        var shot = Environment.GetEnvironmentVariable("VTHUMB_SCREENSHOT_FONT");
        if (!string.IsNullOrEmpty(shot))
        {
#pragma warning disable CS0618
            picker.CaptureRenderedFrame()?.Save(shot);
#pragma warning restore CS0618
        }

        if (families.Count > 0)
        {
            var name = families[^1];
            picker.Search.Text = name;
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            Assert.All(picker.Visible, e => Assert.Contains(name, e.Label, StringComparison.OrdinalIgnoreCase));
            picker.List.SelectedItem = picker.Visible.First(e => e.Family == name);
            picker.Accept();
            Assert.Equal(name, await result);
        }
        else
        {
            picker.Accept();
            Assert.Equal("", await result);
        }
        owner.Close();
    }
}
