using System.Text.Json;
using VideoThumbnailer.Core;
using VideoThumbnailer.Core.Batch;
using VideoThumbnailer.Core.Ffmpeg;
using VideoThumbnailer.Core.Input;
using VideoThumbnailer.Core.Output;
using VideoThumbnailer.Core.Settings;
using VideoThumbnailer.Core.Util;

namespace VideoThumbnailer.Cli;

public static class CliApp
{
    public const int ExitOk = 0, ExitFailures = 1, ExitUsage = 2, ExitNoFfmpeg = 3, ExitCancelled = 130;

    public static async Task<int> RunAsync(string[] args)
    {
        try
        {
            if (args.Length == 0 || args[0] is "-h" or "--help" or "help")
            {
                Console.WriteLine(HelpText.Main);
                return args.Length == 0 ? ExitUsage : ExitOk;
            }
            if (args[0] is "--version" or "version")
            {
                Console.WriteLine("vthumb " + typeof(CliApp).Assembly.GetName().Version?.ToString(3));
                return ExitOk;
            }
            return args[0] switch
            {
                "ffmpeg" => await FfmpegCommandAsync(args.Skip(1).ToList()),
                "presets" or "preset" => PresetsCommand(args.Skip(1).ToList()),
                _ => await GenerateAsync(ArgParser.ParseGenerate(args)),
            };
        }
        catch (UsageException ex)
        {
            Console.Error.WriteLine("Error: " + ex.Message);
            Console.Error.WriteLine("Run 'vthumb --help' for usage.");
            return ExitUsage;
        }
    }

    // ------------------------------------------------------------------ generate

    private static async Task<int> GenerateAsync(GenerateOptions o)
    {
        var app = AppSettings.Load();
        var store = new PresetStore();

        SheetSettings settings;
        if (o.Preset is not null)
            settings = store.Find(o.Preset)?.Clone() ?? throw new UsageException($"Preset not found: {o.Preset}");
        else
            settings = app.LastSettings?.Clone() ?? BuiltInPresets.Default; // spec: without preset use last GUI settings
        foreach (var apply in o.Overrides) apply(settings);
        var problems = settings.Normalize();
        if (problems.Count > 0) throw new UsageException(string.Join(" ", problems));

        if (o.SavePresetAs is not null)
        {
            settings.Name = o.SavePresetAs;
            var path = store.Save(settings);
            Console.WriteLine($"Preset saved: {path}");
            if (o.Inputs.Count == 0) return ExitOk;
        }

        if (o.Inputs.Count == 0) throw new UsageException("No input files or folders given.");
        var missing = o.Inputs.Where(p => !File.Exists(p) && !Directory.Exists(p)).ToList();
        foreach (var m in missing) Console.Error.WriteLine($"Warning: not found: {m}");

        var inputs = VideoScanner.Scan(o.Inputs, o.Recursive ?? app.Recursive);
        if (inputs.Count == 0)
        {
            Console.Error.WriteLine("No video files found.");
            return ExitFailures;
        }

        if (o.DryRun)
        {
            foreach (var v in inputs)
                Console.WriteLine($"{v.Path}\n  -> {OutputPathResolver.ResolveSheetPath(settings, v.Path, v.Root)}");
            Console.WriteLine($"{inputs.Count} video(s), preset '{settings.Name}'.");
            return ExitOk;
        }

        var tools = await EnsureFfmpegAsync(o.FfmpegDir ?? app.FfmpegDirectory, o.Yes, app);
        if (tools is null) return ExitNoFfmpeg;

        var jobs = inputs.Select(v => new ThumbnailJob(v.Path, v.Root)).ToList();
        var options = new BatchOptions();
        if ((o.Jobs ?? app.MaxParallelProcesses) is { } j) options.MaxParallelProcesses = Math.Max(1, j);
        options.MaxParallelVideos = Math.Max(1, o.Videos ?? app.MaxParallelVideos ?? Math.Max(1, options.MaxParallelProcesses / 2));

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            if (cts.IsCancellationRequested) return; // second Ctrl+C: hard exit
            e.Cancel = true;
            Console.Error.WriteLine("\nCancelling… (press Ctrl+C again to abort immediately)");
            cts.Cancel();
        };

        var finished = 0;
        var consoleLock = new object();
        foreach (var job in jobs)
        {
            job.Changed += jb =>
            {
                if (!jb.IsFinished || o.Quiet && jb.Status == JobStatus.Done) return;
                lock (consoleLock)
                {
                    var n = Interlocked.Increment(ref finished);
                    var tag = jb.Status switch
                    {
                        JobStatus.Done => "OK  ",
                        JobStatus.Skipped => "SKIP",
                        JobStatus.Cancelled => "STOP",
                        _ => "FAIL",
                    };
                    var detail = jb.Status == JobStatus.Done ? jb.OutputPath : jb.Message;
                    var w = jb.Status == JobStatus.Failed ? Console.Error : Console.Out;
                    w.WriteLine($"[{n}/{jobs.Count}] {tag} {jb.InputPath}{(string.IsNullOrEmpty(detail) ? "" : " -> " + detail)}");
                    if (jb.Status == JobStatus.Done && !string.IsNullOrEmpty(jb.Message))
                        Console.WriteLine("       note: " + jb.Message);
                }
            };
        }

        if (!o.Quiet)
            Console.WriteLine($"Processing {jobs.Count} video(s) with preset '{settings.Name}' " +
                              $"({options.MaxParallelProcesses} ffmpeg processes, {options.MaxParallelVideos} videos in parallel)…");

        var stats = await new BatchProcessor(tools).RunAsync(jobs, settings, options, cts.Token);
        if (!o.Quiet || stats.Failed > 0)
            Console.WriteLine($"Done in {Formatting.Duration(stats.Elapsed.TotalSeconds)}: {stats.Done} created, {stats.Skipped} skipped, " +
                              $"{stats.Failed} failed{(stats.Cancelled > 0 ? $", {stats.Cancelled} cancelled" : "")}.");
        if (cts.IsCancellationRequested) return ExitCancelled;
        return stats.Failed > 0 ? ExitFailures : ExitOk;
    }

    /// <summary>Finds ffmpeg; offers the download on first use (asks unless --yes).</summary>
    private static async Task<FfmpegTools?> EnsureFfmpegAsync(string? customDir, bool yes, AppSettings app)
    {
        if (FfmpegLocator.Locate(customDir) is { } found) return found;

        var installer = FfmpegInstaller.ForCurrentPlatform();
        if (installer is null)
        {
            Console.Error.WriteLine("ffmpeg was not found and there is no automatic download for this platform. " +
                                    "Install ffmpeg and ffprobe and put them on PATH, or pass --ffmpeg <folder>.");
            return null;
        }

        if (!yes)
        {
            if (Console.IsInputRedirected)
            {
                Console.Error.WriteLine("ffmpeg was not found. Run 'vthumb ffmpeg install' or pass --yes to download it automatically.");
                return null;
            }
            Console.Write($"ffmpeg was not found. Download it now from {installer.Source.Homepage} (about 100–150 MB) into\n  {installer.InstallRoot}\n? [Y/n] ");
            var answer = Console.ReadLine()?.Trim().ToLowerInvariant();
            app.FfmpegDownloadAsked = true;
            app.Save();
            if (answer is "n" or "no" or "nein")
            {
                Console.Error.WriteLine("Without ffmpeg no sheets can be created.");
                return null;
            }
        }
        return await InstallAsync(installer, null);
    }

    private static async Task<FfmpegTools?> InstallAsync(FfmpegInstaller installer, FfmpegRelease? release)
    {
        try
        {
            Console.WriteLine($"Source: {installer.Source.Name}");
            var lastPct = -1;
            var progress = new Progress<DownloadProgress>(p =>
            {
                if (p.TotalBytes is not > 0) return;
                var pct = (int)(100 * p.BytesReceived / p.TotalBytes.Value);
                if (pct / 10 == lastPct / 10) return;
                lastPct = pct;
                Console.WriteLine($"  {p.File}: {pct}% of {Formatting.Size(p.TotalBytes.Value)}");
            });
            var tools = await installer.InstallAsync(release, progress);
            var version = await FfmpegLocator.GetVersionAsync(tools.FfmpegPath);
            Console.WriteLine($"Installed (SHA-256 verified): {version}\n  {Path.GetDirectoryName(tools.FfmpegPath)}");
            return tools;
        }
        catch (Exception ex) when (ex is HttpRequestException or InvalidDataException or InvalidOperationException or IOException or TaskCanceledException)
        {
            Console.Error.WriteLine("ffmpeg download failed: " + ex.Message);
            return null;
        }
    }

    // ------------------------------------------------------------------ ffmpeg

    private static async Task<int> FfmpegCommandAsync(List<string> args)
    {
        var sub = args.FirstOrDefault() ?? "status";
        var app = AppSettings.Load();
        var installer = FfmpegInstaller.ForCurrentPlatform();
        switch (sub)
        {
            case "status":
            {
                var tools = FfmpegLocator.Locate(app.FfmpegDirectory);
                if (tools is null)
                {
                    Console.WriteLine("ffmpeg: not found");
                    return ExitNoFfmpeg;
                }
                Console.WriteLine($"ffmpeg:  {tools.FfmpegPath}\nffprobe: {tools.FfprobePath}\norigin:  {tools.Origin}");
                Console.WriteLine("version: " + (await FfmpegLocator.GetVersionAsync(tools.FfmpegPath) ?? "unknown (cannot run)"));
                if (tools.Origin == FfmpegOrigin.Managed && installer?.ReadManifest() is { } m)
                    Console.WriteLine($"source:  {m.Source}, build {m.Version}, installed {m.InstalledAt:yyyy-MM-dd}");
                return ExitOk;
            }
            case "install" or "update" or "check":
            {
                if (installer is null) { Console.Error.WriteLine("No automatic ffmpeg download for this platform."); return ExitNoFfmpeg; }
                FfmpegUpdateInfo info;
                try { info = await installer.CheckForUpdateAsync(); }
                catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or TaskCanceledException)
                {
                    Console.Error.WriteLine("Could not query the ffmpeg download source: " + ex.Message);
                    return ExitFailures;
                }
                var installed = info.Installed;
                Console.WriteLine($"Installed: {(installed is null ? "none (managed)" : $"{installed.Version} ({installed.Source})")}");
                Console.WriteLine($"Latest:    {info.Latest.Version} ({info.Latest.SourceName})");
                app.LastFfmpegUpdateCheck = DateTimeOffset.UtcNow;
                app.Save();
                if (sub == "check")
                {
                    Console.WriteLine(installed is null ? "No managed ffmpeg installed. Run 'vthumb ffmpeg install' to download it."
                        : info.UpdateAvailable ? "An update is available. Run 'vthumb ffmpeg update'." : "Up to date.");
                    return ExitOk;
                }
                if (!info.UpdateAvailable && !args.Contains("--force"))
                {
                    Console.WriteLine("Up to date. Use --force to reinstall.");
                    return ExitOk;
                }
                return await InstallAsync(installer, info.Latest) is null ? ExitFailures : ExitOk;
            }
            case "uninstall":
                if (installer?.Uninstall() == true) Console.WriteLine("Managed ffmpeg removed.");
                else Console.WriteLine("No managed ffmpeg installed.");
                return ExitOk;
            default:
                throw new UsageException($"Unknown ffmpeg command '{sub}' (status, check, install, update, uninstall).");
        }
    }

    // ------------------------------------------------------------------ presets

    private static int PresetsCommand(List<string> args)
    {
        var store = new PresetStore();
        var sub = args.FirstOrDefault() ?? "list";
        switch (sub)
        {
            case "list":
                foreach (var p in store.LoadAll())
                    Console.WriteLine($"{p.Name,-16} {(PresetStore.IsBuiltIn(p.Name) ? "[built-in] " : "")}{p.Description}");
                Console.WriteLine($"\nUser presets: {store.Directory}");
                return ExitOk;
            case "show":
            {
                var name = args.ElementAtOrDefault(1) ?? throw new UsageException("presets show <name>");
                var p = store.Find(name) ?? throw new UsageException($"Preset not found: {name}");
                Console.WriteLine(JsonSerializer.Serialize(p, SettingsJson.Options));
                return ExitOk;
            }
            case "export":
            {
                var name = args.ElementAtOrDefault(1) ?? throw new UsageException("presets export <name> <file>");
                var file = args.ElementAtOrDefault(2) ?? throw new UsageException("presets export <name> <file>");
                var p = store.Find(name) ?? throw new UsageException($"Preset not found: {name}");
                PresetStore.SaveFile(p, file);
                Console.WriteLine($"Exported to {file}");
                return ExitOk;
            }
            case "import":
            {
                var file = args.ElementAtOrDefault(1) ?? throw new UsageException("presets import <file>");
                var p = PresetStore.LoadFile(file);
                Console.WriteLine($"Imported '{p.Name}' to {store.Save(p)}");
                return ExitOk;
            }
            case "delete":
            {
                var name = args.ElementAtOrDefault(1) ?? throw new UsageException("presets delete <name>");
                if (PresetStore.IsBuiltIn(name)) throw new UsageException("Built-in presets cannot be deleted.");
                Console.WriteLine(store.Delete(name) ? $"Deleted '{name}'." : $"No user preset named '{name}'.");
                return ExitOk;
            }
            default:
                throw new UsageException($"Unknown presets command '{sub}' (list, show, import, export, delete).");
        }
    }
}
