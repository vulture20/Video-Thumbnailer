using System.Text;

namespace VideoThumbnailer.Cli;

/// <summary>
/// One command line option for the documentation: all spellings (short, long, the "--no-…" counterpart), the value it
/// takes, a German description and an example call. <see cref="FileArgument"/> marks examples that name a file which
/// must exist, so the test that runs every example through the parser leaves them out.
/// </summary>
public sealed record CliOption(string[] Names, string? Value, string Description, string Example, bool FileArgument = false);

public sealed record CliOptionGroup(string Title, CliOption[] Options);

/// <summary>
/// Catalog of every option <see cref="ArgParser"/> understands, the source of the option table in README.md (#53).
/// A test checks that the catalog and the parser know the same options, that every example parses, that both help
/// texts mention every option and that the README table is what <see cref="Markdown"/> produces.
/// </summary>
public static class CliOptions
{
    private static CliOption O(string names, string? value, string description, string example, bool file = false) =>
        new(names.Split(' ', StringSplitOptions.RemoveEmptyEntries), value, description, example, file);

    public static readonly CliOptionGroup[] Groups =
    [
        new("Eingabe und Ablauf",
        [
            O("-r --recursive --no-recursive", null, "Unterordner mit durchsuchen (Standard: an) bzw. nur die angegebenen Ordner.", "vthumb --no-recursive ~/Videos"),
            O("-n --dry-run", null, "Nur auflisten, welche Videos gefunden werden und wohin die Sheets kämen; nichts erstellen.", "vthumb -n ~/Videos"),
            O("-j --jobs", "<n>", "Höchstens so viele ffmpeg-Prozesse gleichzeitig (Standard: Anzahl der CPU-Kerne).", "vthumb -j 4 ~/Videos"),
            O("--videos", "<n>", "Höchstens so viele Videos gleichzeitig bearbeiten.", "vthumb --videos 2 ~/Videos"),
            O("--ffmpeg", "<Ordner>", "ffmpeg und ffprobe aus diesem Ordner verwenden.", "vthumb --ffmpeg /opt/ffmpeg/bin film.mkv"),
            O("-y --yes", null, "Fehlt ffmpeg, ohne Nachfrage herunterladen (für Skripte).", "vthumb -y film.mkv"),
            O("--quiet", null, "Nur Fehler und die Zusammenfassung ausgeben.", "vthumb --quiet ~/Videos"),
            O("--lang", "de|en|auto", "Sprache von Hilfe und Meldungen (auch per Umgebungsvariable `VTHUMB_LANG`).", "vthumb --lang en ~/Videos"),
            O("--sheet-lang", "de|en|auto", "Sprache der Texte im Sheet (z. B. „Größe:“/„Size:“, Dezimalkomma).", "vthumb --sheet-lang en film.mkv"),
        ]),
        new("Preset und Raster",
        [
            O("-p --preset", "<Name|Datei>", "Preset nach Name oder als `.vtpreset`-Datei. Ohne Angabe gelten die zuletzt in der Oberfläche benutzten Einstellungen.", "vthumb -p Compact film.mkv"),
            O("--save-preset", "<Name>", "Die Einstellungen dieses Aufrufs als Preset speichern.", "vthumb -c 5 --rows 6 --save-preset \"5x6\""),
            O("--description", "<Text>", "Beschreibung für das mit `--save-preset` gespeicherte Preset.", "vthumb -c 5 --save-preset Breit --description \"5 Spalten\""),
            O("-c --columns", "<n>", "Kacheln pro Zeile.", "vthumb -c 6 film.mkv"),
            O("--rows", "<n>", "Anzahl der Zeilen.", "vthumb --rows 8 film.mkv"),
            O("-w --width", "<px>", "Gesamtbreite des Sheets; die Kachelhöhe ergibt sich aus dem Seitenverhältnis.", "vthumb -w 2560 film.mkv"),
            O("--tile-width", "<px>", "Feste Breite jeder Kachel (statt `--width`).", "vthumb --tile-width 320 film.mkv"),
            O("--layout", "<grid|Name|Datei>", "Spezial-Raster: `big-top`, `big-center`, `two-big`, `big-first`, ein eigenes Raster aus dem Editor oder eine `.vtgrid`-Datei; `grid` = einfaches Raster.", "vthumb --layout big-center film.mkv"),
            O("--fit-screen", "off|width|both", "Sheet so breit wie der Bildschirm (`width`) oder so groß, dass es ganz darauf passt (`both`).", "vthumb --fit-screen both film.mkv"),
            O("--screen", "<BxH>", "Bildschirmgröße für `--fit-screen` (Standard: der Bildschirm, auf dem die Oberfläche zuletzt war).", "vthumb --fit-screen both --screen 2560x1440 film.mkv"),
            O("--gap", "<px>", "Abstand zwischen den Kacheln.", "vthumb --gap 12 film.mkv"),
            O("--margin", "<px>", "Rand um das ganze Sheet.", "vthumb --margin 24 film.mkv"),
            O("--order", "row|column|random", "Reihenfolge der Kacheln: zeilenweise, spaltenweise oder zufällig.", "vthumb --order column film.mkv"),
            O("--portrait-swap --no-portrait-swap", null, "Bei Hochkant-Videos Spalten und Zeilen tauschen (Standard) bzw. beibehalten.", "vthumb --no-portrait-swap handy.mp4"),
        ]),
        new("Aufnahmezeitpunkte",
        [
            O("--skip-start", "<%>", "Diesen Anteil am Anfang des Videos auslassen (Vorspann).", "vthumb --skip-start 5 film.mkv"),
            O("--skip-end", "<%>", "Diesen Anteil am Ende auslassen (Abspann).", "vthumb --skip-end 8 film.mkv"),
            O("--times", "<Liste>", "Zeitpunkte von Hand, je Kachel in Zeitfolge: Sekunden, `mm:ss` oder `hh:mm:ss`; `-` = automatisch, `+s` = Länge des Ausschnitts bei animierten Sheets.", "vthumb -c 3 --rows 1 --times \"1:00,-,5:30+2\" film.mkv"),
            O("--skip-black --no-skip-black", null, "Schwarze oder einfarbige Bilder durch ein gutes Bild kurz davor oder danach ersetzen (Standard: an).", "vthumb --no-skip-black film.mkv"),
            O("--black-search", "<s>", "So viele Sekunden vor und nach dem Zeitpunkt nach einem guten Bild suchen (Standard 2).", "vthumb --black-search 5 film.mkv"),
        ]),
        new("Hintergrund, Kacheln und Rahmen",
        [
            O("--background", "<Farbe>", "Hintergrundfarbe als `#RRGGBB` oder `#AARRGGBB`.", "vthumb --background \"#101820\" film.mkv"),
            O("--background-image", "<Datei|none>", "Bild hinter den Kacheln.", "vthumb --background-image kino.jpg film.mkv", file: true),
            O("--background-mode", "fill|stretch|tile|center", "Hintergrundbild füllen, strecken, kacheln oder zentrieren (Standard `fill`).", "vthumb --background-mode tile film.mkv"),
            O("--background-opacity", "<0–1 oder %>", "Wie stark das Bild über der Hintergrundfarbe liegt (Standard 0,5).", "vthumb --background-opacity 40 film.mkv"),
            O("--font", "<Schriftfamilie>", "Schrift für Info-Kopf und Zeitstempel.", "vthumb --font \"DejaVu Sans\" film.mkv"),
            O("--tile-border --no-tile-border", "<px>", "Linie innen am Rand jeder Kachel bzw. keine.", "vthumb --tile-border 2 film.mkv"),
            O("--tile-border-color", "<Farbe>", "Farbe der Kachel-Linie.", "vthumb --tile-border 2 --tile-border-color \"#E6E6E6\" film.mkv"),
            O("--tile-shadow --no-tile-shadow", null, "Schlagschatten hinter jeder Kachel (fällt in den Abstand, also `--gap` setzen).", "vthumb --gap 12 --tile-shadow film.mkv"),
            O("--tile-shadow-color", "<Farbe>", "Farbe des Schattens (schaltet ihn ein).", "vthumb --tile-shadow-color \"#80000000\" film.mkv"),
            O("--tile-shadow-offset", "<px>", "Versatz des Schattens nach rechts unten (Standard 4).", "vthumb --tile-shadow-offset 6 film.mkv"),
            O("--tile-shadow-blur", "<px>", "Weichheit des Schattens (Standard 4).", "vthumb --tile-shadow-blur 8 film.mkv"),
            O("--grayscale --no-grayscale", null, "Kacheln in Graustufen.", "vthumb --grayscale film.mkv"),
            O("--corners", "square|rounded|cut", "Ecken der Kacheln: eckig, abgerundet oder abgeschnitten.", "vthumb --corners rounded film.mkv"),
            O("--corner-radius", "<px>", "Radius der runden bzw. Größe der abgeschnittenen Ecken (Standard 12).", "vthumb --corners rounded --corner-radius 20 film.mkv"),
            O("--sheet-border --no-sheet-border", "<px>", "Linie um das ganze Sheet (liegt im Rand) bzw. keine.", "vthumb --margin 16 --sheet-border 3 film.mkv"),
            O("--sheet-border-color", "<Farbe>", "Farbe der Linie um das Sheet.", "vthumb --sheet-border 3 --sheet-border-color \"#FFD700\" film.mkv"),
        ]),
        new("Zeitstempel",
        [
            O("--timestamps --no-timestamps", null, "Zeitstempel in jeder Kachel anzeigen bzw. weglassen.", "vthumb --no-timestamps film.mkv"),
            O("--timestamp-pos", "top-left|top-right|bottom-left|bottom-right|center", "Position des Zeitstempels in der Kachel.", "vthumb --timestamp-pos top-left film.mkv"),
            O("--timestamp-size", "<pt>", "Schriftgröße des Zeitstempels.", "vthumb --timestamp-size 18 film.mkv"),
            O("--timestamp-bold --no-timestamp-bold", null, "Zeitstempel fett bzw. normal.", "vthumb --no-timestamp-bold film.mkv"),
            O("--timestamp-color", "<Farbe>", "Textfarbe des Zeitstempels.", "vthumb --timestamp-color \"#FFFF00\" film.mkv"),
            O("--timestamp-opacity", "<0–1 oder %>", "Deckkraft des Zeitstempels.", "vthumb --timestamp-opacity 70 film.mkv"),
            O("--timestamp-padding", "<px>", "Abstand des Zeitstempels vom Kachelrand.", "vthumb --timestamp-padding 10 film.mkv"),
            O("--outline-color", "<Farbe>", "Farbe der Kontur um die Ziffern.", "vthumb --outline-color \"#FF0000\" film.mkv"),
            O("--outline-width --no-outline", "<px>", "Breite der Kontur bzw. keine Kontur.", "vthumb --outline-width 3 film.mkv"),
            O("--shadow --no-shadow", null, "Schatten unter dem Zeitstempel.", "vthumb --no-shadow film.mkv"),
            O("--shadow-color", "<Farbe>", "Farbe des Zeitstempel-Schattens (schaltet ihn ein).", "vthumb --shadow-color \"#000000\" film.mkv"),
            O("--shadow-offset", "<px>", "Versatz des Zeitstempel-Schattens.", "vthumb --shadow-offset 2 film.mkv"),
            O("--milliseconds --no-milliseconds", null, "Zeit mit Millisekunden, z. B. `00:12:34.567`.", "vthumb --milliseconds film.mkv"),
        ]),
        new("Info-Kopf und Kommentar",
        [
            O("--header --no-header", null, "Info-Kopf mit Videodaten über (oder unter) den Kacheln anzeigen bzw. weglassen.", "vthumb --no-header film.mkv"),
            O("--header-pos", "top-left|top-right|bottom-left|bottom-right", "Lage des Info-Kopfs.", "vthumb --header-pos bottom-left film.mkv"),
            O("--header-template", "<Vorlage>", "Zeilen des Info-Kopfs mit Platzhaltern (siehe unten), `\\n` für eine neue Zeile.", "vthumb --header-template \"{filename}\\n{size:GB} · {duration} · {resolution}\" film.mkv"),
            O("--title-size", "<pt>", "Schriftgröße der Titelzeile.", "vthumb --title-size 28 film.mkv"),
            O("--header-size", "<pt>", "Schriftgröße der übrigen Zeilen.", "vthumb --header-size 16 film.mkv"),
            O("--title-color", "<Farbe>", "Farbe der Titelzeile.", "vthumb --title-color \"#00FF00\" film.mkv"),
            O("--header-color", "<Farbe>", "Farbe der übrigen Zeilen.", "vthumb --header-color \"#CCCCCC\" film.mkv"),
            O("--line-spacing", "<Faktor>", "Zeilenabstand im Info-Kopf (Standard 1,25).", "vthumb --line-spacing 1.5 film.mkv"),
            O("--comment --no-comment", "<Text>", "Eigener Text unter dem Info-Kopf (oder an der Stelle von `{comment}`), `\\n` für weitere Zeilen.", "vthumb --comment \"Urlaub 2026\" film.mkv"),
        ]),
        new("Wasserzeichen",
        [
            O("--watermark", "<Text>", "Text als Wasserzeichen.", "vthumb --watermark \"© 2026\" film.mkv"),
            O("--watermark-image", "<Datei|none>", "Bild (z. B. PNG mit Transparenz) als Wasserzeichen; hat Vorrang vor dem Text.", "vthumb --watermark-image logo.png film.mkv", file: true),
            O("--no-watermark", null, "Kein Wasserzeichen (auch wenn das Preset eines hat).", "vthumb -p Web --no-watermark film.mkv"),
            O("--watermark-pos", "top-left|top-right|bottom-left|bottom-right|center|tiled", "Ecke, Mitte oder über die ganze Fläche wiederholt.", "vthumb --watermark \"Demo\" --watermark-pos tiled film.mkv"),
            O("--watermark-size", "<0–1 oder %>", "Breite im Verhältnis zum Sheet bzw. zur Kachel (Standard 0,2).", "vthumb --watermark \"Demo\" --watermark-size 0.1 film.mkv"),
            O("--watermark-opacity", "<0–1 oder %>", "Deckkraft (Standard 0,5).", "vthumb --watermark \"Demo\" --watermark-opacity 0.4 film.mkv"),
            O("--watermark-color", "<Farbe>", "Farbe des Text-Wasserzeichens.", "vthumb --watermark \"Demo\" --watermark-color \"#FFFFFF\" film.mkv"),
            O("--watermark-per-tile --watermark-sheet", null, "Wasserzeichen in jeder Kachel bzw. einmal über dem ganzen Sheet.", "vthumb --watermark \"Demo\" --watermark-per-tile film.mkv"),
        ]),
        new("Ausgabe",
        [
            O("-f --format", "jpg|png|webp", "Bildformat des Sheets.", "vthumb -f webp film.mkv"),
            O("-q --quality", "<1–100>", "Qualität für JPG und WebP.", "vthumb -q 80 film.mkv"),
            O("-o --output", "<Ordner>", "Alle Sheets in diesen Ordner schreiben.", "vthumb -o ~/Sheets ~/Videos"),
            O("--keep-structure --no-keep-structure", null, "Mit `-o`: die Ordnerstruktur der Eingabe nachbilden.", "vthumb -o ~/Sheets --keep-structure ~/Videos"),
            O("--next-to-video", null, "Sheet neben das jeweilige Video schreiben (Standard).", "vthumb --next-to-video ~/Videos"),
            O("--subfolder", "<Name>", "Sheet in einen Unterordner neben dem Video schreiben.", "vthumb --subfolder sheets ~/Videos"),
            O("--pattern", "<Muster>", "Dateiname mit `{name}` `{ext}` `{preset}` `{cols}` `{rows}` `{date}` `{time}`.", "vthumb --pattern \"{name}_{cols}x{rows}\" film.mkv"),
            O("--overwrite", "skip|overwrite|rename", "Was mit vorhandenen Sheets passiert.", "vthumb --overwrite rename film.mkv"),
            O("--frames --no-frames", null, "Zusätzlich jede Aufnahme als Einzelbild speichern.", "vthumb --frames film.mkv"),
            O("--frames-folder", "<Muster>", "Ordner der Einzelbilder, relativ zum Sheet (Standard `{name}_frames`).", "vthumb --frames-folder \"{name}-bilder\" film.mkv"),
        ]),
        new("Animiertes Sheet",
        [
            O("-a --animated", "mp4|h265|webm|ogv|gif|webp|apng", "Animiertes Sheet: jede Kachel spielt einen kurzen Ausschnitt ab ihrem Zeitpunkt.", "vthumb -a webp film.mkv"),
            O("--still", null, "Standbild, auch wenn das Preset animiert ist.", "vthumb -p MeinAnimiertes --still film.mkv"),
            O("--clip-seconds", "<s>", "Länge jedes Ausschnitts (Standard 3).", "vthumb -a mp4 --clip-seconds 5 film.mkv"),
            O("--fps", "<n>", "Bilder pro Sekunde der Animation (Standard 10).", "vthumb -a gif --fps 8 film.mkv"),
            O("--audio --no-audio", null, "Ton der größten (sonst ersten) Kachel mitnehmen; nur bei mp4, h265, webm, ogv.", "vthumb -a h265 --audio --layout big-center film.mkv"),
        ]),
        new("Vorschau-Clip (Trailer)",
        [
            O("--trailer --no-trailer", "mp4|h265|webm|ogv|gif|webp|apng", "Zusätzlich einen Clip aus kurzen Ausschnitten schreiben (`<Name>.preview.<Endung>`).", "vthumb --trailer mp4 film.mkv"),
            O("--trailer-only --with-sheet", null, "Nur den Clip, kein Sheet bzw. beides.", "vthumb --trailer gif --trailer-only film.mkv"),
            O("--trailer-clips", "<n>", "Anzahl der Ausschnitte (Standard 12).", "vthumb --trailer mp4 --trailer-clips 8 film.mkv"),
            O("--trailer-seconds", "<s>", "Länge jedes Ausschnitts (Standard 2,5; der Clip bleibt unter 2 Minuten).", "vthumb --trailer mp4 --trailer-seconds 1.5 film.mkv"),
            O("--trailer-fade", "<s>", "Überblendung zwischen den Ausschnitten, 0 = harter Schnitt (Standard 0,5).", "vthumb --trailer mp4 --trailer-fade 0 film.mkv"),
            O("--trailer-size", "<px>", "Längere Bildseite (Standard 640).", "vthumb --trailer gif --trailer-size 480 film.mkv"),
            O("--trailer-fps", "<n>", "Bilder pro Sekunde (Standard 24).", "vthumb --trailer webm --trailer-fps 30 film.mkv"),
            O("--trailer-timestamps --no-trailer-timestamps", null, "Zeit jedes Ausschnitts in der Ecke (Standard: an).", "vthumb --trailer mp4 --no-trailer-timestamps film.mkv"),
            O("--trailer-audio --no-trailer-audio", null, "Ton der Ausschnitte mitnehmen (mp4, h265, webm, ogv).", "vthumb --trailer mp4 --trailer-audio film.mkv"),
        ]),
        new("Begleitdateien und Video-Infos",
        [
            O("--sidecar --no-sidecar", null, "Begleitdatei `<Name>.vtsheet` mit Zeitpunkt und Rechteck jeder Kachel, für den Viewer.", "vthumb --sidecar film.mkv"),
            O("--webvtt --no-webvtt", null, "WebVTT-Vorschaubilder für Web-Player: `<Name>.thumbnails.vtt` und Sprite `<Name>.thumbnails.jpg`.", "vthumb --webvtt -c 10 --rows 10 film.mp4"),
            O("--webvtt-width", "<px>", "Breite eines Bildes im Sprite (Standard 160).", "vthumb --webvtt-width 240 film.mp4"),
            O("--export-info", "<Datei.csv|Datei.xml>", "Technische Daten aller Videos als CSV- oder XML-Tabelle (eine Zeile je Video).", "vthumb --export-info videos.csv ~/Videos"),
            O("--info-only", null, "Mit `--export-info`: nur die Tabelle, keine Sheets.", "vthumb --export-info videos.xml --info-only ~/Videos"),
        ]),
    ];

    /// <summary>Subcommands, shown in their own table.</summary>
    public static readonly (string Call, string Description)[] Commands =
    [
        ("vthumb [Optionen] <Datei|Ordner|URL>…", "Sheets erstellen (Standard)."),
        ("vthumb pack [Optionen] <Datei|Ordner>… -o <bild.jpg>", "Ein Übersichtsbild vieler Videos, eine Zeile je Video. `-c`/`--tiles` = Bilder je Video, `-w` = Breite; alle Sheet-Optionen gelten."),
        ("vthumb presets list", "Alle Presets mit Kurzbeschreibung und Speicherort."),
        ("vthumb presets show <Name>", "Ein Preset als JSON ausgeben."),
        ("vthumb presets export <Name> <Datei>", "Preset als `.vtpreset` speichern (zum Weitergeben)."),
        ("vthumb presets import <Datei>", "Preset aus einer Datei übernehmen."),
        ("vthumb presets delete <Name>", "Eigenes Preset löschen."),
        ("vthumb ffmpeg status", "Zeigt, welches ffmpeg benutzt wird."),
        ("vthumb ffmpeg check", "Sucht nach einem neueren hinterlegten ffmpeg-Build."),
        ("vthumb ffmpeg install|update [--force] [--latest]", "ffmpeg herunterladen bzw. aktualisieren (`--latest`: neuester Build statt des hinterlegten)."),
        ("vthumb ffmpeg uninstall", "Heruntergeladenes ffmpeg entfernen."),
        ("vthumb --help | --version", "Hilfe bzw. Version anzeigen."),
    ];

    /// <summary>All spellings of all options.</summary>
    public static IEnumerable<string> AllNames => Groups.SelectMany(g => g.Options).SelectMany(o => o.Names);

    /// <summary>The Markdown between the cli-options markers in README.md.</summary>
    public static string Markdown()
    {
        var sb = new StringBuilder();
        sb.Append("| Aufruf | Zweck |\n|---|---|\n");
        foreach (var (call, description) in Commands) sb.Append($"| `{Cell(call)}` | {Cell(description)} |\n");
        foreach (var g in Groups)
        {
            sb.Append($"\n#### {g.Title}\n\n| Option | Wert | Bedeutung | Beispiel |\n|---|---|---|---|\n");
            foreach (var o in g.Options)
            {
                var names = string.Join("<br>", o.Names.Select(n => $"`{n}`"));
                var value = o.Value is null ? "" : $"`{Cell(o.Value)}`";
                sb.Append($"| {names} | {value} | {Cell(o.Description)} | `{Cell(o.Example)}` |\n");
            }
        }
        return sb.ToString();
    }

    private static string Cell(string s) => s.Replace("|", "\\|");
}
