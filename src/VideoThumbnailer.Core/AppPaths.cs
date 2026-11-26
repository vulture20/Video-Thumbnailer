namespace VideoThumbnailer.Core;

/// <summary>
/// Per-user storage locations. Can be overridden with the VIDEO_THUMBNAILER_HOME
/// environment variable or by placing a file named "portable.txt" next to the executable
/// (then everything is stored in a "data" folder next to the executable).
/// </summary>
public static class AppPaths
{
    public const string AppFolderName = "VideoThumbnailer";
    public const string UnixFolderName = "video-thumbnailer";

    private static string? _overrideRoot;

    /// <summary>Overrides all locations (used by tests).</summary>
    public static void SetRootOverride(string? root) => _overrideRoot = root;

    private static string? PortableOrOverrideRoot()
    {
        if (_overrideRoot is not null) return _overrideRoot;
        var env = Environment.GetEnvironmentVariable("VIDEO_THUMBNAILER_HOME");
        if (!string.IsNullOrWhiteSpace(env)) return env;
        var baseDir = AppContext.BaseDirectory;
        if (File.Exists(Path.Combine(baseDir, "portable.txt"))) return Path.Combine(baseDir, "data");
        return null;
    }

    /// <summary>Settings and presets.</summary>
    public static string ConfigDir
    {
        get
        {
            var root = PortableOrOverrideRoot();
            if (root is not null) return Path.Combine(root, "config");
            if (OperatingSystem.IsWindows())
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppFolderName);
            if (OperatingSystem.IsMacOS())
                return Path.Combine(Home, "Library", "Application Support", AppFolderName);
            var xdg = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
            return Path.Combine(string.IsNullOrWhiteSpace(xdg) ? Path.Combine(Home, ".config") : xdg, UnixFolderName);
        }
    }

    /// <summary>Larger data such as the managed ffmpeg installation.</summary>
    public static string DataDir
    {
        get
        {
            var root = PortableOrOverrideRoot();
            if (root is not null) return Path.Combine(root, "data");
            if (OperatingSystem.IsWindows())
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppFolderName);
            if (OperatingSystem.IsMacOS())
                return Path.Combine(Home, "Library", "Application Support", AppFolderName);
            var xdg = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
            return Path.Combine(string.IsNullOrWhiteSpace(xdg) ? Path.Combine(Home, ".local", "share") : xdg, UnixFolderName);
        }
    }

    public static string PresetsDir => Path.Combine(ConfigDir, "presets");
    public static string SettingsFile => Path.Combine(ConfigDir, "settings.json");
    public static string FfmpegDir => Path.Combine(DataDir, "ffmpeg");

    private static string Home => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
}
