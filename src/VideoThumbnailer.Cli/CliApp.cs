using System.Text.Json;
using VideoThumbnailer.Core;
using VideoThumbnailer.Core.Batch;
using VideoThumbnailer.Core.Ffmpeg;
using VideoThumbnailer.Core.Input;
using VideoThumbnailer.Core.Layout;
using VideoThumbnailer.Core.Localization;
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
            args = ApplyLanguage(args);
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
            Console.Error.WriteLine(CliText.T("Error", ex.Message));
            Console.Error.WriteLine(CliText.T("SeeHelp"));
            return ExitUsage;
        }
    }

    /// <summary>
    /// Sets <see cref="Lang.Current"/>: --lang option, then VTHUMB_LANG, then the GUI language from settings.json,
    /// then the system language. Removes --lang from the arguments.
    /// </summary>
    internal static string[] ApplyLanguage(string[] args)
    {
        string? chosen = null;
        var rest = new List<string>();
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] == "--lang" && i + 1 < args.Length) { chosen = args[++i]; continue; }
            if (args[i].StartsWith("--lang=", StringComparison.Ordinal)) { chosen = args[i][7..]; continue; }
            rest.Add(args[i]);
        }
        var lang = Lang.Normalize(chosen) ?? Lang.Normalize(Environment.GetEnvironmentVariable("VTHUMB_LANG"));
        if (lang is null && (chosen is null || Lang.Normalize(chosen) is null && chosen.ToLowerInvariant() is not ("auto" or "system")))
            lang = Lang.Normalize(AppSettings.Load().Language);
        Lang.Current = lang ?? Lang.Detect();
        if (chosen is not null && Lang.Normalize(chosen) is null && chosen.ToLowerInvariant() is not ("auto" or "system"))
            throw new UsageException(CliText.T("UnknownLanguage", chosen));
        return rest.ToArray();
    }

    // ------------------------------------------------------------------ generate

    /// <summary>Screen for "fit to screen": --screen, otherwise the one the GUI saw last, otherwise the fixed width.</summary>
    private static void ApplyScreen(SheetSettings settings, GenerateOptions o, AppSettings app)
    {
        ScreenFitting.Current = o.Screen ?? app.LastScreen;
        if (settings.FitToScreen == ScreenFit.Off || o.Quiet) return;
        if (ScreenFitting.Current is { } screen)
            Console.WriteLine(CliText.T("ScreenUsed", screen, CliText.T(o.Screen is not null ? "ScreenFromOption" : "ScreenFromGui")));
        else
            Console.WriteLine(CliText.T("ScreenUnknown"));
    }

    private static async Task<int> GenerateAsync(GenerateOptions o)
    {
        var app = AppSettings.Load();
        var store = new PresetStore();

        SheetSettings settings;
        if (o.Preset is not null)
            settings = store.Find(o.Preset)?.Clone() ?? throw new UsageException(CliText.T("PresetNotFound", o.Preset));
        else
            settings = app.LastSettings?.Clone() ?? BuiltInPresets.Default; // spec: without preset use last GUI settings
        foreach (var apply in o.Overrides) apply(settings);
        var problems = settings.Normalize();
        if (problems.Count > 0) throw new UsageException(string.Join(" ", problems));
        ApplyScreen(settings, o, app);

        if (o.SavePresetAs is not null)
        {
            settings.Name = o.SavePresetAs;
            var path = store.Save(settings);
            Console.WriteLine(CliText.T("PresetSaved", path));
            if (o.Inputs.Count == 0) return ExitOk;
        }

        if (o.Inputs.Count == 0) throw new UsageException(CliText.T("NoInputs"));
        // Sheets of remote videos have no folder next to the video: without -o they go into the current folder.
        OutputPathResolver.RemoteFallbackFolder = Environment.CurrentDirectory;
        var missing = o.Inputs.Where(p => !VideoSource.IsUrl(p) && !File.Exists(p) && !Directory.Exists(p)).ToList();
        foreach (var m in missing) Console.Error.WriteLine(CliText.T("NotFoundWarning", m));

        var inputs = VideoScanner.Scan(o.Inputs, o.Recursive ?? app.Recursive);
        if (inputs.Count == 0)
        {
            Console.Error.WriteLine(CliText.T("NoVideos"));
            return ExitFailures;
        }

        if (o.InfoOnly && o.InfoExport is null) throw new UsageException(CliText.T("InfoOnlyNeedsFile"));
        if (o.DryRun)
        {
            foreach (var v in inputs)
            {
                if (o.InfoOnly) { Console.WriteLine(v.Path); continue; }
                if (!(settings.Trailer.Enabled && settings.Trailer.Only))
                    Console.WriteLine($"{v.Path}\n  -> {OutputPathResolver.ResolveSheetPath(settings, v.Path, v.Root)}");
                if (settings.Trailer.Enabled)
                    Console.WriteLine($"{v.Path}\n  -> {OutputPathResolver.ResolveTrailerPath(settings, v.Path, v.Root)}");
            }
            if (o.InfoExport is not null) Console.WriteLine("  => " + Path.GetFullPath(o.InfoExport));
            Console.WriteLine(CliText.T("DryRunSummary", inputs.Count, settings.Name));
            return ExitOk;
        }

        var tools = await EnsureFfmpegAsync(o.FfmpegDir ?? app.FfmpegDirectory, o.Yes, app);
        if (tools is null) return ExitNoFfmpeg;

        var jobs = inputs.Select(v => new ThumbnailJob(v.Path, v.Root) { ManualTimes = o.ManualTimes }).ToList();
        var options = new BatchOptions();
        if ((o.Jobs ?? app.MaxParallelProcesses) is { } j) options.MaxParallelProcesses = Math.Max(1, j);
        options.MaxParallelVideos = Math.Max(1, o.Videos ?? app.MaxParallelVideos ?? Math.Max(1, options.MaxParallelProcesses / 2));
        if (o.InfoOnly) return await ExportInfoAsync(tools, inputs, o, options) ? ExitOk : ExitFailures;

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            if (cts.IsCancellationRequested) return; // second Ctrl+C: hard exit
            e.Cancel = true;
            Console.Error.WriteLine("\n" + CliText.T("Cancelling"));
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
                        Console.WriteLine(CliText.T("Note", jb.Message));
                }
            };
        }

        if (!o.Quiet)
            Console.WriteLine(CliText.T("Processing", jobs.Count, settings.Name, options.MaxParallelProcesses, options.MaxParallelVideos));

        var stats = await new BatchProcessor(tools).RunAsync(jobs, settings, options, cts.Token);
        if (!o.Quiet || stats.Failed > 0)
        {
            var elapsed = Formatting.Duration(stats.Elapsed.TotalSeconds);
            Console.WriteLine(stats.Cancelled > 0
                ? CliText.T("SummaryCancelled", elapsed, stats.Done, stats.Skipped, stats.Failed, stats.Cancelled)
                : CliText.T("Summary", elapsed, stats.Done, stats.Skipped, stats.Failed));
        }
        if (cts.IsCancellationRequested) return ExitCancelled;
        var exported = o.InfoExport is null || await ExportInfoAsync(tools, inputs, o, options);
        return stats.Failed > 0 || !exported ? ExitFailures : ExitOk;
    }

    /// <summary>Writes the data of all inputs into the file of --export-info (#32). False if it could not be written.</summary>
    private static async Task<bool> ExportInfoAsync(FfmpegTools tools, IReadOnlyList<VideoInput> inputs, GenerateOptions o, BatchOptions options)
    {
        var file = Path.GetFullPath(o.InfoExport!);
        var rows = await InfoExport.ProbeAllAsync(tools.FfprobePath, inputs.Select(v => v.Path).ToList(), options.MaxParallelProcesses, CancellationToken.None);
        try { InfoExport.Write(file, rows); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine(CliText.T("InfoExportFailed", file, ex.Message));
            return false;
        }
        foreach (var r in rows.Where(r => r.Error is not null)) Console.Error.WriteLine($"FAIL {r.Path} -> {r.Error}");
        if (!o.Quiet) Console.WriteLine(CliText.T("InfoExported", rows.Count, file));
        return true;
    }

    /// <summary>Finds ffmpeg; offers the download on first use (asks unless --yes).</summary>
    private static async Task<FfmpegTools?> EnsureFfmpegAsync(string? customDir, bool yes, AppSettings app)
    {
        if (FfmpegLocator.Locate(customDir) is { } found) return found;

        var installer = FfmpegInstaller.ForCurrentPlatform();
        if (installer is null)
        {
            Console.Error.WriteLine(CliText.T("NoFfmpegNoDownload"));
            return null;
        }

        if (!yes)
        {
            if (Console.IsInputRedirected)
            {
                Console.Error.WriteLine(CliText.T("NoFfmpegNonInteractive"));
                return null;
            }
            Console.Write(CliText.T("AskDownload", installer.Source.Homepage, installer.InstallRoot));
            var answer = Console.ReadLine()?.Trim().ToLowerInvariant();
            app.FfmpegDownloadAsked = true;
            app.Save();
            if (answer is "n" or "no" or "nein")
            {
                Console.Error.WriteLine(CliText.T("WithoutFfmpeg"));
                return null;
            }
        }
        return await InstallAsync(installer, null);
    }

    private static async Task<FfmpegTools?> InstallAsync(FfmpegInstaller installer, FfmpegRelease? release)
    {
        try
        {
            Console.WriteLine(CliText.T("Source", installer.Source.Name));
            var lastPct = -1;
            var progress = new Progress<DownloadProgress>(p =>
            {
                if (p.TotalBytes is not > 0) return;
                var pct = (int)(100 * p.BytesReceived / p.TotalBytes.Value);
                if (pct / 10 == lastPct / 10) return;
                lastPct = pct;
                Console.WriteLine(CliText.T("DownloadProgress", p.File, pct, Formatting.Size(p.TotalBytes.Value, null, Lang.NumberFormat(Lang.Current))));
            });
            var tools = await installer.InstallAsync(release, progress);
            var version = await FfmpegLocator.GetVersionAsync(tools.FfmpegPath);
            Console.WriteLine(CliText.T("Installed", version, Path.GetDirectoryName(tools.FfmpegPath)));
            return tools;
        }
        catch (Exception ex) when (ex is HttpRequestException or InvalidDataException or InvalidOperationException or IOException or TaskCanceledException)
        {
            Console.Error.WriteLine(CliText.T("DownloadFailed", ex.Message));
            return null;
        }
    }

    // ------------------------------------------------------------------ ffmpeg

    private static async Task<int> FfmpegCommandAsync(List<string> args)
    {
        var sub = args.FirstOrDefault() ?? "status";
        var app = AppSettings.Load();
        var installer = FfmpegInstaller.ForCurrentPlatform(latest: args.Contains("--latest"));
        switch (sub)
        {
            case "status":
            {
                var tools = FfmpegLocator.Locate(app.FfmpegDirectory);
                if (tools is null)
                {
                    Console.WriteLine(CliText.T("FfmpegNotFound"));
                    return ExitNoFfmpeg;
                }
                Console.WriteLine(CliText.T("StatusPaths", tools.FfmpegPath, tools.FfprobePath, CliText.T("Origin." + tools.Origin)));
                Console.WriteLine(CliText.T("StatusVersion", await FfmpegLocator.GetVersionAsync(tools.FfmpegPath) ?? CliText.T("VersionUnknown")));
                if (tools.Origin == FfmpegOrigin.Managed && installer?.ReadManifest() is { } m)
                    Console.WriteLine(CliText.T("StatusSource", m.Source, m.Version, m.InstalledAt.ToString("yyyy-MM-dd")));
                return ExitOk;
            }
            case "install" or "update" or "check":
            {
                if (installer is null) { Console.Error.WriteLine(CliText.T("NoDownloadForPlatform")); return ExitNoFfmpeg; }
                FfmpegUpdateInfo info;
                try { info = await installer.CheckForUpdateAsync(); }
                catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or TaskCanceledException)
                {
                    Console.Error.WriteLine(CliText.T("QueryFailed", ex.Message));
                    return ExitFailures;
                }
                var installed = info.Installed;
                Console.WriteLine(CliText.T("InstalledLine", installed is null ? CliText.T("InstalledNone") : $"{installed.Version} ({installed.Source})"));
                Console.WriteLine(CliText.T("LatestLine", info.Latest.Version, info.Latest.SourceName));
                app.LastFfmpegUpdateCheck = DateTimeOffset.UtcNow;
                app.Save();
                if (sub == "check")
                {
                    Console.WriteLine(CliText.T(installed is null ? "NoManaged" : info.UpdateAvailable ? "UpdateAvailable" : "UpToDate"));
                    return ExitOk;
                }
                if (!info.UpdateAvailable && !args.Contains("--force"))
                {
                    Console.WriteLine(CliText.T("UpToDateForce"));
                    return ExitOk;
                }
                return await InstallAsync(installer, info.Latest) is null ? ExitFailures : ExitOk;
            }
            case "uninstall":
                Console.WriteLine(CliText.T(installer?.Uninstall() == true ? "ManagedRemoved" : "NoManagedInstalled"));
                return ExitOk;
            default:
                throw new UsageException(CliText.T("UnknownFfmpegCommand", sub));
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
                    Console.WriteLine($"{p.Name,-16} {(PresetStore.IsBuiltIn(p.Name) ? CliText.T("BuiltIn") : "")}{BuiltInPresets.Describe(p)}");
                Console.WriteLine("\n" + CliText.T("UserPresets", store.Directory));
                return ExitOk;
            case "show":
            {
                var name = args.ElementAtOrDefault(1) ?? throw new UsageException(CliText.T("Usage", "presets show <name>"));
                var p = store.Find(name) ?? throw new UsageException(CliText.T("PresetNotFound", name));
                Console.WriteLine(JsonSerializer.Serialize(p, SettingsJson.Options));
                return ExitOk;
            }
            case "export":
            {
                var name = args.ElementAtOrDefault(1) ?? throw new UsageException(CliText.T("Usage", "presets export <name> <file>"));
                var file = args.ElementAtOrDefault(2) ?? throw new UsageException(CliText.T("Usage", "presets export <name> <file>"));
                var p = store.Find(name) ?? throw new UsageException(CliText.T("PresetNotFound", name));
                PresetStore.ExportFile(p, file);
                Console.WriteLine(CliText.T("ExportedTo", file));
                return ExitOk;
            }
            case "import":
            {
                var file = args.ElementAtOrDefault(1) ?? throw new UsageException(CliText.T("Usage", "presets import <file>"));
                var p = PresetStore.LoadFile(file);
                Console.WriteLine(CliText.T("ImportedTo", p.Name, store.Save(p)));
                return ExitOk;
            }
            case "delete":
            {
                var name = args.ElementAtOrDefault(1) ?? throw new UsageException(CliText.T("Usage", "presets delete <name>"));
                if (PresetStore.IsBuiltIn(name)) throw new UsageException(CliText.T("BuiltInNoDelete"));
                Console.WriteLine(store.Delete(name) ? CliText.T("Deleted", name) : CliText.T("NoUserPreset", name));
                return ExitOk;
            }
            default:
                throw new UsageException(CliText.T("UnknownPresetsCommand", sub));
        }
    }
}
