using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using SkiaSharp;
using VideoThumbnailer.Core;
using VideoThumbnailer.Core.Layout;
using VideoThumbnailer.Core.Settings;
using VideoThumbnailer.Gui.Localization;
using VideoThumbnailer.Gui.ViewModels;
using VideoThumbnailer.Gui.Views;

namespace VideoThumbnailer.Tests;

/// <summary>GUI side of #24 (special grids), #25 (fit to screen) and #26 (background picture).</summary>
public class GuiLayoutFeatureTests
{
    private sealed class TestUi(MainWindow window) : IUiServices
    {
        public string? ImageAnswer { get; set; }
        public Task<IReadOnlyList<string>> PickVideoFilesAsync() => window.PickVideoFilesAsync();
        public Task<string?> PickFolderAsync(string title) => window.PickFolderAsync(title);
        public Task<string?> PickPresetFileAsync(bool save, string? suggestedName = null) => window.PickPresetFileAsync(save, suggestedName);
        public Task<string?> PickImageFileAsync(string? current) => Task.FromResult(ImageAnswer);
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

    private static string MakeImage(string dir)
    {
        using var bmp = new SKBitmap(64, 36);
        using (var c = new SKCanvas(bmp))
        {
            using var p = new SKPaint { Shader = SKShader.CreateLinearGradient(new SKPoint(0, 0), new SKPoint(64, 36), [new SKColor(0xFF2B6CB0), new SKColor(0xFFE0A030)], SKShaderTileMode.Clamp) };
            c.DrawRect(0, 0, 64, 36, p);
        }
        var path = Path.Combine(dir, "bg.png");
        using var fs = File.Create(path);
        bmp.Encode(fs, SKEncodedImageFormat.Png, 100);
        return path;
    }

    [AvaloniaFact]
    public async Task Layout_picker_screen_size_and_background_picture()
    {
        var home = TestUtil.TempDir();
        AppPaths.SetRootOverride(home);
        var before = ScreenFitting.Current;
        try
        {
            new AppSettings { FfmpegDownloadAsked = true, CheckFfmpegUpdates = false, Language = "English" }.Save();
            var (window, vm, ui) = Open();
            var shots = Environment.GetEnvironmentVariable("VTHUMB_FEATURE_SHOTS");
            void Shot(string name)
            {
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                if (string.IsNullOrEmpty(shots)) return;
#pragma warning disable CS0618
                window.CaptureRenderedFrame()?.Save(Path.Combine(shots, name));
#pragma warning restore CS0618
            }

            // #24: plain grid plus the built-in special grids; columns/rows only apply to the plain grid.
            Assert.Equal(1 + BuiltInTemplates.All.Count, vm.Settings.Layouts.Count);
            Assert.True(vm.Settings.IsPlainGrid);
            var columns = window.FindControl<NumericUpDown>("ColumnsBox")!;
            Assert.True(columns.IsEffectivelyEnabled);
            vm.Settings.SelectedLayout = vm.Settings.Layouts.First(l => l.Template?.Name == "BigTop");
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            Assert.False(columns.IsEffectivelyEnabled);
            var s = vm.Settings.ToSettings("x");
            Assert.Equal("BigTop", s.Template!.Name);
            await vm.UpdatePreviewAsync();
            Assert.StartsWith(Loc.T("Layout.BigTop") + " = 21", vm.PreviewCaption);
            Shot("layout-bigtop.png");

            // A preset with its own special grid gets an entry of its own; the plain grid removes it again.
            var custom = new SheetSettings { Name = "Mine", Template = new GridTemplate { Name = "Strip", Columns = 3, Rows = 2, Tiles = [new(0, 0, 2), new(2, 0), new(2, 1)] } };
            vm.Settings.Load(custom);
            Assert.Equal("Strip", vm.Settings.SelectedLayout!.Label);
            Assert.False(vm.Settings.SelectedLayout.IsBuiltIn);
            vm.Settings.Load(BuiltInPresets.Default);
            Assert.True(vm.Settings.IsPlainGrid);
            Assert.DoesNotContain(vm.Settings.Layouts, l => !l.IsBuiltIn);

            // #25: the size follows the screen the window is on.
            vm.OnScreenChanged(new ScreenSize(1600, 900));
            vm.Settings.FitToScreen = ScreenFit.WidthAndHeight;
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            Assert.False(vm.Settings.IsFixedWidth);
            await vm.UpdatePreviewAsync();
            var caption = vm.PreviewCaption;
            var size = caption[(caption.LastIndexOf("sheet", StringComparison.Ordinal) + 6)..].Replace(" px", "").Split(" × ").Select(int.Parse).ToArray();
            Assert.True(size[0] <= 1600 && size[1] <= 900, caption);
            Assert.Equal(ScreenFit.WidthAndHeight, vm.Settings.ToSettings("x").FitToScreen);
            vm.Settings.FitToScreen = ScreenFit.Width;
            await vm.UpdatePreviewAsync();
            Assert.Contains("1600 × ", vm.PreviewCaption.Replace("1599", "1600").Replace("1598", "1600").Replace("1597", "1600"));

            // #26: background picture (advanced), chosen with the picker, cleared with ✕.
            vm.IsAdvanced = true;
            vm.Settings.FitToScreen = ScreenFit.Off;
            vm.Settings.Margin = 40;
            vm.Settings.SelectedLayout = vm.Settings.Layouts.First(l => l.Template?.Name == "BigCenter");
            ui.ImageAnswer = MakeImage(home);
            await vm.PickBackgroundImageCommand.ExecuteAsync(null);
            Assert.Equal(ui.ImageAnswer, vm.Settings.BackgroundImagePath);
            Assert.True(vm.Settings.HasBackgroundImage);
            vm.Settings.BackgroundImageOpacity = 0.8m;
            var bg = vm.Settings.ToSettings("x").BackgroundImage;
            Assert.Equal((ui.ImageAnswer, BackgroundImageMode.Fill, 0.8), (bg.Path, bg.Mode, bg.Opacity));
            await vm.UpdatePreviewAsync();
            window.FindControl<TextBox>("BackgroundImageBox")!.BringIntoView();
            Shot("background-advanced.png");
            vm.ClearBackgroundImageCommand.Execute(null);
            Assert.False(vm.Settings.HasBackgroundImage);
            Assert.Null(vm.Settings.ToSettings("x").BackgroundImage.Path);

            vm.Language = UiLanguage.German;
            vm.Settings.BackgroundImagePath = ui.ImageAnswer;
            await vm.UpdatePreviewAsync();
            Shot("background-advanced-de.png");
            window.Close();
        }
        finally
        {
            ScreenFitting.Current = before;
            AppPaths.SetRootOverride(null);
        }
    }
}
