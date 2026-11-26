using VideoThumbnailer.Core.Localization;

namespace VideoThumbnailer.Cli;

internal static class HelpText
{
    public static string Main => Lang.Current == Lang.German ? German : English;

    public const string English = """
        vthumb - create video contact sheets (thumbnail sheets)

        Usage:
          vthumb [options] <file|folder>...      create sheets
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

        Preset and layout:
          -p, --preset <name|file>   preset name or path to a .vtpreset.json file
          -c, --columns <n>          tiles per row
              --rows <n>             rows
          -w, --width <px>           total sheet width
              --tile-width <px>      fixed tile width (overrides --width)
              --gap <px> / --margin <px>
              --order row|column|random
              --skip-start <%> / --skip-end <%>   ignore start/end of the video
              --no-portrait-swap     keep columns/rows for portrait videos
              --background <#AARRGGBB>  --font <family>

        Overlays:
              --no-timestamps        --timestamp-pos TopLeft|TopRight|BottomLeft|BottomRight|Center
              --no-header            --header-pos TopLeft|TopRight|BottomLeft|BottomRight
              --header-template <t>  e.g. "{filename}\n{size:GB} · {duration} · {resolution}"

        Output:
          -f, --format jpg|png|webp  -q, --quality <1-100>
          -o, --output <folder>      write all sheets into this folder
              --keep-structure       with -o: recreate the input folder structure
              --next-to-video        write next to each video (default)
              --subfolder <name>     write into <video folder>/<name>
              --pattern <pattern>    file name, placeholders {name} {ext} {preset} {cols} {rows} {date} {time}
              --overwrite skip|overwrite|rename
              --frames               also save every frame as its own image (still sheets only)
              --save-preset <name>   save the resulting settings as a preset

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
          vthumb [Optionen] <Datei|Ordner>...    Sheets erstellen
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

        Preset und Layout:
          -p, --preset <Name|Datei>  Preset-Name oder Pfad zu einer .vtpreset.json-Datei
          -c, --columns <n>          Kacheln pro Zeile
              --rows <n>             Zeilen
          -w, --width <px>           Gesamtbreite des Sheets
              --tile-width <px>      feste Kachelbreite (ersetzt --width)
              --gap <px> / --margin <px>
              --order row|column|random
              --skip-start <%> / --skip-end <%>   Anfang/Ende des Videos auslassen
              --no-portrait-swap     Spalten/Zeilen bei Hochkant-Videos nicht tauschen
              --background <#AARRGGBB>  --font <Schriftfamilie>

        Einblendungen:
              --no-timestamps        --timestamp-pos TopLeft|TopRight|BottomLeft|BottomRight|Center
              --no-header            --header-pos TopLeft|TopRight|BottomLeft|BottomRight
              --header-template <t>  z. B. "{filename}\n{size:GB} · {duration} · {resolution}"

        Ausgabe:
          -f, --format jpg|png|webp  -q, --quality <1-100>
          -o, --output <Ordner>      alle Sheets in diesen Ordner schreiben
              --keep-structure       mit -o: Ordnerstruktur der Eingabe nachbilden
              --next-to-video        neben das jeweilige Video schreiben (Standard)
              --subfolder <Name>     in <Videoordner>/<Name> schreiben
              --pattern <Muster>     Dateiname, Platzhalter {name} {ext} {preset} {cols} {rows} {date} {time}
              --overwrite skip|overwrite|rename
              --frames               zusätzlich jedes Einzelbild als eigene Datei speichern (nur Standbild)
              --save-preset <Name>   resultierende Einstellungen als Preset speichern

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
