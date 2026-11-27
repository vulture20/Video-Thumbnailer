# Begleitdatei `.vtsheet` (Format 1)

Neben einem Sheet schreibt Video-Thumbnailer auf Wunsch (`--sidecar` bzw. „Begleitdatei mit Kachel-Zeiten“) eine Datei `<Name>.vtsheet`. Sie ist UTF-8-JSON ohne BOM und offen dokumentiert, damit auch andere Programme (eigene Viewer, Webseiten, Skripte) sie lesen können. Der eingebaute Viewer (#39) startet damit das Video an der angeklickten Kachel.

Der Name folgt dem Sheet: `Film.jpg` → `Film.vtsheet`, ein animiertes `Film.sheet.mp4` → ebenfalls `Film.vtsheet`.

## Beispiel

```json
{
  "format": "video-thumbnailer-sheet",
  "version": 1,
  "generator": "Video-Thumbnailer 0.7.0",
  "created": "2026-11-27T03:10:00+00:00",
  "video": {
    "path": "../Videos/Film.mkv",
    "absolutePath": "D:\\Videos\\Film.mkv",
    "duration": 5025.5,
    "width": 1920,
    "height": 1080
  },
  "sheet": {
    "file": "Film.jpg",
    "width": 1920,
    "height": 1406,
    "animated": false
  },
  "tiles": [
    { "index": 0, "x": 10, "y": 148, "width": 380, "height": 214, "time": 100.51 },
    { "index": 1, "x": 396, "y": 148, "width": 380, "height": 214, "time": 300, "manual": true }
  ]
}
```

## Felder

| Feld | Bedeutung |
|---|---|
| `format` | immer `video-thumbnailer-sheet` |
| `version` | Formatversion, derzeit `1`. Leser lehnen höhere Versionen ab; neue, optionale Felder erhöhen die Version nicht |
| `generator`, `created` | Programmversion und Zeitpunkt (UTC, ISO 8601); nur zur Information |
| `video.path` | Pfad des Videos relativ zum Ordner der Begleitdatei, mit `/`. Fehlt, wenn das Video auf einem anderen Laufwerk liegt oder eine Webadresse ist |
| `video.absolutePath` | absoluter Pfad zur Zeit der Erstellung |
| `video.url` | Webadresse bei Videos per URL (#21) |
| `video.duration` | Dauer in Sekunden |
| `video.width`, `video.height` | angezeigte Größe (Drehung und Pixel-Seitenverhältnis berücksichtigt) |
| `sheet.file` | Dateiname des Sheets im selben Ordner |
| `sheet.width`, `sheet.height` | Größe des Sheets in Pixeln |
| `sheet.animated` | `true` bei animierten Sheets (dann ist `time` der Anfang des Ausschnitts) |
| `tiles[]` | Kacheln in Zeitfolge |
| `tiles[].index` | Position in der Zeitfolge (0 = früheste) |
| `tiles[].x`, `y`, `width`, `height` | Rechteck der Kachel im Sheet (Pixel, Ursprung oben links) |
| `tiles[].time` | Aufnahmezeitpunkt in Sekunden (der tatsächliche, nach dem Überspringen schwarzer Bilder) |
| `tiles[].manual` | `true`, wenn der Zeitpunkt von Hand gewählt wurde; fehlt sonst |
| `tiles[].clipSeconds` | nur bei animierten Sheets: Länge des Ausschnitts |

## Video finden

Ein Leser sucht das Video in dieser Reihenfolge: `path` relativ zur Begleitdatei (Ordner wurden zusammen verschoben), dann `absolutePath`, dann `url`.

## Zeitpunkte von Hand

Kommt ein Video wieder in die Warteschlange (oder läuft `vthumb` ohne `--times`), liest das Programm die Begleitdatei an der Stelle, an der das neue Sheet landen würde. Gehört sie zum selben Video, werden die Kacheln mit `"manual": true` als Zeitpunkte von Hand übernommen.
