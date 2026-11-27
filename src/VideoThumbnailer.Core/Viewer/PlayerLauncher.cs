using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace VideoThumbnailer.Core.Viewer;

/// <summary>Players the viewer (#39) can start at a given time.</summary>
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<PlayerKind>))]
public enum PlayerKind { Auto, Mpv, Vlc, MpcHc, PotPlayer, Custom, System }

/// <summary>A player program and the arguments to start a video at a time.</summary>
public sealed record PlayerCommand(string FileName, IReadOnlyList<string> Arguments, bool SupportsTime, PlayerKind Kind);

/// <summary>
/// Starts a video in the preferred player at a time (#39). mpv, VLC, MPC-HC/MPC-BE and PotPlayer are found on PATH or
/// in their usual install folders; a custom command line can use <c>{file}</c>, <c>{seconds}</c>, <c>{ms}</c> and
/// <c>{time}</c> (hh:mm:ss.fff). The system default player cannot be told a time and opens the video at the start.
/// </summary>
public static class PlayerLauncher
{
    /// <summary>Order in which "automatic" looks for players.</summary>
    public static IReadOnlyList<PlayerKind> AutoOrder { get; } = [PlayerKind.Mpv, PlayerKind.Vlc, PlayerKind.MpcHc, PlayerKind.PotPlayer];

    /// <summary>The command for a choice, or null when that player is not installed (system default: always a command).</summary>
    public static PlayerCommand? Resolve(PlayerKind kind, string? customCommand, string video, double seconds)
    {
        switch (kind)
        {
            case PlayerKind.Auto:
                foreach (var k in AutoOrder)
                    if (Resolve(k, null, video, seconds) is { } found) return found;
                return SystemDefault(video);
            case PlayerKind.Custom:
                return string.IsNullOrWhiteSpace(customCommand) ? null : FromTemplate(customCommand!, video, seconds);
            case PlayerKind.System:
                return SystemDefault(video);
            default:
                return Find(kind) is { } exe ? Build(kind, exe, video, seconds) : null;
        }
    }

    /// <summary>Starts the player; returns the command used. Throws when it cannot be started.</summary>
    public static PlayerCommand Start(PlayerKind kind, string? customCommand, string video, double seconds)
    {
        var cmd = Resolve(kind, customCommand, video, seconds) ?? Resolve(PlayerKind.System, null, video, seconds)!;
        var psi = new ProcessStartInfo(cmd.FileName) { UseShellExecute = false };
        foreach (var a in cmd.Arguments) psi.ArgumentList.Add(a);
        using var _ = Process.Start(psi) ?? throw new InvalidOperationException(cmd.FileName);
        return cmd;
    }

    internal static PlayerCommand Build(PlayerKind kind, string exe, string video, double seconds)
    {
        var inv = CultureInfo.InvariantCulture;
        var s = Math.Max(0, seconds);
        IReadOnlyList<string> args = kind switch
        {
            PlayerKind.Mpv => ["--start=" + s.ToString("0.###", inv), "--", video],
            PlayerKind.Vlc => ["--start-time=" + s.ToString("0.###", inv), video],
            PlayerKind.MpcHc => [video, "/start", ((long)Math.Round(s * 1000)).ToString(inv)],
            PlayerKind.PotPlayer => [video, "/seek=" + Clock(s)],
            _ => [video],
        };
        return new PlayerCommand(exe, args, true, kind);
    }

    /// <summary>Custom command line: the first word is the program, quotes group words; placeholders in every argument.</summary>
    internal static PlayerCommand? FromTemplate(string template, string video, double seconds)
    {
        var parts = SplitCommandLine(template);
        if (parts.Count == 0) return null;
        var inv = CultureInfo.InvariantCulture;
        var s = Math.Max(0, seconds);
        string Expand(string a) => a.Replace("{file}", video)
            .Replace("{seconds}", s.ToString("0.###", inv))
            .Replace("{ms}", ((long)Math.Round(s * 1000)).ToString(inv))
            .Replace("{time}", Clock(s));
        var args = parts.Skip(1).Select(Expand).ToList();
        if (!parts.Skip(1).Any(p => p.Contains("{file}"))) args.Add(video);
        var usesTime = parts.Any(p => p.Contains("{seconds}") || p.Contains("{ms}") || p.Contains("{time}"));
        return new PlayerCommand(parts[0], args, usesTime, PlayerKind.Custom);
    }

    private static PlayerCommand SystemDefault(string video)
    {
        if (OperatingSystem.IsWindows()) return new PlayerCommand("explorer.exe", [video], false, PlayerKind.System);
        if (OperatingSystem.IsMacOS()) return new PlayerCommand("open", [video], false, PlayerKind.System);
        return new PlayerCommand("xdg-open", [video], false, PlayerKind.System);
    }

    /// <summary>hh:mm:ss.fff</summary>
    internal static string Clock(double seconds)
    {
        var t = TimeSpan.FromMilliseconds(Math.Round(Math.Max(0, seconds) * 1000));
        return $"{(int)t.TotalHours:00}:{t.Minutes:00}:{t.Seconds:00}.{t.Milliseconds:000}";
    }

    internal static List<string> SplitCommandLine(string text)
    {
        var list = new List<string>();
        var sb = new StringBuilder();
        var quoted = false;
        var any = false;
        foreach (var c in text.Trim())
        {
            if (c == '"') { quoted = !quoted; any = true; continue; }
            if (char.IsWhiteSpace(c) && !quoted)
            {
                if (any) { list.Add(sb.ToString()); sb.Clear(); any = false; }
                continue;
            }
            sb.Append(c);
            any = true;
        }
        if (any) list.Add(sb.ToString());
        return list;
    }

    /// <summary>Path of an installed player, or null.</summary>
    public static string? Find(PlayerKind kind)
    {
        var (names, folders) = Candidates(kind);
        foreach (var dir in folders)
            foreach (var n in names)
            {
                var p = Path.Combine(dir, n);
                if (File.Exists(p)) return p;
            }
        foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            foreach (var n in names)
            {
                try
                {
                    var p = Path.Combine(dir.Trim('"'), n);
                    if (File.Exists(p)) return p;
                }
                catch (ArgumentException) { /* bad PATH entry */ }
            }
        return null;
    }

    private static (string[] Names, string[] Folders) Candidates(PlayerKind kind)
    {
        if (OperatingSystem.IsWindows())
        {
            var pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            var pf86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string[] In(params string[] sub) => [.. sub.SelectMany(s => new[] { Path.Combine(pf, s), Path.Combine(pf86, s), Path.Combine(local, "Programs", s) })];
            return kind switch
            {
                PlayerKind.Mpv => (["mpv.exe"], In("mpv", "mpv.net")),
                PlayerKind.Vlc => (["vlc.exe"], In(Path.Combine("VideoLAN", "VLC"))),
                PlayerKind.MpcHc => (["mpc-hc64.exe", "mpc-hc.exe", "mpc-be64.exe", "mpc-be.exe"], In("MPC-HC", "MPC-BE", Path.Combine("K-Lite Codec Pack", "MPC-HC64"))),
                PlayerKind.PotPlayer => (["PotPlayerMini64.exe", "PotPlayerMini.exe"], In(Path.Combine("DAUM", "PotPlayer"), "PotPlayer")),
                _ => ([], []),
            };
        }
        if (OperatingSystem.IsMacOS())
        {
            return kind switch
            {
                PlayerKind.Mpv => (["mpv"], ["/Applications/mpv.app/Contents/MacOS", "/opt/homebrew/bin", "/usr/local/bin"]),
                PlayerKind.Vlc => (["VLC"], ["/Applications/VLC.app/Contents/MacOS"]),
                _ => ([], []),
            };
        }
        return kind switch
        {
            PlayerKind.Mpv => (["mpv"], ["/usr/bin", "/usr/local/bin", "/snap/bin"]),
            PlayerKind.Vlc => (["vlc"], ["/usr/bin", "/usr/local/bin", "/snap/bin"]),
            _ => ([], []),
        };
    }
}
