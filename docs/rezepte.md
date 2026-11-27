# Rezepte

Fertige Aufrufe für häufige Aufgaben. Alle Optionen erklärt die [Tabelle in der README](../README.md#alle-befehle-und-optionen); `vthumb --dry-run …` zeigt vorher, was passieren würde.

## Eine Videosammlung einmal komplett erschließen

```bash
# Sheets in einen Unterordner "thumbnails" neben jedem Video, vorhandene überspringen
vthumb -p Standard --subfolder thumbnails --overwrite skip ~/Videos
```

`--subfolder` hält die Videoordner aufgeräumt. Liegen die Sheets direkt neben den Videos, tragen sie denselben Namen wie das Video (`Film.jpg`); manche Medienserver und Player nehmen ein solches Bild als Cover. Wer das nicht will, nimmt `--subfolder` oder ein Muster wie `--pattern "{name}.sheet"`.

## Regelmäßig nur neue Videos (Cronjob, Aufgabenplanung)

`--overwrite skip` lässt vorhandene Sheets in Ruhe, `-y` lädt ffmpeg bei Bedarf ohne Rückfrage, `--quiet` gibt nur Fehler und die Zusammenfassung aus. Der Exit-Code ist `0`, wenn alles geklappt hat, und `1`, wenn einzelne Videos fehlgeschlagen sind.

```bash
# Linux/macOS: crontab -e, jede Nacht um 3 Uhr
0 3 * * *  /pfad/zu/vthumb -y --quiet -p Standard --subfolder thumbnails --overwrite skip /srv/videos >> ~/vthumb.log 2>&1
```

```bat
:: Windows: Aufgabe anlegen (Pfad zum Installationsordner anpassen)
schtasks /Create /SC DAILY /ST 03:00 /TN "Video-Thumbnailer" ^
  /TR "\"%LOCALAPPDATA%\Programs\Video-Thumbnailer\vthumb.exe\" -y --quiet --overwrite skip D:\Videos"
```

## Alle Sheets an einem Ort, mit gleicher Ordnerstruktur

```bash
vthumb -o ~/Sheets --keep-structure ~/Videos
# ~/Videos/Serien/A/folge1.mkv -> ~/Sheets/Serien/A/folge1.jpg
```

## Für Foren und Chats: klein und schnell geladen

```bash
# 3 × 3, 900 px, WebP
vthumb -c 3 --rows 3 -w 900 -f webp -q 80 film.mkv

# als animiertes GIF (jede Kachel 2 s, 8 Bilder/s) – GIF wird schnell groß, also wenige Kacheln
vthumb -c 3 --rows 2 -w 720 -a gif --clip-seconds 2 --fps 8 film.mkv

# kurzer Vorschau-Clip statt Sheet
vthumb --trailer gif --trailer-only --trailer-clips 8 --trailer-seconds 1.5 --trailer-size 480 film.mkv
```

## Für die eigene Webseite: Vorschaubilder in der Zeitleiste

```bash
vthumb --webvtt -c 10 --rows 10 vortrag.mp4
# -> vortrag.thumbnails.vtt und vortrag.thumbnails.jpg
```

Die `.vtt`-Datei verweist auf Ausschnitte des Sprites (`#xywh=`). Web-Player wie Video.js (mit Thumbnail-Plugin), Plyr oder JW Player binden sie als „thumbnails“-Spur ein, z. B. `<track kind="metadata" label="thumbnails" src="vortrag.thumbnails.vtt">` (je nach Player).

## Übersicht einer Staffel

```bash
vthumb pack ~/Serien/Staffel1 -o ~/Staffel1.jpg --tiles 8 -w 2400
```

## Bestand erfassen: technische Daten als Tabelle

```bash
vthumb --export-info videos.csv --info-only ~/Videos
```

Eine Zeile je Video mit Größe, Dauer, Auflösung, Codecs, Bitraten, Tonspuren und Untertiteln, in jeder Tabellenkalkulation lesbar. Mit `.xml` statt `.csv` entsteht XML.

## Videos sichten und aussortieren

```bash
vthumb --sidecar -p Standard ~/Downloads/Videos
```

Danach die Sheets im Viewer öffnen (Doppelklick auf die `.vtsheet`-Datei oder auf einen erledigten Eintrag der Warteschlange): ein Klick auf eine Kachel spielt das Video ab dieser Stelle, <kbd>Entf</kbd> verschiebt es nach Rückfrage in den Papierkorb.

## Eigenes Aussehen speichern und weitergeben

```bash
vthumb -p Standard --layout big-top --corners rounded --tile-shadow --gap 12 --background "#1B2430" \
       --save-preset "Kino" --description "Großes Bild oben, runde Ecken"
vthumb presets export Kino Kino.vtpreset
```

Die Datei `Kino.vtpreset` öffnet sich auf einem anderen Rechner per Doppelklick. Was darin stehen kann, beschreibt [presets.md](presets.md).

## Ein Video direkt vom Webserver

```bash
vthumb -o ~/Sheets "https://example.org/videos/vortrag.mp4"
```

ffmpeg lädt nur die Stellen um die Aufnahmezeitpunkte. Das geht schnell, wenn der Server Range-Anfragen unterstützt (übliche Webserver und CDNs tun das).

## Skripte: Ergebnis auswerten

```bash
if vthumb -y --quiet -o ~/Sheets ~/Videos; then
  echo "alles erstellt"
else
  case $? in
    1) echo "einzelne Videos fehlgeschlagen" ;;
    2) echo "Aufruf fehlerhaft" ;;
    3) echo "ffmpeg fehlt" ;;
  esac
fi
```
