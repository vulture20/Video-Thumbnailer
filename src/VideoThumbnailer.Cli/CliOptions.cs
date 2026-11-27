using System.Text;
using System.Text.RegularExpressions;
using VideoThumbnailer.Core.Localization;

namespace VideoThumbnailer.Cli;

/// <summary>
/// One command line option: all spellings (short, long, the "--no-…" counterpart), the value it takes (German and
/// English placeholder), a German and an English description and an example call. <see cref="FileArgument"/> marks
/// examples that name a file which must exist, so the test that runs every example through the parser leaves them out.
/// Descriptions may use Markdown code spans (`…`); the help text drops the backticks.
/// </summary>
public sealed record CliOption(string[] Names, string? ValueDe, string? ValueEn, string De, string En, string Example, bool FileArgument = false)
{
    public string? Value(string lang) => lang == Lang.German ? ValueDe : ValueEn;
    public string Description(string lang) => lang == Lang.German ? De : En;
}

public sealed record CliOptionGroup(string De, string En, CliOption[] Options)
{
    public string Title(string lang) => lang == Lang.German ? De : En;
}

public sealed record CliCommand(string Call, string De, string En)
{
    public string Description(string lang) => lang == Lang.German ? De : En;
}

/// <summary>
/// Catalog of every option <see cref="ArgParser"/> understands (#53): the source of <c>vthumb --help</c> and of the
/// option tables in README.md and README.en.md. A test checks that the catalog and the parser know the same options,
/// that every example parses and that the README tables are what <see cref="Markdown"/> produces.
/// </summary>
public static class CliOptions
{
    private static CliOption O(string names, string? value, string example, string de, string en, string? valueEn = null, bool file = false) =>
        new(names.Split(' ', StringSplitOptions.RemoveEmptyEntries), value, valueEn ?? value, de, en, example, file);

    public static readonly CliCommand[] Commands =
    [
        new("vthumb [Optionen] <Datei|Ordner|URL>…", "Sheets erstellen (Standard).", "Create sheets (default)."),
        new("vthumb pack [Optionen] <Datei|Ordner>… -o <bild.jpg>",
            "Ein Übersichtsbild vieler Videos, eine Zeile je Video. `-c`/`--tiles` = Bilder je Video, `-w` = Breite; alle Sheet-Optionen gelten.",
            "One overview picture of many videos, a row per video. `-c`/`--tiles` = pictures per video, `-w` = width; all sheet options apply."),
        new("vthumb presets list", "Alle Presets mit Kurzbeschreibung und Speicherort.", "All presets with a short description and their folder."),
        new("vthumb presets show <Name>", "Ein Preset als JSON ausgeben.", "Print a preset as JSON."),
        new("vthumb presets export <Name> <Datei>", "Preset als `.vtpreset` speichern (zum Weitergeben).", "Save a preset as `.vtpreset` (to pass it on)."),
        new("vthumb presets import <Datei>", "Preset aus einer Datei übernehmen.", "Add a preset from a file."),
        new("vthumb presets delete <Name>", "Eigenes Preset löschen.", "Delete an own preset."),
        new("vthumb ffmpeg status", "Zeigt, welches ffmpeg benutzt wird.", "Shows which ffmpeg is used."),
        new("vthumb ffmpeg check", "Sucht nach einem neueren hinterlegten ffmpeg-Build.", "Looks for a newer pinned ffmpeg build."),
        new("vthumb ffmpeg install|update [--force] [--latest]",
            "ffmpeg herunterladen bzw. aktualisieren (`--latest`: neuester Build statt des hinterlegten).",
            "Download or update ffmpeg (`--latest`: newest build instead of the pinned one)."),
        new("vthumb ffmpeg uninstall", "Heruntergeladenes ffmpeg entfernen.", "Remove the downloaded ffmpeg."),
        new("vthumb --help | --version", "Hilfe bzw. Version anzeigen.", "Show help or version."),
    ];

    public static readonly CliOptionGroup[] Groups =
    [
        new("Eingabe und Ablauf", "Input and processing",
        [
            O("-r --recursive --no-recursive", null, "vthumb --no-recursive ~/Videos",
              "Unterordner mit durchsuchen (Standard: an) bzw. nur die angegebenen Ordner.",
              "Scan sub folders (default: on) or only the given folders."),
            O("-n --dry-run", null, "vthumb -n ~/Videos",
              "Nur auflisten, welche Videos gefunden werden und wohin die Sheets kämen; nichts erstellen.",
              "Only list the videos found and where their sheets would go; create nothing."),
            O("-j --jobs", "<n>", "vthumb -j 4 ~/Videos",
              "Höchstens so viele ffmpeg-Prozesse gleichzeitig (Standard: Anzahl der CPU-Kerne).",
              "At most this many ffmpeg processes at once (default: number of CPU cores)."),
            O("--videos", "<n>", "vthumb --videos 2 ~/Videos",
              "Höchstens so viele Videos gleichzeitig bearbeiten.", "At most this many videos at once."),
            O("--ffmpeg", "<Ordner>", "vthumb --ffmpeg /opt/ffmpeg/bin film.mkv",
              "ffmpeg und ffprobe aus diesem Ordner verwenden.", "Use ffmpeg and ffprobe from this folder.", "<folder>"),
            O("-y --yes", null, "vthumb -y film.mkv",
              "Fehlt ffmpeg, ohne Nachfrage herunterladen (für Skripte).", "Download ffmpeg without asking if it is missing (for scripts)."),
            O("--quiet", null, "vthumb --quiet ~/Videos",
              "Nur Fehler und die Zusammenfassung ausgeben.", "Only print errors and the summary."),
            O("--lang", "de|en|auto", "vthumb --lang en ~/Videos",
              "Sprache von Hilfe und Meldungen (auch per Umgebungsvariable `VTHUMB_LANG`).",
              "Language of help and messages (also environment variable `VTHUMB_LANG`)."),
            O("--sheet-lang", "de|en|auto", "vthumb --sheet-lang en film.mkv",
              "Sprache der Texte im Sheet (z. B. „Größe:“/„Size:“, Dezimalkomma).",
              "Language of the texts in the sheet (e.g. \"Size:\"/\"Größe:\", decimal comma)."),
        ]),
        new("Preset und Raster", "Preset and grid",
        [
            O("-p --preset", "<Name|Datei>", "vthumb -p Compact film.mkv",
              "Preset nach Name oder als `.vtpreset`-Datei. Ohne Angabe gelten die zuletzt in der Oberfläche benutzten Einstellungen.",
              "Preset by name or as a `.vtpreset` file. Without it the settings last used in the GUI apply.", "<name|file>"),
            O("--save-preset", "<Name>", "vthumb -c 5 --rows 6 --save-preset \"5x6\"",
              "Die Einstellungen dieses Aufrufs als Preset speichern.", "Save the settings of this call as a preset.", "<name>"),
            O("--description", "<Text>", "vthumb -c 5 --save-preset Breit --description \"5 Spalten\"",
              "Beschreibung für das mit `--save-preset` gespeicherte Preset.", "Description of the preset saved with `--save-preset`.", "<text>"),
            O("-c --columns", "<n>", "vthumb -c 6 film.mkv", "Kacheln pro Zeile.", "Tiles per row."),
            O("--rows", "<n>", "vthumb --rows 8 film.mkv", "Anzahl der Zeilen.", "Number of rows."),
            O("-w --width", "<px>", "vthumb -w 2560 film.mkv",
              "Gesamtbreite des Sheets; die Kachelhöhe ergibt sich aus dem Seitenverhältnis.",
              "Total width of the sheet; the tile height follows from the aspect ratio."),
            O("--tile-width", "<px>", "vthumb --tile-width 320 film.mkv",
              "Feste Breite jeder Kachel (statt `--width`).", "Fixed width of each tile (instead of `--width`)."),
            O("--layout", "<grid|Name|Datei>", "vthumb --layout big-center film.mkv",
              "Spezial-Raster: `big-top`, `big-center`, `two-big`, `big-first`, ein eigenes Raster aus dem Editor oder eine `.vtgrid`-Datei; `grid` = einfaches Raster.",
              "Special grid: `big-top`, `big-center`, `two-big`, `big-first`, an own grid from the editor or a `.vtgrid` file; `grid` = plain grid.",
              "<grid|name|file>"),
            O("--fit-screen", "off|width|both", "vthumb --fit-screen both film.mkv",
              "Sheet so breit wie der Bildschirm (`width`) oder so groß, dass es ganz darauf passt (`both`).",
              "Sheet as wide as the screen (`width`) or as large as fits on it entirely (`both`)."),
            O("--screen", "<BxH>", "vthumb --fit-screen both --screen 2560x1440 film.mkv",
              "Bildschirmgröße für `--fit-screen` (Standard: der Bildschirm, auf dem die Oberfläche zuletzt war).",
              "Screen size for `--fit-screen` (default: the screen the GUI was last on).", "<WxH>"),
            O("--gap", "<px>", "vthumb --gap 12 film.mkv", "Abstand zwischen den Kacheln.", "Gap between the tiles."),
            O("--margin", "<px>", "vthumb --margin 24 film.mkv", "Rand um das ganze Sheet.", "Margin around the whole sheet."),
            O("--order", "row|column|random", "vthumb --order column film.mkv",
              "Reihenfolge der Kacheln: zeilenweise, spaltenweise oder zufällig.", "Order of the tiles: by rows, by columns or random."),
            O("--portrait-swap --no-portrait-swap", null, "vthumb --no-portrait-swap handy.mp4",
              "Bei Hochkant-Videos Spalten und Zeilen tauschen (Standard) bzw. beibehalten.",
              "Swap columns and rows for portrait videos (default) or keep them."),
        ]),
        new("Aufnahmezeitpunkte", "Capture times",
        [
            O("--skip-start", "<%>", "vthumb --skip-start 5 film.mkv",
              "Diesen Anteil am Anfang des Videos auslassen (Vorspann).", "Leave out this share at the start of the video (intro)."),
            O("--skip-end", "<%>", "vthumb --skip-end 8 film.mkv",
              "Diesen Anteil am Ende auslassen (Abspann).", "Leave out this share at the end (credits)."),
            O("--times", "<Liste>", "vthumb -c 3 --rows 1 --times \"1:00,-,5:30+2\" film.mkv",
              "Zeitpunkte von Hand, je Kachel in Zeitfolge: Sekunden, `mm:ss` oder `hh:mm:ss`; `-` = automatisch, `+s` = Länge des Ausschnitts bei animierten Sheets.",
              "Capture times by hand, one per tile in timeline order: seconds, `mm:ss` or `hh:mm:ss`; `-` = automatic, `+s` = clip length of animated sheets.",
              "<list>"),
            O("--skip-black --no-skip-black", null, "vthumb --no-skip-black film.mkv",
              "Schwarze oder einfarbige Bilder durch ein gutes Bild kurz davor oder danach ersetzen (Standard: an).",
              "Replace black or one-coloured frames by a good one shortly before or after (default: on)."),
            O("--black-search", "<s>", "vthumb --black-search 5 film.mkv",
              "So viele Sekunden vor und nach dem Zeitpunkt nach einem guten Bild suchen (Standard 2).",
              "Look this many seconds before and after the time for a good frame (default 2)."),
        ]),
        new("Hintergrund, Kacheln und Rahmen", "Background, tiles and borders",
        [
            O("--background", "<Farbe>", "vthumb --background \"#101820\" film.mkv",
              "Hintergrundfarbe als `#RRGGBB` oder `#AARRGGBB`.", "Background colour as `#RRGGBB` or `#AARRGGBB`.", "<color>"),
            O("--background-image", "<Datei|none>", "vthumb --background-image kino.jpg film.mkv",
              "Bild hinter den Kacheln.", "Picture behind the tiles.", "<file|none>", file: true),
            O("--background-mode", "fill|stretch|tile|center", "vthumb --background-mode tile film.mkv",
              "Hintergrundbild füllen, strecken, kacheln oder zentrieren (Standard `fill`).",
              "Fill, stretch, tile or center the background picture (default `fill`)."),
            O("--background-opacity", "<0–1 oder %>", "vthumb --background-opacity 40 film.mkv",
              "Wie stark das Bild über der Hintergrundfarbe liegt (Standard 0,5).",
              "How strongly the picture shows over the background colour (default 0.5).", "<0–1 or %>"),
            O("--font", "<Schriftfamilie>", "vthumb --font \"DejaVu Sans\" film.mkv",
              "Schrift für Info-Kopf und Zeitstempel.", "Font of the info header and the time stamps.", "<family>"),
            O("--tile-border --no-tile-border", "<px>", "vthumb --tile-border 2 film.mkv",
              "Linie innen am Rand jeder Kachel bzw. keine.", "Line inside the edge of each tile, or none."),
            O("--tile-border-color", "<Farbe>", "vthumb --tile-border 2 --tile-border-color \"#E6E6E6\" film.mkv",
              "Farbe der Kachel-Linie.", "Colour of the tile line.", "<color>"),
            O("--tile-shadow --no-tile-shadow", null, "vthumb --gap 12 --tile-shadow film.mkv",
              "Schlagschatten hinter jeder Kachel (fällt in den Abstand, also `--gap` setzen).",
              "Drop shadow behind each tile (falls into the gap, so set `--gap`)."),
            O("--tile-shadow-color", "<Farbe>", "vthumb --tile-shadow-color \"#80000000\" film.mkv",
              "Farbe des Schattens (schaltet ihn ein).", "Colour of the shadow (turns it on).", "<color>"),
            O("--tile-shadow-offset", "<px>", "vthumb --tile-shadow-offset 6 film.mkv",
              "Versatz des Schattens nach rechts unten (Standard 4).", "Offset of the shadow to the bottom right (default 4)."),
            O("--tile-shadow-blur", "<px>", "vthumb --tile-shadow-blur 8 film.mkv",
              "Weichheit des Schattens (Standard 4).", "Softness of the shadow (default 4)."),
            O("--grayscale --no-grayscale", null, "vthumb --grayscale film.mkv", "Kacheln in Graustufen.", "Tiles in shades of grey."),
            O("--corners", "square|rounded|cut", "vthumb --corners rounded film.mkv",
              "Ecken der Kacheln: eckig, abgerundet oder abgeschnitten.", "Corners of the tiles: square, rounded or cut."),
            O("--corner-radius", "<px>", "vthumb --corners rounded --corner-radius 20 film.mkv",
              "Radius der runden bzw. Größe der abgeschnittenen Ecken (Standard 12).",
              "Radius of rounded or size of cut corners (default 12)."),
            O("--sheet-border --no-sheet-border", "<px>", "vthumb --margin 16 --sheet-border 3 film.mkv",
              "Linie um das ganze Sheet (liegt im Rand) bzw. keine.", "Line around the whole sheet (inside the margin), or none."),
            O("--sheet-border-color", "<Farbe>", "vthumb --sheet-border 3 --sheet-border-color \"#FFD700\" film.mkv",
              "Farbe der Linie um das Sheet.", "Colour of the line around the sheet.", "<color>"),
        ]),
        new("Zeitstempel", "Time stamps",
        [
            O("--timestamps --no-timestamps", null, "vthumb --no-timestamps film.mkv",
              "Zeitstempel in jeder Kachel anzeigen bzw. weglassen.", "Show the time stamp in each tile, or leave it out."),
            O("--timestamp-pos", "top-left|top-right|bottom-left|bottom-right|center", "vthumb --timestamp-pos top-left film.mkv",
              "Position des Zeitstempels in der Kachel.", "Position of the time stamp in the tile."),
            O("--timestamp-size", "<pt>", "vthumb --timestamp-size 18 film.mkv", "Schriftgröße des Zeitstempels.", "Font size of the time stamp."),
            O("--timestamp-bold --no-timestamp-bold", null, "vthumb --no-timestamp-bold film.mkv",
              "Zeitstempel fett bzw. normal.", "Time stamp bold or regular."),
            O("--timestamp-color", "<Farbe>", "vthumb --timestamp-color \"#FFFF00\" film.mkv",
              "Textfarbe des Zeitstempels.", "Text colour of the time stamp.", "<color>"),
            O("--timestamp-opacity", "<0–1 oder %>", "vthumb --timestamp-opacity 70 film.mkv",
              "Deckkraft des Zeitstempels.", "Opacity of the time stamp.", "<0–1 or %>"),
            O("--timestamp-padding", "<px>", "vthumb --timestamp-padding 10 film.mkv",
              "Abstand des Zeitstempels vom Kachelrand.", "Distance of the time stamp from the tile edge."),
            O("--outline-color", "<Farbe>", "vthumb --outline-color \"#FF0000\" film.mkv",
              "Farbe der Kontur um die Ziffern.", "Colour of the outline around the digits.", "<color>"),
            O("--outline-width --no-outline", "<px>", "vthumb --outline-width 3 film.mkv",
              "Breite der Kontur bzw. keine Kontur.", "Width of the outline, or no outline."),
            O("--shadow --no-shadow", null, "vthumb --no-shadow film.mkv",
              "Schatten unter dem Zeitstempel.", "Shadow under the time stamp."),
            O("--shadow-color", "<Farbe>", "vthumb --shadow-color \"#000000\" film.mkv",
              "Farbe des Zeitstempel-Schattens (schaltet ihn ein).", "Colour of the time stamp shadow (turns it on).", "<color>"),
            O("--shadow-offset", "<px>", "vthumb --shadow-offset 2 film.mkv",
              "Versatz des Zeitstempel-Schattens.", "Offset of the time stamp shadow."),
            O("--milliseconds --no-milliseconds", null, "vthumb --milliseconds film.mkv",
              "Zeit mit Millisekunden, z. B. `00:12:34.567`.", "Time with milliseconds, e.g. `00:12:34.567`."),
        ]),
        new("Info-Kopf und Kommentar", "Info header and comment",
        [
            O("--header --no-header", null, "vthumb --no-header film.mkv",
              "Info-Kopf mit Videodaten anzeigen bzw. weglassen.", "Show the info header with the video data, or leave it out."),
            O("--header-pos", "top-left|top-right|bottom-left|bottom-right", "vthumb --header-pos bottom-left film.mkv",
              "Lage des Info-Kopfs.", "Place of the info header."),
            O("--header-template", "<Vorlage>", "vthumb --header-template \"{filename}\\n{size:GB} · {duration} · {resolution}\" film.mkv",
              "Zeilen des Info-Kopfs mit Platzhaltern wie `{filename}`, `{size:GB}`, `{duration}`; `\\n` für eine neue Zeile.",
              "Lines of the info header with placeholders like `{filename}`, `{size:GB}`, `{duration}`; `\\n` for a new line.", "<template>"),
            O("--title-size", "<pt>", "vthumb --title-size 28 film.mkv", "Schriftgröße der Titelzeile.", "Font size of the title line."),
            O("--header-size", "<pt>", "vthumb --header-size 16 film.mkv", "Schriftgröße der übrigen Zeilen.", "Font size of the other lines."),
            O("--title-color", "<Farbe>", "vthumb --title-color \"#00FF00\" film.mkv", "Farbe der Titelzeile.", "Colour of the title line.", "<color>"),
            O("--header-color", "<Farbe>", "vthumb --header-color \"#CCCCCC\" film.mkv", "Farbe der übrigen Zeilen.", "Colour of the other lines.", "<color>"),
            O("--line-spacing", "<Faktor>", "vthumb --line-spacing 1.5 film.mkv",
              "Zeilenabstand im Info-Kopf (Standard 1,25).", "Line spacing in the info header (default 1.25).", "<factor>"),
            O("--comment --no-comment", "<Text>", "vthumb --comment \"Urlaub 2026\" film.mkv",
              "Eigener Text unter dem Info-Kopf (oder an der Stelle von `{comment}`), `\\n` für weitere Zeilen.",
              "Own text below the info header (or where the template has `{comment}`), `\\n` for more lines.", "<text>"),
        ]),
        new("Wasserzeichen", "Watermark",
        [
            O("--watermark", "<Text>", "vthumb --watermark \"© 2026\" film.mkv", "Text als Wasserzeichen.", "Text as watermark.", "<text>"),
            O("--watermark-image", "<Datei|none>", "vthumb --watermark-image logo.png film.mkv",
              "Bild (z. B. PNG mit Transparenz) als Wasserzeichen; hat Vorrang vor dem Text.",
              "Picture (e.g. PNG with transparency) as watermark; wins over the text.", "<file|none>", file: true),
            O("--no-watermark", null, "vthumb -p Web --no-watermark film.mkv",
              "Kein Wasserzeichen (auch wenn das Preset eines hat).", "No watermark (even if the preset has one)."),
            O("--watermark-pos", "top-left|top-right|bottom-left|bottom-right|center|tiled", "vthumb --watermark \"Demo\" --watermark-pos tiled film.mkv",
              "Ecke, Mitte oder über die ganze Fläche wiederholt.", "Corner, center or repeated over the whole area."),
            O("--watermark-size", "<0–1 oder %>", "vthumb --watermark \"Demo\" --watermark-size 0.1 film.mkv",
              "Breite im Verhältnis zum Sheet bzw. zur Kachel (Standard 0,2).", "Width relative to the sheet or tile (default 0.2).", "<0–1 or %>"),
            O("--watermark-opacity", "<0–1 oder %>", "vthumb --watermark \"Demo\" --watermark-opacity 0.4 film.mkv",
              "Deckkraft (Standard 0,5).", "Opacity (default 0.5).", "<0–1 or %>"),
            O("--watermark-color", "<Farbe>", "vthumb --watermark \"Demo\" --watermark-color \"#FFFFFF\" film.mkv",
              "Farbe des Text-Wasserzeichens.", "Colour of the text watermark.", "<color>"),
            O("--watermark-per-tile --watermark-sheet", null, "vthumb --watermark \"Demo\" --watermark-per-tile film.mkv",
              "Wasserzeichen in jeder Kachel bzw. einmal über dem ganzen Sheet.", "Watermark in every tile, or once over the whole sheet."),
        ]),
        new("Ausgabe", "Output",
        [
            O("-f --format", "jpg|png|webp", "vthumb -f webp film.mkv", "Bildformat des Sheets.", "Picture format of the sheet."),
            O("-q --quality", "<1–100>", "vthumb -q 80 film.mkv", "Qualität für JPG und WebP.", "Quality for JPG and WebP."),
            O("-o --output", "<Ordner>", "vthumb -o ~/Sheets ~/Videos", "Alle Sheets in diesen Ordner schreiben.", "Write all sheets into this folder.", "<folder>"),
            O("--keep-structure --no-keep-structure", null, "vthumb -o ~/Sheets --keep-structure ~/Videos",
              "Mit `-o`: die Ordnerstruktur der Eingabe nachbilden.", "With `-o`: recreate the folder structure of the input."),
            O("--next-to-video", null, "vthumb --next-to-video ~/Videos",
              "Sheet neben das jeweilige Video schreiben (Standard).", "Write each sheet next to its video (default)."),
            O("--subfolder", "<Name>", "vthumb --subfolder sheets ~/Videos",
              "Sheet in einen Unterordner neben dem Video schreiben.", "Write the sheet into a sub folder next to the video.", "<name>"),
            O("--pattern", "<Muster>", "vthumb --pattern \"{name}_{cols}x{rows}\" film.mkv",
              "Dateiname mit `{name}` `{ext}` `{preset}` `{cols}` `{rows}` `{date}` `{time}`.",
              "File name with `{name}` `{ext}` `{preset}` `{cols}` `{rows}` `{date}` `{time}`.", "<pattern>"),
            O("--overwrite", "skip|overwrite|rename", "vthumb --overwrite rename film.mkv",
              "Was mit vorhandenen Sheets passiert.", "What happens to existing sheets."),
            O("--frames --no-frames", null, "vthumb --frames film.mkv",
              "Zusätzlich jede Aufnahme als Einzelbild speichern.", "Also save every captured frame as its own picture."),
            O("--frames-folder", "<Muster>", "vthumb --frames-folder \"{name}-bilder\" film.mkv",
              "Ordner der Einzelbilder, relativ zum Sheet (Standard `{name}_frames`).",
              "Folder of the single frames, relative to the sheet (default `{name}_frames`).", "<pattern>"),
        ]),
        new("Animiertes Sheet", "Animated sheet",
        [
            O("-a --animated", "mp4|h265|webm|ogv|gif|webp|apng", "vthumb -a webp film.mkv",
              "Animiertes Sheet: jede Kachel spielt einen kurzen Ausschnitt ab ihrem Zeitpunkt.",
              "Animated sheet: each tile plays a short clip from its time on."),
            O("--still", null, "vthumb -p MeinAnimiertes --still film.mkv",
              "Standbild, auch wenn das Preset animiert ist.", "Still picture even if the preset is animated."),
            O("--clip-seconds", "<s>", "vthumb -a mp4 --clip-seconds 5 film.mkv",
              "Länge jedes Ausschnitts (Standard 3).", "Length of each clip (default 3)."),
            O("--fps", "<n>", "vthumb -a gif --fps 8 film.mkv",
              "Bilder pro Sekunde der Animation (Standard 10).", "Frames per second of the animation (default 10)."),
            O("--audio --no-audio", null, "vthumb -a h265 --audio --layout big-center film.mkv",
              "Ton der größten (sonst ersten) Kachel mitnehmen; nur bei mp4, h265, webm, ogv.",
              "Sound of the biggest (or first) tile; only for mp4, h265, webm, ogv."),
        ]),
        new("Vorschau-Clip (Trailer)", "Preview clip (trailer)",
        [
            O("--trailer --no-trailer", "mp4|h265|webm|ogv|gif|webp|apng", "vthumb --trailer mp4 film.mkv",
              "Zusätzlich einen Clip aus kurzen Ausschnitten schreiben (`<Name>.preview.<Endung>`).",
              "Also write a clip of short parts (`<name>.preview.<ext>`)."),
            O("--trailer-only --with-sheet", null, "vthumb --trailer gif --trailer-only film.mkv",
              "Nur den Clip, kein Sheet bzw. beides.", "Only the clip, no sheet, or both."),
            O("--trailer-clips", "<n>", "vthumb --trailer mp4 --trailer-clips 8 film.mkv",
              "Anzahl der Ausschnitte (Standard 12).", "Number of parts (default 12)."),
            O("--trailer-seconds", "<s>", "vthumb --trailer mp4 --trailer-seconds 1.5 film.mkv",
              "Länge jedes Ausschnitts (Standard 2,5; der Clip bleibt unter 2 Minuten).",
              "Length of each part (default 2.5; the clip stays within 2 minutes)."),
            O("--trailer-fade", "<s>", "vthumb --trailer mp4 --trailer-fade 0 film.mkv",
              "Überblendung zwischen den Ausschnitten, 0 = harter Schnitt (Standard 0,5).",
              "Cross-fade between the parts, 0 = hard cut (default 0.5)."),
            O("--trailer-size", "<px>", "vthumb --trailer gif --trailer-size 480 film.mkv",
              "Längere Bildseite (Standard 640).", "Longer side of the picture (default 640)."),
            O("--trailer-fps", "<n>", "vthumb --trailer webm --trailer-fps 30 film.mkv",
              "Bilder pro Sekunde (Standard 24).", "Frames per second (default 24)."),
            O("--trailer-timestamps --no-trailer-timestamps", null, "vthumb --trailer mp4 --no-trailer-timestamps film.mkv",
              "Zeit jedes Ausschnitts in der Ecke (Standard: an).", "Time of each part in the corner (default: on)."),
            O("--trailer-audio --no-trailer-audio", null, "vthumb --trailer mp4 --trailer-audio film.mkv",
              "Ton der Ausschnitte mitnehmen (mp4, h265, webm, ogv).", "Sound of the parts (mp4, h265, webm, ogv)."),
        ]),
        new("Begleitdateien und Video-Infos", "Companion files and video data",
        [
            O("--sidecar --no-sidecar", null, "vthumb --sidecar film.mkv",
              "Begleitdatei `<Name>.vtsheet` mit Zeitpunkt und Rechteck jeder Kachel, für den Viewer.",
              "Sidecar file `<name>.vtsheet` with the time and rectangle of every tile, for the viewer."),
            O("--webvtt --no-webvtt", null, "vthumb --webvtt -c 10 --rows 10 film.mp4",
              "WebVTT-Vorschaubilder für Web-Player: `<Name>.thumbnails.vtt` und Sprite `<Name>.thumbnails.jpg`.",
              "WebVTT thumbnails for web players: `<name>.thumbnails.vtt` and sprite `<name>.thumbnails.jpg`."),
            O("--webvtt-width", "<px>", "vthumb --webvtt-width 240 film.mp4",
              "Breite eines Bildes im Sprite (Standard 160).", "Width of one picture in the sprite (default 160)."),
            O("--export-info", "<Datei.csv|Datei.xml>", "vthumb --export-info videos.csv ~/Videos",
              "Technische Daten aller Videos als CSV- oder XML-Tabelle (eine Zeile je Video).",
              "Technical data of all videos as a CSV or XML table (a row per video).", "<file.csv|file.xml>"),
            O("--info-only", null, "vthumb --export-info videos.xml --info-only ~/Videos",
              "Mit `--export-info`: nur die Tabelle, keine Sheets.", "With `--export-info`: only the table, no sheets."),
        ]),
    ];

    /// <summary>All spellings of all options.</summary>
    public static IEnumerable<string> AllNames => Groups.SelectMany(g => g.Options).SelectMany(o => o.Names);

    /// <summary>The Markdown between the cli-options markers in README.md (German) or README.en.md (English).</summary>
    public static string Markdown(string lang)
    {
        var de = lang == Lang.German;
        var sb = new StringBuilder();
        sb.Append(de ? "| Aufruf | Zweck |\n|---|---|\n" : "| Call | Purpose |\n|---|---|\n");
        foreach (var c in Commands) sb.Append($"| `{Cell(de ? c.Call : English(c.Call))}` | {Cell(c.Description(lang))} |\n");
        foreach (var g in Groups)
        {
            sb.Append($"\n#### {g.Title(lang)}\n\n");
            sb.Append(de ? "| Option | Wert | Bedeutung | Beispiel |\n" : "| Option | Value | Meaning | Example |\n");
            sb.Append("|---|---|---|---|\n");
            foreach (var o in g.Options)
            {
                var names = string.Join("<br>", o.Names.Select(n => $"`{n}`"));
                var value = o.Value(lang) is { } v ? $"`{Cell(v)}`" : "";
                sb.Append($"| {names} | {value} | {Cell(o.Description(lang))} | `{Cell(o.Example)}` |\n");
            }
        }
        return sb.ToString();
    }

    private static string Cell(string s) => s.Replace("|", "\\|");

    /// <summary>Placeholders of the command calls in English.</summary>
    internal static string English(string call) => call
        .Replace("[Optionen]", "[options]").Replace("<Datei|Ordner|URL>", "<file|folder|URL>").Replace("<Datei|Ordner>", "<file|folder>")
        .Replace("<bild.jpg>", "<picture.jpg>").Replace("<Name>", "<name>").Replace("<Datei>", "<file>");

    // ------------------------------------------------------------------ help text

    /// <summary>The option part of <c>vthumb --help</c>: one block per group, the names left, the description right.</summary>
    public static string Help(string lang, int width = 112)
    {
        const int column = 31;
        var sb = new StringBuilder();
        foreach (var g in Groups)
        {
            sb.Append('\n').Append(g.Title(lang)).Append(":\n");
            foreach (var o in g.Options)
            {
                var left = "  " + HelpNames(o, lang);
                var text = PlainText(o.Description(lang));
                if (left.Length > column - 2) { sb.Append(left).Append('\n'); left = ""; }
                var lines = Wrap(text, width - column);
                sb.Append(left.PadRight(column)).Append(lines[0]).Append('\n');
                foreach (var l in lines.Skip(1)) sb.Append(new string(' ', column)).Append(l).Append('\n');
            }
        }
        return sb.ToString();
    }

    /// <summary>"-c, --columns &lt;n&gt;", "--outline-width &lt;px&gt;, --no-outline": the value follows the first long name.</summary>
    internal static string HelpNames(CliOption o, string lang)
    {
        var value = o.Value(lang);
        var firstLong = Array.FindIndex(o.Names, n => n.StartsWith("--", StringComparison.Ordinal));
        return string.Join(", ", o.Names.Select((n, i) => i == firstLong && value is not null ? n + " " + value : n));
    }

    internal static string PlainText(string markdown) => markdown.Replace("`", "");

    private static List<string> Wrap(string text, int width)
    {
        var lines = new List<string>();
        var line = new StringBuilder();
        foreach (var word in WhiteSpace().Split(text))
        {
            if (line.Length > 0 && line.Length + 1 + word.Length > width) { lines.Add(line.ToString()); line.Clear(); }
            if (line.Length > 0) line.Append(' ');
            line.Append(word);
        }
        lines.Add(line.ToString());
        return lines;
    }

    private static readonly Regex WhiteSpaceRegex = new(@"\s+");
    private static Regex WhiteSpace() => WhiteSpaceRegex;
}
