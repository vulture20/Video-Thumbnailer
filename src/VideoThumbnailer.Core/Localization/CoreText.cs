using System.Globalization;

namespace VideoThumbnailer.Core.Localization;

/// <summary>Texts produced by the core library (job messages and labels drawn into sheets).</summary>
public static class CoreText
{
    private static readonly Dictionary<string, (string En, string De)> Table = new()
    {
        ["SheetExists"] = ("Sheet already exists", "Sheet existiert bereits"),
        ["NoVideoStream"] = ("No video stream found", "Keine Videospur gefunden"),
        ["NoFrameDecoded"] = ("Could not decode any frame", "Kein einziges Bild konnte gelesen werden"),
        ["FramesMissing"] = ("{0} frame(s) could not be decoded", "{0} Bild(er) konnten nicht gelesen werden"),
        ["Cancelled"] = ("Cancelled", "Abgebrochen"),
        ["OutputFolderMissing"] = ("Output location is 'output folder' but no output folder is set.", "Als Speicherort ist „Ausgabeordner“ gewählt, aber kein Ordner angegeben."),
        ["FfprobeFailed"] = ("ffprobe failed ({0}): {1}", "ffprobe fehlgeschlagen ({0}): {1}"),
        ["ArchiveMissingBinaries"] = ("The downloaded archive did not contain ffmpeg and ffprobe.", "Das heruntergeladene Archiv enthält kein ffmpeg und ffprobe."),
        ["FfmpegDoesNotRun"] = ("The downloaded ffmpeg does not run on this system.", "Das heruntergeladene ffmpeg läuft auf diesem System nicht."),
        ["ChecksumMismatch"] = ("Checksum mismatch for {0}: expected {1}, got {2}.", "Prüfsumme von {0} stimmt nicht: erwartet {1}, erhalten {2}."),
        ["NoSuitableBuild"] = ("No suitable ffmpeg build for '{0}' found in the latest release.", "Kein passender ffmpeg-Build für „{0}“ im aktuellen Release gefunden."),
        ["SignatureMismatch"] = ("Code signature of {0} is invalid or not from the expected publisher (Team ID {1}, found {2}).", "Code-Signatur von {0} ist ungültig oder stammt nicht vom erwarteten Herausgeber (Team-ID {1}, gefunden {2})."),
        ["NoChecksum"] = ("No valid SHA-256 checksum published for {0}.", "Für {0} ist keine gültige SHA-256-Prüfsumme veröffentlicht."),
        ["ArchiveMissing"] = ("{0} does not contain: {1}", "{0} enthält nicht: {1}"),

        ["TemplateSize"] = ("Grid template '{0}': columns must be 1-50 and rows 1-100.", "Raster-Vorlage „{0}“: Spalten müssen 1–50 und Zeilen 1–100 sein."),
        ["TemplateEmpty"] = ("Grid template '{0}' has no tiles.", "Raster-Vorlage „{0}“ hat keine Kacheln."),
        ["TemplateTooMany"] = ("Grid template '{0}' has more than {1} tiles.", "Raster-Vorlage „{0}“ hat mehr als {1} Kacheln."),
        ["TemplateTileOutside"] = ("Grid template '{0}': tile at {1},{2} (size {3}) lies outside the grid.", "Raster-Vorlage „{0}“: Kachel bei {1},{2} (Größe {3}) liegt außerhalb des Rasters."),
        ["TemplateTileOverlap"] = ("Grid template '{0}': tile at {1},{2} overlaps another tile.", "Raster-Vorlage „{0}“: Kachel bei {1},{2} überdeckt eine andere Kachel."),
        ["TemplateIgnored"] = ("The grid template is not usable, the plain grid is used instead.", "Die Raster-Vorlage ist nicht verwendbar, es gilt das einfache Raster."),
        ["WatermarkImageMissing"] = ("Watermark picture not found or not readable: {0}", "Wasserzeichen-Bild nicht gefunden oder nicht lesbar: {0}"),
        ["EncoderMissing"] = ("This ffmpeg has no encoder '{0}' for the chosen animation format.", "Diesem ffmpeg fehlt der Encoder „{0}“ für das gewählte Animationsformat."),
        ["BackgroundImageMissing"] = ("Background image not found or not readable: {0}", "Hintergrundbild nicht gefunden oder nicht lesbar: {0}"),

        // built-in preset descriptions
        ["Preset.Standard"] = ("4 × 5 tiles, 1920 px wide, JPG, info header and timestamps", "4 × 5 Kacheln, 1920 px breit, JPG, Info-Kopf und Zeitstempel"),
        ["Preset.Compact"] = ("3 × 3 tiles, 1280 px wide, small header", "3 × 3 Kacheln, 1280 px breit, kleiner Kopf"),
        ["Preset.Detailed"] = ("6 × 8 tiles, 3000 px wide, PNG", "6 × 8 Kacheln, 3000 px breit, PNG"),
        ["Preset.Web"] = ("5 × 4 tiles, 1600 px wide, WebP, light background", "5 × 4 Kacheln, 1600 px breit, WebP, heller Hintergrund"),

        // drawn into sheets
        ["Sheet.NoFrame"] = ("no frame", "kein Bild"),
        ["Sheet.NoAudio"] = ("none", "keine"),
        ["Sheet.NotAvailable"] = ("n/a", "k. A."),
    };

    /// <summary>Default info header per language. The first line is the title.</summary>
    public static string DefaultHeaderTemplate(string lang) => lang == Lang.German
        ? "{filename}\n" +
          "Größe: {size} · Dauer: {duration} · Bitrate: {bitrate}\n" +
          "Video: {vcodec} · {resolution} · {fps} fps · {pixfmt}{hdr}\n" +
          "Audio: {audio}"
        : "{filename}\n" +
          "Size: {size} · Duration: {duration} · Bitrate: {bitrate}\n" +
          "Video: {vcodec} · {resolution} · {fps} fps · {pixfmt}{hdr}\n" +
          "Audio: {audio}";

    /// <summary>True if the template is empty or one of the built-in default templates (so it follows the language).</summary>
    public static bool IsDefaultHeaderTemplate(string? template) =>
        string.IsNullOrWhiteSpace(template) ||
        Lang.Supported.Any(l => string.Equals(Normalize(template), Normalize(DefaultHeaderTemplate(l)), StringComparison.Ordinal));

    private static string Normalize(string s) => s.Replace("\r\n", "\n").Trim();

    public static string T(string key, params object?[] args) => TIn(Lang.Current, key, args);

    public static string TIn(string lang, string key, params object?[] args)
    {
        var s = Table.TryGetValue(key, out var e) ? (lang == Lang.German ? e.De : e.En) : key;
        return args.Length == 0 ? s : string.Format(CultureInfo.InvariantCulture, s, args);
    }

    internal static IEnumerable<string> Keys => Table.Keys;
    internal static (string En, string De) Entry(string key) => Table[key];
}
