using VideoThumbnailer.Core.Localization;

namespace VideoThumbnailer.Cli;

internal static class HelpText
{
    public static string Main => Lang.Current == Lang.German ? German : English;

    public const string English = """
        vthumb - create video contact sheets (thumbnail sheets)

        Usage:
          vthumb [options] <file|folder|URL>...  create sheets
          vthumb presets list|show|import|export|delete
          vthumb ffmpeg status|check|install|update|uninstall [--force] [--latest]
          vthumb --help | --version

        ffmpeg download:
          Installs the fixed ffmpeg build from the list in the project repository (SHA-256 stored there, not on the
          download server; the list built into the app is used if GitHub cannot be reached).
          --latest installs the newest build instead, checked only against the checksum on the download server.

        Language:
              --lang en|de|auto      language of help and messages (also VTHUMB_LANG)
              --sheet-lang auto|en|de  language of the texts drawn into the sheet

        Without --preset the settings last used in the GUI are applied (or 'Standard').

        Input:
          -r, --recursive            scan sub folders (default: on)
              --no-recursive         only the given folders
          -n, --dry-run              list videos and output paths, create nothing
          http(s)://…                videos on a web server are read directly, only the parts around the
                                     capture times are downloaded (server must support range requests to be fast).
                                     Their sheets go to -o, otherwise into the current folder.

        Preset and layout:
          -p, --preset <name|file>   preset name or path to a .vtpreset.json file
          -c, --columns <n>          tiles per row
              --rows <n>             rows
          -w, --width <px>           total sheet width
              --tile-width <px>      fixed tile width (overrides --width)
              --gap <px> / --margin <px>
              --order row|column|random
              --skip-start <%> / --skip-end <%>   ignore start/end of the video
              --times <list>         capture times by hand, one per tile in timeline order, for every input:
                                     e.g. "1:00,5:30,-,1:02:10.5"; "-" = automatic; "+s" sets the clip length
                                     of animated sheets ("5:30+2")
              --skip-black / --no-skip-black  replace black or one-colored frames by one close by (default: on)
              --black-search <s>     how far before/after to look (default 2)
              --no-portrait-swap     keep columns/rows for portrait videos (--portrait-swap: swap them)
              --background <color>   --font <family>

        Timestamps (colors as #RRGGBB or #AARRGGBB, AA = opacity):
              --timestamps / --no-timestamps
              --timestamp-pos TopLeft|TopRight|BottomLeft|BottomRight|Center
              --timestamp-size <pt>  --timestamp-bold / --no-timestamp-bold
              --timestamp-color <color>  --timestamp-opacity <0-1 or %>  --timestamp-padding <px>
              --outline-color <color>  --outline-width <px>  --no-outline
              --shadow / --no-shadow  --shadow-color <color>  --shadow-offset <px>
              --milliseconds / --no-milliseconds   e.g. 00:12:34.567

        Info header:
              --header / --no-header  --header-pos TopLeft|TopRight|BottomLeft|BottomRight
              --header-template <t>  e.g. "{filename}\n{size:GB} · {duration} · {resolution}"
              --title-size <pt>  --header-size <pt>  --title-color <color>  --header-color <color>
              --line-spacing <factor>  (default 1.25)

        Output:
          -f, --format jpg|png|webp  -q, --quality <1-100>
          -o, --output <folder>      write all sheets into this folder
              --keep-structure       with -o: recreate the input folder structure (--no-keep-structure)
              --next-to-video        write next to each video (default)
              --subfolder <name>     write into <video folder>/<name>
              --pattern <pattern>    file name, placeholders {name} {ext} {preset} {cols} {rows} {date} {time}
              --overwrite skip|overwrite|rename
              --frames / --no-frames also save every frame as its own image (still sheets only)
              --frames-folder <pattern>  folder for the frames, relative to the sheet (default {name}_frames)
              --save-preset <name>   save the resulting settings as a preset (--description <text>)

        Animated sheet (every tile plays a short clip):
          -a, --animated mp4|webm|gif  write an animated sheet instead of an image
              --clip-seconds <s>     length of each clip and of the animation (default 3)
              --fps <n>              frames per second (default 10)
              --still                still image even if the preset is animated

        Processing:
          -j, --jobs <n>             max. parallel ffmpeg processes (default: CPU cores)
              --videos <n>           max. videos in parallel
              --ffmpeg <folder>      use ffmpeg/ffprobe from this folder
          -y, --yes                  download ffmpeg without asking if it is missing
              --quiet                only print errors and the summary

        Exit codes: 0 ok, 1 some videos failed, 2 usage error, 3 ffmpeg missing, 130 cancelled.
        """;

    public const string German = """
        vthumb - Video-Kontaktabzüge (Thumbnail-Sheets) erstellen

        Aufruf:
          vthumb [Optionen] <Datei|Ordner|URL>... Sheets erstellen
          vthumb presets list|show|import|export|delete
          vthumb ffmpeg status|check|install|update|uninstall [--force] [--latest]
          vthumb --help | --version

        ffmpeg-Download:
          Installiert den fest hinterlegten ffmpeg-Build aus der Liste im Projekt-Repository (SHA-256 steht dort, nicht
          auf dem Download-Server; ohne Verbindung zu GitHub gilt die in die App eingebaute Liste).
          --latest installiert stattdessen den neuesten Build, nur gegen die Prüfsumme auf dem Download-Server geprüft.

        Sprache:
              --lang en|de|auto      Sprache von Hilfe und Meldungen (auch VTHUMB_LANG)
              --sheet-lang auto|en|de  Sprache der Texte im Sheet

        Ohne --preset gelten die zuletzt in der GUI verwendeten Einstellungen (sonst „Standard“).

        Eingabe:
          -r, --recursive            Unterordner durchsuchen (Standard: an)
              --no-recursive         nur die angegebenen Ordner
          -n, --dry-run              Videos und Ausgabepfade auflisten, nichts erstellen
          http(s)://…                Videos auf einem Webserver werden direkt gelesen, nur die Stellen um die
                                     Aufnahmezeitpunkte werden geladen (schnell nur, wenn der Server Range-Anfragen
                                     kann). Ihre Sheets landen in -o, sonst im aktuellen Ordner.

        Preset und Layout:
          -p, --preset <Name|Datei>  Preset-Name oder Pfad zu einer .vtpreset.json-Datei
          -c, --columns <n>          Kacheln pro Zeile
              --rows <n>             Zeilen
          -w, --width <px>           Gesamtbreite des Sheets
              --tile-width <px>      feste Kachelbreite (ersetzt --width)
              --gap <px> / --margin <px>
              --order row|column|random
              --skip-start <%> / --skip-end <%>   Anfang/Ende des Videos auslassen
              --times <Liste>        Aufnahmezeitpunkte von Hand, je Kachel in Zeitfolge, für jede Eingabe:
                                     z. B. "1:00,5:30,-,1:02:10.5"; "-" = automatisch; "+s" setzt die Länge des
                                     Ausschnitts bei animierten Sheets ("5:30+2")
              --skip-black / --no-skip-black  schwarze oder einfarbige Bilder durch ein Bild kurz davor/danach
                                     ersetzen (Standard: an)
              --black-search <s>     so weit davor/danach suchen (Standard 2)
              --no-portrait-swap     Spalten/Zeilen bei Hochkant-Videos nicht tauschen (--portrait-swap: tauschen)
              --background <Farbe>   --font <Schriftfamilie>

        Zeitstempel (Farben als #RRGGBB oder #AARRGGBB, AA = Deckkraft):
              --timestamps / --no-timestamps
              --timestamp-pos TopLeft|TopRight|BottomLeft|BottomRight|Center
              --timestamp-size <pt>  --timestamp-bold / --no-timestamp-bold
              --timestamp-color <Farbe>  --timestamp-opacity <0-1 oder %>  --timestamp-padding <px>
              --outline-color <Farbe>  --outline-width <px>  --no-outline
              --shadow / --no-shadow  --shadow-color <Farbe>  --shadow-offset <px>
              --milliseconds / --no-milliseconds   z. B. 00:12:34.567

        Info-Kopf:
              --header / --no-header  --header-pos TopLeft|TopRight|BottomLeft|BottomRight
              --header-template <t>  z. B. "{filename}\n{size:GB} · {duration} · {resolution}"
              --title-size <pt>  --header-size <pt>  --title-color <Farbe>  --header-color <Farbe>
              --line-spacing <Faktor>  (Standard 1,25)

        Ausgabe:
          -f, --format jpg|png|webp  -q, --quality <1-100>
          -o, --output <Ordner>      alle Sheets in diesen Ordner schreiben
              --keep-structure       mit -o: Ordnerstruktur der Eingabe nachbilden (--no-keep-structure)
              --next-to-video        neben das jeweilige Video schreiben (Standard)
              --subfolder <Name>     in <Videoordner>/<Name> schreiben
              --pattern <Muster>     Dateiname, Platzhalter {name} {ext} {preset} {cols} {rows} {date} {time}
              --overwrite skip|overwrite|rename
              --frames / --no-frames zusätzlich jedes Einzelbild als eigene Datei speichern (nur Standbild)
              --frames-folder <Muster>  Ordner für die Einzelbilder, relativ zum Sheet (Standard {name}_frames)
              --save-preset <Name>   resultierende Einstellungen als Preset speichern (--description <Text>)

        Animiertes Sheet (jede Kachel spielt einen kurzen Ausschnitt ab):
          -a, --animated mp4|webm|gif  animiertes Sheet statt eines Bildes erstellen
              --clip-seconds <s>     Länge jedes Ausschnitts und der Animation (Standard 3)
              --fps <n>              Bilder pro Sekunde (Standard 10)
              --still                Standbild, auch wenn das Preset animiert ist

        Verarbeitung:
          -j, --jobs <n>             max. parallele ffmpeg-Prozesse (Standard: CPU-Kerne)
              --videos <n>           max. Videos gleichzeitig
              --ffmpeg <Ordner>      ffmpeg/ffprobe aus diesem Ordner verwenden
          -y, --yes                  ffmpeg ohne Nachfrage herunterladen, falls es fehlt
              --quiet                nur Fehler und die Zusammenfassung ausgeben

        Exit-Codes: 0 ok, 1 einzelne Videos fehlgeschlagen, 2 Aufruffehler, 3 ffmpeg fehlt, 130 abgebrochen.
        """;
}
