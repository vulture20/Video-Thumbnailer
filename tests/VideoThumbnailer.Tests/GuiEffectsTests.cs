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

/// <summary>GUI side of #27 (tile effects), #28 (comment), #29 (watermark) and #30 (animation formats, sound).</summary>
public class GuiEffectsTests
{
    private sealed class TestUi(MainWindow window) : IUiServices
    {
        public string? ImageAnswer { get; set; }
        public Task<IReadOnlyList<string>> PickVideoFilesAsync() => window.PickVideoFilesAsync();
        public Task<string?> PickFolderAsync(string title) => window.PickFolderAsync(title);
        public Task<string?> PickPresetFileAsync(bool save, string? suggestedName = null) => window.PickPresetFileAsync(save, suggestedName);
        public Task<string?> PickInfoExportFileAsync(string suggestedName) => Task.FromResult<string?>(null);
        public Task<string?> PickImageFileAsync(string? current, string titleKey) => Task.FromResult(ImageAnswer);
        public Task<string?> PromptTextAsync(string title, string message, string initial) => Task.FromResult<string?>(null);
        public Task<bool> ConfirmAsync(string title, string message, string? yes = null, string? no = null) => Task.FromResult(true);
        public Task ShowMessageAsync(string title, string message) => Task.CompletedTask;
        public Task<string?> PickFontAsync(string? current) => Task.FromResult<string?>(null);
        public Task<TileTimeResult> EditTileTimeAsync(TileTimeViewModel editor) => Task.FromResult(TileTimeResult.Cancel);
        public Task<string?> PickSheetFileAsync() => Task.FromResult<string?>(null);
        public void ShowViewer(ViewerViewModel viewer) { }
        public Task<bool> EditGridAsync(GridEditorViewModel editor) => Task.FromResult(false);
        public Task<string?> PickPackFileAsync(string suggestedName) => Task.FromResult<string?>(null);
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
        var path = Path.Combine(dir, "logo.png");
        using var fs = File.Create(path);
        bmp.Encode(fs, SKEncodedImageFormat.Png, 100);
        return path;
    }

    [AvaloniaFact]
    public async Task Tile_effects_comment_watermark_and_animation_formats()
    {
        var home = TestUtil.TempDir();
        AppPaths.SetRootOverride(home);
        try
        {
            new AppSettings { FfmpegDownloadAsked = true, CheckFfmpegUpdates = false, Language = "German" }.Save();
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
            vm.IsAdvanced = true;
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();

            // #27: tile effects, only in advanced mode.
            vm.Settings.Gap = 12;
            vm.Settings.TileBorderWidth = 2;
            vm.Settings.TileBorderColor = "#FFE6E6E6";
            vm.Settings.TileShadow = true;
            vm.Settings.TileCorners = TileCorners.Rounded;
            Assert.True(vm.Settings.HasCornerSize);
            vm.Settings.TileGrayscale = true;
            vm.Settings.SheetBorderWidth = 3;
            var s = vm.Settings.ToSettings("x");
            Assert.Equal((2f, "#FFE6E6E6", true, TileCorners.Rounded, true, 3f),
                         (s.TileStyle.BorderWidth, s.TileStyle.BorderColor, s.TileStyle.Shadow, s.TileStyle.Corners, s.TileStyle.Grayscale, s.SheetBorderWidth));
            Assert.Equal(4, s.TileStyle.ShadowOffset); // not shown, kept from the preset

            // #28: comment in the header.
            vm.Settings.HeaderComment = "Urlaub 2026";
            Assert.Equal("Urlaub 2026", vm.Settings.ToSettings("x").Header.Comment);

            // #29: text watermark; a picture wins and locks the text.
            vm.Settings.WatermarkText = "© 2026";
            Assert.True(vm.Settings.HasWatermark);
            Assert.True(window.FindControl<TextBox>("WatermarkTextBox")!.IsEffectivelyEnabled);
            ui.ImageAnswer = MakeImage(home);
            await vm.PickWatermarkImageCommand.ExecuteAsync(null);
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            Assert.Equal(ui.ImageAnswer, vm.Settings.ToSettings("x").Watermark.ImagePath);
            Assert.False(window.FindControl<TextBox>("WatermarkTextBox")!.IsEffectivelyEnabled);
            vm.ClearWatermarkImageCommand.Execute(null);
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            Assert.True(window.FindControl<TextBox>("WatermarkTextBox")!.IsEffectivelyEnabled);
            vm.Settings.WatermarkPosition = WatermarkPosition.BottomRight;
            await vm.UpdatePreviewAsync();
            window.FindControl<CheckBox>("GrayscaleCheck")!.BringIntoView();
            Shot("effects-advanced-de.png");
            window.FindControl<CheckBox>("WatermarkPerTileCheck")!.BringIntoView();
            Shot("watermark-advanced-de.png");

            // Settings survive a round trip through the view model.
            vm.Settings.Load(vm.Settings.ToSettings("y"));
            Assert.Equal(("© 2026", "Urlaub 2026", TileCorners.Rounded), (vm.Settings.WatermarkText, vm.Settings.HeaderComment, vm.Settings.TileCorners));

            // #30: seven formats; sound only for the video formats, quality not for GIF/APNG.
            vm.Settings.Animated = true;
            vm.Settings.IsWebpAnimation = true;
            Assert.Equal(AnimationFormat.Webp, vm.Settings.AnimationFormat);
            Assert.False(vm.Settings.CanHaveAudio);
            Assert.True(vm.Settings.HasQuality);
            vm.Settings.IsApng = true;
            Assert.False(vm.Settings.HasQuality);
            vm.Settings.IsOgv = true;
            Assert.True(vm.Settings.CanHaveAudio);
            vm.Settings.IsH265 = true;
            vm.Settings.AnimationAudio = true;
            var a = vm.Settings.ToSettings("x").Animation;
            Assert.Equal((AnimationFormat.H265, true), (a.Format, a.Audio));
            window.FindControl<ToggleSwitch>("AudioSwitch")!.BringIntoView();
            Shot("animation-formats-de.png");
            window.Close();
        }
        finally
        {
            AppPaths.SetRootOverride(null);
        }
    }
}
