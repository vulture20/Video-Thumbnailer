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
              --layout <grid|name|file>  special grid with big and small tiles: big-top, big-center, two-big,
                                     big-first, or a JSON file with a template; "grid" = plain columns x rows
              --fit-screen off|width|both  sheet as wide as the screen, or as large as fits on it (width and height)
              --screen <WxH>         screen size for --fit-screen (default: the screen the GUI was last on;
                                     unknown = the fixed width applies)
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
              --background-image <file|none>  picture behind the tiles
              --background-mode fill|stretch|tile|center  (default fill)
              --background-opacity <0-1 or %>  how strongly the picture shows over the background color (default 0.5)

        Tiles and sheet (#27):
              --tile-border <px>  --tile-border-color <color>  --no-tile-border   line inside the edge of each tile
              --tile-shadow / --no-tile-shadow  --tile-shadow-color <color>  --tile-shadow-offset <px>
              --tile-shadow-blur <px>   drop shadow, falls into the gap (use a gap of about the offset)
              --grayscale / --no-grayscale      tiles in shades of gray
              --corners square|rounded|cut  --corner-radius <px>  (default 12)
              --sheet-border <px>  --sheet-border-color <color>  --no-sheet-border   line around the sheet

        Watermark (#29):
              --watermark <text>     text over the sheet  --watermark-image <file|none>  picture instead (PNG)
              --no-watermark         --watermark-pos TopLeft|TopRight|BottomLeft|BottomRight|Center|Tiled
              --watermark-size <0-1 or %>  width relative to the sheet (or tile), default 0.2
              --watermark-opacity <0-1 or %>  (default 0.5)  --watermark-color <color>
              --watermark-per-tile / --watermark-sheet   in every tile instead of once

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
              --comment <text>       own text below the header lines, or where the template has {comment};
                                     "\n" for more lines (--no-comment)

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
          -a, --animated mp4|h265|webm|ogv|gif|webp|apng  write an animated sheet instead of an image
                                     (mp4 = H.264; apng is lossless and large)
              --audio / --no-audio   sound of the biggest (or first) tile, for mp4, h265, webm, ogv
              --clip-seconds <s>     length of each clip and of the animation (default 3)
              --fps <n>              frames per second (default 10)
              --still                still image even if the preset is animated

        Preview clip (parts of the video one after the other, like a trailer; "<name>.preview.<ext>"):
              --trailer mp4|h265|webm|ogv|gif|webp|apng  also write a preview clip (--no-trailer)
              --trailer-only         only the preview clip, no sheet (--with-sheet)
              --trailer-clips <n>    number of parts (default 12)
              --trailer-seconds <s>  length of each part (default 2.5; the clip stays within 2 minutes)
              --trailer-fade <s>     cross-fade between parts, 0 = hard cut (default 0.5)
              --trailer-size <px>    longer side of the picture (default 640)
              --trailer-fps <n>      frames per second (default 24)
              --trailer-timestamps / --no-trailer-timestamps   time of each part in the corner (default on)
              --trailer-audio / --no-trailer-audio             sound of the parts (mp4, h265, webm, ogv)

        Video data and web players:
              --export-info <file>   write the data of all videos into one CSV or XML file (by extension)
              --info-only            with --export-info: only the data, no sheets
              --webvtt / --no-webvtt also write "<name>.thumbnails.vtt" with a sprite "<name>.thumbnails.jpg"
              --webvtt-width <px>    width of one picture in the sprite (default 160)

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
              --layout <grid|Name|Datei>  Spezial-Raster mit großen und kleinen Kacheln: big-top, big-center,
                                     two-big, big-first oder eine JSON-Datei mit Vorlage; "grid" = einfaches Raster
              --fit-screen off|width|both  Sheet so breit wie der Bildschirm oder so groß, dass es ganz darauf passt
              --screen <BxH>         Bildschirmgröße für --fit-screen (Standard: der Bildschirm, auf dem die
                                     Oberfläche zuletzt war; unbekannt = es gilt die feste Breite)
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
              --background-image <Datei|none>  Bild hinter den Kacheln
              --background-mode fill|stretch|tile|center  füllen, strecken, kacheln, zentriert (Standard fill)
              --background-opacity <0-1 oder %>  wie stark das Bild über der Hintergrundfarbe liegt (Standard 0,5)

        Kacheln und Sheet (#27):
              --tile-border <px>  --tile-border-color <Farbe>  --no-tile-border   Linie innen am Rand jeder Kachel
              --tile-shadow / --no-tile-shadow  --tile-shadow-color <Farbe>  --tile-shadow-offset <px>
              --tile-shadow-blur <px>   Schlagschatten, fällt in den Abstand (Abstand etwa so groß wie der Versatz)
              --grayscale / --no-grayscale      Kacheln in Graustufen
              --corners square|rounded|cut  --corner-radius <px>  eckig, abgerundet, abgeschnitten (Standard 12)
              --sheet-border <px>  --sheet-border-color <Farbe>  --no-sheet-border   Linie um das ganze Sheet

        Wasserzeichen (#29):
              --watermark <Text>     Text über dem Sheet  --watermark-image <Datei|none>  stattdessen ein Bild (PNG)
              --no-watermark         --watermark-pos TopLeft|TopRight|BottomLeft|BottomRight|Center|Tiled
              --watermark-size <0-1 oder %>  Breite im Verhältnis zum Sheet (bzw. zur Kachel), Standard 0,2
              --watermark-opacity <0-1 oder %>  (Standard 0,5)  --watermark-color <Farbe>
              --watermark-per-tile / --watermark-sheet   in jeder Kachel statt einmal

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
              --comment <Text>       eigener Text unter den Zeilen des Info-Kopfs oder dort, wo die Vorlage
                                     {comment} enthält; "\n" für weitere Zeilen (--no-comment)

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
          -a, --animated mp4|h265|webm|ogv|gif|webp|apng  animiertes Sheet statt eines Bildes erstellen
                                     (mp4 = H.264; apng ist verlustfrei und groß)
              --audio / --no-audio   Ton der größten (sonst ersten) Kachel, bei mp4, h265, webm, ogv
              --clip-seconds <s>     Länge jedes Ausschnitts und der Animation (Standard 3)
              --fps <n>              Bilder pro Sekunde (Standard 10)
              --still                Standbild, auch wenn das Preset animiert ist

        Vorschau-Clip (Ausschnitte nacheinander wie ein Trailer; „<Name>.preview.<Endung>“):
              --trailer mp4|h265|webm|ogv|gif|webp|apng  zusätzlich einen Vorschau-Clip schreiben (--no-trailer)
              --trailer-only         nur den Vorschau-Clip, kein Sheet (--with-sheet)
              --trailer-clips <n>    Anzahl der Ausschnitte (Standard 12)
              --trailer-seconds <s>  Länge jedes Ausschnitts (Standard 2,5; der Clip bleibt unter 2 Minuten)
              --trailer-fade <s>     Überblendung zwischen den Ausschnitten, 0 = harter Schnitt (Standard 0,5)
              --trailer-size <px>    längere Bildseite (Standard 640)
              --trailer-fps <n>      Bilder pro Sekunde (Standard 24)
              --trailer-timestamps / --no-trailer-timestamps   Zeit jedes Ausschnitts in der Ecke (Standard an)
              --trailer-audio / --no-trailer-audio             Ton der Ausschnitte (mp4, h265, webm, ogv)

        Video-Infos und Web-Player:
              --export-info <Datei>  Daten aller Videos in eine CSV- oder XML-Datei schreiben (nach Endung)
              --info-only            mit --export-info: nur die Daten, keine Sheets
              --webvtt / --no-webvtt zusätzlich „<Name>.thumbnails.vtt“ mit Sprite „<Name>.thumbnails.jpg“ schreiben
              --webvtt-width <px>    Breite eines Bildes im Sprite (Standard 160)

        Verarbeitung:
          -j, --jobs <n>             max. parallele ffmpeg-Prozesse (Standard: CPU-Kerne)
              --videos <n>           max. Videos gleichzeitig
              --ffmpeg <Ordner>      ffmpeg/ffprobe aus diesem Ordner verwenden
          -y, --yes                  ffmpeg ohne Nachfrage herunterladen, falls es fehlt
              --quiet                nur Fehler und die Zusammenfassung ausgeben

        Exit-Codes: 0 ok, 1 einzelne Videos fehlgeschlagen, 2 Aufruffehler, 3 ffmpeg fehlt, 130 abgebrochen.
        """;
}
