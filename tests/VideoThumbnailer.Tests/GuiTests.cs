using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using VideoThumbnailer.Core;
using VideoThumbnailer.Core.Settings;
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

            vm.AddPaths([videos]);
            Assert.Equal(2, vm.Jobs.Count);
            Assert.True(vm.StartCommand.CanExecute(null));

            vm.SelectedPreset = vm.Presets.First(p => p.Name == "Compact");
            Assert.Equal(3, vm.Settings.Columns);
            Assert.Equal(3, vm.Settings.ToSettings("x").Rows);

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
