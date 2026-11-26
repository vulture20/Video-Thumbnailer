using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using VideoThumbnailer.Gui.ViewModels;
using VideoThumbnailer.Gui.Views;

namespace VideoThumbnailer.Gui;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var window = new MainWindow();
            var vm = new MainViewModel(window);
            window.DataContext = vm;
            desktop.MainWindow = window;
            // Files/folders passed on the command line (e.g. "Open with") are queued; a preset file selects the preset.
            vm.HandleStartupArguments(desktop.Args ?? []);
        }
        base.OnFrameworkInitializationCompleted();
    }
}
