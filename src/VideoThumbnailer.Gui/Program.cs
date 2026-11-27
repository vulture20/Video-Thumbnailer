using Avalonia;
using VideoThumbnailer.Gui.Input;

namespace VideoThumbnailer.Gui;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // A double-click on a preset or video while the program is open: hand it over instead of opening a second window (#33).
        if (args.Length > 0 && SingleInstance.TrySend(SingleInstance.DefaultPipeName(), args)) return;
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
}
