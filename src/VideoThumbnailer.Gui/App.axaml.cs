using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using VideoThumbnailer.Gui.Input;
using VideoThumbnailer.Gui.ViewModels;
using VideoThumbnailer.Gui.Views;

namespace VideoThumbnailer.Gui;

public partial class App : Application
{
    private SingleInstance? _instance;

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

            // Later starts with files hand them over to this window (#33).
            _instance = SingleInstance.Listen(SingleInstance.DefaultPipeName(),
                args => Dispatcher.UIThread.Post(() => window.ReceiveArguments(args)));
            desktop.Exit += (_, _) => _instance?.Dispose();

            // macOS delivers double-clicked files as an "open file" event, not as arguments.
            if (TryGetFeature(typeof(IActivatableLifetime)) is IActivatableLifetime activatable)
            {
                activatable.Activated += (_, e) =>
                {
                    if (e is not FileActivatedEventArgs files) return;
                    var paths = files.Files.Select(f => f.TryGetLocalPath()).OfType<string>().ToArray();
                    if (paths.Length > 0) Dispatcher.UIThread.Post(() => window.ReceiveArguments(paths));
                };
            }
        }
        base.OnFrameworkInitializationCompleted();
    }
}
