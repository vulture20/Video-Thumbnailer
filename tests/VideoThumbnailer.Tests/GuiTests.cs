using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using VideoThumbnailer.Core;
using VideoThumbnailer.Core.Settings;
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
            var addFiles = window.GetVisualDescendants().OfType<Button>().First(b => b.Command == vm.AddFilesCommand);
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
