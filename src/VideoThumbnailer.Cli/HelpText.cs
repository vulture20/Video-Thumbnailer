using VideoThumbnailer.Core.Localization;

namespace VideoThumbnailer.Cli;

internal static class HelpText
{
    public static string Main => Lang.Current == Lang.German ? German : English;

    /// <summary>Full help; the option list comes from <see cref="CliOptions"/> (#53).</summary>
    public static string English => EnglishHead + CliOptions.Help(Lang.English) + "\n" + EnglishFoot;
    public static string German => GermanHead + CliOptions.Help(Lang.German) + "\n" + GermanFoot;

    private const string EnglishHead = """
        vthumb - create video contact sheets (thumbnail sheets)

        Usage:
          vthumb [options] <file|folder|URL>...  create sheets
          vthumb presets list|show|import|export|delete
          vthumb pack [options] <file|folder>... -o <pack.jpg>   one overview picture, a row per video (pack --help)
          vthumb ffmpeg status|check|install|update|uninstall [--force] [--latest]
          vthumb --help | --version

        Without --preset the settings last used in the GUI are applied (or 'Standard'). Every option changes the
        preset, in command line order; values also as --option=value. Colors as #RRGGBB or #AARRGGBB (AA = opacity).
        http(s)://… videos on a web server are read directly, only the parts around the capture times are downloaded
        (fast only if the server supports range requests); their sheets go to -o, otherwise into the current folder.

        """;

    private const string EnglishFoot = """
        ffmpeg download:
          Installs the fixed ffmpeg build from the list in the project repository (SHA-256 stored there, not on the
          download server; the list built into the app is used if GitHub cannot be reached).
          --latest installs the newest build instead, checked only against the checksum on the download server.

        Exit codes: 0 ok, 1 some videos failed, 2 usage error, 3 ffmpeg missing, 130 cancelled.
        """;

    private const string GermanHead = """
        vthumb - Video-Kontaktabzüge (Thumbnail-Sheets) erstellen

        Aufruf:
          vthumb [Optionen] <Datei|Ordner|URL>... Sheets erstellen
          vthumb presets list|show|import|export|delete
          vthumb pack [Optionen] <Datei|Ordner>... -o <pack.jpg>  ein Übersichtsbild, eine Zeile je Video (pack --help)
          vthumb ffmpeg status|check|install|update|uninstall [--force] [--latest]
          vthumb --help | --version

        Ohne --preset gelten die zuletzt in der GUI verwendeten Einstellungen (sonst „Standard“). Jede Option ändert
        das Preset, in der Reihenfolge der Kommandozeile; Werte auch als --option=wert. Farben als #RRGGBB oder
        #AARRGGBB (AA = Deckkraft).
        http(s)://… Videos auf einem Webserver werden direkt gelesen, nur die Stellen um die Aufnahmezeitpunkte werden
        geladen (schnell nur, wenn der Server Range-Anfragen kann); ihre Sheets landen in -o, sonst im aktuellen Ordner.

        """;

    private const string GermanFoot = """
        ffmpeg-Download:
          Installiert den fest hinterlegten ffmpeg-Build aus der Liste im Projekt-Repository (SHA-256 steht dort, nicht
          auf dem Download-Server; ohne Verbindung zu GitHub gilt die in die App eingebaute Liste).
          --latest installiert stattdessen den neuesten Build, nur gegen die Prüfsumme auf dem Download-Server geprüft.

        Exit-Codes: 0 ok, 1 einzelne Videos fehlgeschlagen, 2 Aufruffehler, 3 ffmpeg fehlt, 130 abgebrochen.
        """;

    public static string Pack => Lang.Current == Lang.German ? PackGerman : PackEnglish;

    public const string PackEnglish = """
        vthumb pack - one overview picture of many videos (a season, a folder)

        Usage:
          vthumb pack [options] <file|folder>... -o <pack.jpg|png|webp>

        Every video gets one row: its name, duration, resolution and size, below a strip of small pictures.
          -o, --output <file>        target picture; the format follows the extension (JPEG, PNG, WebP)
          -c, --columns, --tiles <n> pictures per video (default: columns of the preset)
          -w, --width <px>           width of the picture (default: sheet width of the preset)
          -p, --preset <name|file>   colours, font, time stamps, gaps and skipped start/end come from the preset;
                                     the sheet options (--background, --gap, --no-timestamps …) work as well
          -r, --recursive, -j, --jobs, --ffmpeg, -y, --dry-run, --quiet  as for sheets
        Too tall for the format (WebP 16383 px, JPEG 65000 px, PNG 32000 px): split into pack-1, pack-2 …
        """;

    public const string PackGerman = """
        vthumb pack - ein Übersichtsbild aus vielen Videos (eine Staffel, ein Ordner)

        Aufruf:
          vthumb pack [Optionen] <Datei|Ordner>... -o <pack.jpg|png|webp>

        Jedes Video bekommt eine Zeile: Name, Dauer, Auflösung und Größe, darunter ein Streifen kleiner Bilder.
          -o, --output <Datei>       Zielbild; das Format richtet sich nach der Endung (JPEG, PNG, WebP)
          -c, --columns, --tiles <n> Bilder je Video (Standard: Spalten des Presets)
          -w, --width <px>           Breite des Bildes (Standard: Sheet-Breite des Presets)
          -p, --preset <Name|Datei>  Farben, Schrift, Zeitstempel, Abstände und übersprungener Anfang/Ende kommen
                                     aus dem Preset; die Sheet-Optionen (--background, --gap, --no-timestamps …) gehen auch
          -r, --recursive, -j, --jobs, --ffmpeg, -y, --dry-run, --quiet  wie bei Sheets
        Zu hoch für das Format (WebP 16383 px, JPEG 65000 px, PNG 32000 px): Aufteilung in pack-1, pack-2 …
        """;
}
