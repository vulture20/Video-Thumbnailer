using System.Globalization;
using VideoThumbnailer.Core.Localization;

namespace VideoThumbnailer.Cli;

/// <summary>Command line texts in English and German. The language is <see cref="Lang.Current"/>.</summary>
internal static class CliText
{
    public static readonly IReadOnlyDictionary<string, (string En, string De)> Table = new Dictionary<string, (string, string)>
    {
        ["Error"] = ("Error: {0}", "Fehler: {0}"),
        ["SeeHelp"] = ("Run 'vthumb --help' for usage.", "Hilfe mit 'vthumb --help'."),
        ["PresetNotFound"] = ("Preset not found: {0}", "Preset nicht gefunden: {0}"),
        ["PresetSaved"] = ("Preset saved: {0}", "Preset gespeichert: {0}"),
        ["NoInputs"] = ("No input files or folders given.", "Keine Dateien oder Ordner angegeben."),
        ["NotFoundWarning"] = ("Warning: not found: {0}", "Warnung: nicht gefunden: {0}"),
        ["NoVideos"] = ("No video files found.", "Keine Videodateien gefunden."),
        ["DryRunSummary"] = ("{0} video(s), preset '{1}'.", "{0} Video(s), Preset „{1}“."),
        ["Cancelling"] = ("Cancelling… (press Ctrl+C again to abort immediately)", "Wird abgebrochen… (Strg+C erneut drücken zum sofortigen Abbruch)"),
        ["Note"] = ("       note: {0}", "       Hinweis: {0}"),
        ["Processing"] = ("Processing {0} video(s) with preset '{1}' ({2} ffmpeg processes, {3} videos in parallel)…", "Verarbeite {0} Video(s) mit Preset „{1}“ ({2} ffmpeg-Prozesse, {3} Videos parallel)…"),
        ["Summary"] = ("Done in {0}: {1} created, {2} skipped, {3} failed.", "Fertig in {0}: {1} erstellt, {2} übersprungen, {3} fehlgeschlagen."),
        ["SummaryCancelled"] = ("Done in {0}: {1} created, {2} skipped, {3} failed, {4} cancelled.", "Fertig in {0}: {1} erstellt, {2} übersprungen, {3} fehlgeschlagen, {4} abgebrochen."),
        ["NoFfmpegNoDownload"] = ("ffmpeg was not found and there is no automatic download for this platform. Install ffmpeg and ffprobe and put them on PATH, or pass --ffmpeg <folder>.", "ffmpeg wurde nicht gefunden, und für diese Plattform gibt es keinen automatischen Download. Bitte ffmpeg und ffprobe installieren und in den PATH legen oder --ffmpeg <Ordner> angeben."),
        ["NoFfmpegNonInteractive"] = ("ffmpeg was not found. Run 'vthumb ffmpeg install' or pass --yes to download it automatically.", "ffmpeg wurde nicht gefunden. 'vthumb ffmpeg install' ausführen oder --yes angeben, um es automatisch herunterzuladen."),
        ["AskDownload"] = ("ffmpeg was not found. Download it now from {0} (about 100–150 MB) into\n  {1}\n? [Y/n] ", "ffmpeg wurde nicht gefunden. Jetzt von {0} herunterladen (etwa 100–150 MB) nach\n  {1}\n? [J/n] "),
        ["WithoutFfmpeg"] = ("Without ffmpeg no sheets can be created.", "Ohne ffmpeg können keine Sheets erstellt werden."),
        ["Source"] = ("Source: {0}", "Quelle: {0}"),
        ["DownloadProgress"] = ("  {0}: {1}% of {2}", "  {0}: {1} % von {2}"),
        ["Installed"] = ("Installed (SHA-256 verified): {0}\n  {1}", "Installiert (SHA-256 geprüft): {0}\n  {1}"),
        ["DownloadFailed"] = ("ffmpeg download failed: {0}", "ffmpeg-Download fehlgeschlagen: {0}"),
        ["FfmpegNotFound"] = ("ffmpeg: not found", "ffmpeg: nicht gefunden"),
        ["StatusPaths"] = ("ffmpeg:  {0}\nffprobe: {1}\norigin:  {2}", "ffmpeg:  {0}\nffprobe: {1}\nHerkunft: {2}"),
        ["StatusVersion"] = ("version: {0}", "Version: {0}"),
        ["VersionUnknown"] = ("unknown (cannot run)", "unbekannt (startet nicht)"),
        ["StatusSource"] = ("source:  {0}, build {1}, installed {2}", "Quelle:  {0}, Build {1}, installiert {2}"),
        ["Origin.Custom"] = ("custom folder", "eigener Ordner"),
        ["Origin.Managed"] = ("downloaded by vthumb", "von vthumb heruntergeladen"),
        ["Origin.SystemPath"] = ("system (PATH)", "System (PATH)"),
        ["NoDownloadForPlatform"] = ("No automatic ffmpeg download for this platform.", "Kein automatischer ffmpeg-Download für diese Plattform."),
        ["QueryFailed"] = ("Could not query the ffmpeg download source: {0}", "Die ffmpeg-Download-Quelle konnte nicht abgefragt werden: {0}"),
        ["InstalledLine"] = ("Installed: {0}", "Installiert: {0}"),
        ["InstalledNone"] = ("none (managed)", "keins (verwaltet)"),
        ["LatestLine"] = ("Latest:    {0} ({1})", "Neueste:     {0} ({1})"),
        ["NoManaged"] = ("No managed ffmpeg installed. Run 'vthumb ffmpeg install' to download it.", "Kein verwaltetes ffmpeg installiert. Mit 'vthumb ffmpeg install' herunterladen."),
        ["UpdateAvailable"] = ("An update is available. Run 'vthumb ffmpeg update'.", "Ein Update ist verfügbar. 'vthumb ffmpeg update' ausführen."),
        ["UpToDate"] = ("Up to date.", "Aktuell."),
        ["UpToDateForce"] = ("Up to date. Use --force to reinstall.", "Aktuell. Mit --force neu installieren."),
        ["ManagedRemoved"] = ("Managed ffmpeg removed.", "Verwaltetes ffmpeg entfernt."),
        ["NoManagedInstalled"] = ("No managed ffmpeg installed.", "Kein verwaltetes ffmpeg installiert."),
        ["UnknownFfmpegCommand"] = ("Unknown ffmpeg command '{0}' (status, check, install, update, uninstall).", "Unbekannter ffmpeg-Befehl „{0}“ (status, check, install, update, uninstall)."),
        ["BuiltIn"] = ("[built-in] ", "[eingebaut] "),
        ["UserPresets"] = ("User presets: {0}", "Eigene Presets: {0}"),
        ["ExportedTo"] = ("Exported to {0}", "Exportiert nach {0}"),
        ["ImportedTo"] = ("Imported '{0}' to {1}", "„{0}“ importiert nach {1}"),
        ["BuiltInNoDelete"] = ("Built-in presets cannot be deleted.", "Eingebaute Presets können nicht gelöscht werden."),
        ["Deleted"] = ("Deleted '{0}'.", "„{0}“ gelöscht."),
        ["NoUserPreset"] = ("No user preset named '{0}'.", "Kein eigenes Preset namens „{0}“."),
        ["UnknownPresetsCommand"] = ("Unknown presets command '{0}' (list, show, import, export, delete).", "Unbekannter presets-Befehl „{0}“ (list, show, import, export, delete)."),
        ["Usage"] = ("Usage: {0}", "Aufruf: {0}"),

        // argument parsing
        ["NeedsValue"] = ("Option {0} needs a value.", "Option {0} braucht einen Wert."),
        ["NeedsInt"] = ("Option {0} needs a whole number.", "Option {0} braucht eine ganze Zahl."),
        ["NeedsNumber"] = ("Option {0} needs a number.", "Option {0} braucht eine Zahl."),
        ["UnknownOption"] = ("Unknown option: {0}", "Unbekannte Option: {0}"),
        ["UnknownFormat"] = ("Unknown format '{0}' (jpg, png, webp).", "Unbekanntes Format „{0}“ (jpg, png, webp)."),
        ["UnknownOrder"] = ("Unknown order '{0}' (row, column, random).", "Unbekannte Reihenfolge „{0}“ (row, column, random)."),
        ["InvalidValue"] = ("Invalid value '{0}' for {1}. Allowed: {2}", "Ungültiger Wert „{0}“ für {1}. Erlaubt: {2}"),
        ["UnknownLanguage"] = ("Unknown language '{0}' (en, de, auto).", "Unbekannte Sprache „{0}“ (en, de, auto)."),
    };

    public static string T(string key, params object?[] args)
    {
        var e = Table[key];
        var s = Lang.Current == Lang.German ? e.De : e.En;
        return args.Length == 0 ? s : string.Format(CultureInfo.InvariantCulture, s, args);
    }
}
