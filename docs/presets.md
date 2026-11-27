# Preset-Format (`.vtpreset`)

Ein Preset ist eine offene JSON-Datei mit allen Einstellungen eines Sheets. Eigene Presets liegen im Einstellungsordner unter `presets` (Windows `%APPDATA%\VideoThumbnailer`, Linux `~/.config/video-thumbnailer`, macOS `~/Library/Application Support/VideoThumbnailer`, im portablen Modus im Ordner `data`). Zum Weitergeben exportiert man sie als `<Name>.vtpreset` (Oberfläche: Menü am Preset, Kommandozeile: `vthumb presets export <Name> <Datei>`); ein Doppelklick auf die Datei nimmt das Preset auf und wählt es.

Die Datei ist UTF-8-JSON. Beim Lesen sind Kommentare (`//`, `/* */`), ein Komma nach dem letzten Eintrag und beliebige Groß-/Kleinschreibung der Namen erlaubt. **Fehlende Felder bekommen ihren Standardwert**, ein Preset muss also nur das enthalten, was vom Standard abweicht. Eine kleine Datei reicht:

```json
{
  "name": "Forum klein",
  "description": "3 × 3, 900 px, WebP",
  "columns": 3,
  "rows": 3,
  "sheetWidth": 900,
  "output": { "format": "Webp", "quality": 80 }
}
```

Jede Einstellung lässt sich auch auf der Kommandozeile ändern; die Spalte „Option“ nennt die passende Option ([alle Optionen](../README.md#alle-befehle-und-optionen)). Farben stehen als `#RRGGBB` oder `#AARRGGBB` (AA = Deckkraft, `FF` = deckend). Aufzählungswerte schreibt man wie angegeben (Groß-/Kleinschreibung egal).

## Oberste Ebene

| Feld | Standard | Bedeutung | Option |
|---|---|---|---|
| `schemaVersion` | `1` | Version des Formats | – |
| `name` | – | Name des Presets (eindeutig) | `--save-preset` |
| `description` | – | Beschreibung, in Listen und Tooltips | `--description` |
| `columns`, `rows` | `4`, `5` | Kacheln pro Zeile, Zeilen | `-c`, `--rows` |
| `template` | `null` | Spezial-Raster (siehe unten); `null` = einfaches Raster | `--layout` |
| `sheetWidth` | `1920` | Gesamtbreite in Pixeln | `-w` |
| `tileWidth` | `null` | feste Kachelbreite statt `sheetWidth` | `--tile-width` |
| `fitToScreen` | `Off` | `Off`, `Width` (so breit wie der Bildschirm), `WidthAndHeight` (passt ganz darauf) | `--fit-screen` |
| `gap`, `margin` | `6`, `10` | Abstand zwischen Kacheln, Rand | `--gap`, `--margin` |
| `portraitSwapGrid` | `true` | bei Hochkant-Videos Spalten und Zeilen tauschen | `--portrait-swap` |
| `order` | `RowMajor` | `RowMajor`, `ColumnMajor`, `Random` | `--order` |
| `skipStartPercent`, `skipEndPercent` | `2`, `2` | Anfang und Ende auslassen (Prozent) | `--skip-start`, `--skip-end` |
| `skipBlackFrames` | `true` | schwarze/einfarbige Bilder ersetzen | `--skip-black` |
| `blackFrameSearchSeconds` | `2` | Suchbereich davor/danach in Sekunden | `--black-search` |
| `backgroundColor` | `#FF1E1E1E` | Hintergrundfarbe | `--background` |
| `fontFamily` | `null` | Schriftfamilie; `null` = mitgelieferte DejaVu Sans | `--font` |
| `textLanguage` | `null` | Sprache der Texte im Sheet: `de`, `en`, `null` = wie das Programm | `--sheet-lang` |
| `sheetBorderWidth`, `sheetBorderColor` | `0`, `#FFFFFFFF` | Linie um das Sheet | `--sheet-border`, `--sheet-border-color` |

## `backgroundImage`

| Feld | Standard | Bedeutung | Option |
|---|---|---|---|
| `path` | `null` | Bilddatei; relativ = relativ zum Ordner der Preset-Datei | `--background-image` |
| `mode` | `Fill` | `Fill`, `Stretch`, `Tile`, `Center` | `--background-mode` |
| `opacity` | `0.5` | Deckkraft über der Hintergrundfarbe (0–1) | `--background-opacity` |

## `tileStyle`

| Feld | Standard | Bedeutung | Option |
|---|---|---|---|
| `borderWidth`, `borderColor` | `0`, `#FFFFFFFF` | Linie innen am Kachelrand | `--tile-border`, `--tile-border-color` |
| `shadow` | `false` | Schlagschatten | `--tile-shadow` |
| `shadowColor`, `shadowOffset`, `shadowBlur` | `#B4000000`, `4`, `4` | Farbe, Versatz, Weichheit des Schattens | `--tile-shadow-color`, `--tile-shadow-offset`, `--tile-shadow-blur` |
| `grayscale` | `false` | Graustufen | `--grayscale` |
| `corners`, `cornerRadius` | `Square`, `12` | `Square`, `Rounded`, `Cut` und deren Größe | `--corners`, `--corner-radius` |

## `watermark`

| Feld | Standard | Bedeutung | Option |
|---|---|---|---|
| `text` | `null` | Text-Wasserzeichen | `--watermark` |
| `imagePath` | `null` | Bild-Wasserzeichen (Vorrang vor `text`); relativ wie oben | `--watermark-image` |
| `position` | `BottomRight` | `TopLeft`, `TopRight`, `BottomLeft`, `BottomRight`, `Center`, `Tiled` | `--watermark-pos` |
| `size` | `0.2` | Breite relativ zum Sheet bzw. zur Kachel | `--watermark-size` |
| `opacity` | `0.5` | Deckkraft | `--watermark-opacity` |
| `color` | `#FFFFFFFF` | Farbe des Texts | `--watermark-color` |
| `perTile` | `false` | in jeder Kachel statt einmal | `--watermark-per-tile` |

## `timestamp`

| Feld | Standard | Bedeutung | Option |
|---|---|---|---|
| `enabled` | `true` | Zeitstempel anzeigen | `--timestamps` |
| `position` | `BottomRight` | `TopLeft`, `TopRight`, `BottomLeft`, `BottomRight`, `Center` | `--timestamp-pos` |
| `fontSize`, `bold` | `16`, `true` | Schriftgröße (pt), fett | `--timestamp-size`, `--timestamp-bold` |
| `color`, `opacity` | `#FFFFFFFF`, `1` | Farbe und Deckkraft | `--timestamp-color`, `--timestamp-opacity` |
| `outlineColor`, `outlineWidth` | `#FF000000`, `3` | Kontur | `--outline-color`, `--outline-width` |
| `shadow`, `shadowColor`, `shadowOffset` | `true`, `#AA000000`, `2` | Schatten | `--shadow`, `--shadow-color`, `--shadow-offset` |
| `padding` | `6` | Abstand vom Kachelrand | `--timestamp-padding` |
| `showMilliseconds` | `false` | mit Millisekunden | `--milliseconds` |

## `header`

| Feld | Standard | Bedeutung | Option |
|---|---|---|---|
| `enabled` | `true` | Info-Kopf anzeigen | `--header` |
| `position` | `TopLeft` | `TopLeft`, `TopRight`, `BottomLeft`, `BottomRight` | `--header-pos` |
| `template` | `null` | Zeilen mit [Platzhaltern](../README.md#platzhalter), `\n` trennt Zeilen; `null` = eingebaute Vorlage | `--header-template` |
| `fontSize`, `titleFontSize` | `16`, `22` | Schriftgrößen der Zeilen und der Titelzeile | `--header-size`, `--title-size` |
| `color`, `titleColor` | `#FFE6E6E6`, `#FFFFFFFF` | Farben | `--header-color`, `--title-color` |
| `lineSpacing` | `1.25` | Zeilenabstand | `--line-spacing` |
| `comment` | `null` | eigener Text | `--comment` |

## `output`

| Feld | Standard | Bedeutung | Option |
|---|---|---|---|
| `format`, `quality` | `Jpeg`, `90` | `Jpeg`, `Png`, `Webp`; Qualität 1–100 | `-f`, `-q` |
| `location` | `NextToVideo` | `NextToVideo`, `OutputFolder`, `SubfolderNextToVideo` | `--next-to-video`, `-o`, `--subfolder` |
| `folder` | `null` | Ausgabeordner bei `OutputFolder` | `-o` |
| `keepFolderStructure` | `false` | Ordnerstruktur nachbilden | `--keep-structure` |
| `subfolderName` | `thumbnails` | Unterordner bei `SubfolderNextToVideo` | `--subfolder` |
| `fileNamePattern` | `{name}` | Dateiname ohne Endung | `--pattern` |
| `overwrite` | `Overwrite` | `Skip`, `Overwrite`, `Rename` | `--overwrite` |
| `saveFrames`, `framesFolderPattern` | `false`, `{name}_frames` | Einzelbilder und ihr Ordner | `--frames`, `--frames-folder` |
| `webVtt`, `webVttTileWidth` | `false`, `160` | WebVTT-Vorschaubilder | `--webvtt`, `--webvtt-width` |
| `sidecar` | `false` | Begleitdatei [`.vtsheet`](vtsheet.md) | `--sidecar` |

## `animation`

| Feld | Standard | Bedeutung | Option |
|---|---|---|---|
| `enabled` | `false` | animiertes Sheet | `-a`, `--still` |
| `format` | `Mp4` | `Mp4`, `H265`, `Webm`, `Ogv`, `Gif`, `Webp`, `Apng` | `-a` |
| `clipSeconds`, `framesPerSecond` | `3`, `10` | Länge der Ausschnitte, Bilder pro Sekunde | `--clip-seconds`, `--fps` |
| `audio` | `false` | Ton der größten Kachel | `--audio` |

## `trailer`

| Feld | Standard | Bedeutung | Option |
|---|---|---|---|
| `enabled`, `only` | `false`, `false` | Vorschau-Clip schreiben; nur den Clip, kein Sheet | `--trailer`, `--trailer-only` |
| `format` | `Mp4` | wie bei `animation` | `--trailer` |
| `clips`, `clipSeconds`, `crossfadeSeconds` | `12`, `2.5`, `0.5` | Anzahl, Länge, Überblendung | `--trailer-clips`, `--trailer-seconds`, `--trailer-fade` |
| `size`, `framesPerSecond` | `640`, `24` | längere Bildseite, Bilder pro Sekunde | `--trailer-size`, `--trailer-fps` |
| `timestamps`, `audio` | `true`, `false` | Zeit in der Ecke, Ton | `--trailer-timestamps`, `--trailer-audio` |

## `template` (Spezial-Raster)

Ein Raster aus `columns` × `rows` Feldern im Seitenverhältnis des Videos; jede Kachel hat ihr linkes oberes Feld (`x`, `y`, ab 0) und eine Größe in Feldern (`size`, Standard 1, immer quadratisch). Felder ohne Kachel bleiben frei. Dasselbe Format haben `.vtgrid`-Dateien aus dem Raster-Editor.

```json
"template": {
  "name": "Breitbild", "columns": 3, "rows": 3,
  "tiles": [ { "x": 0, "y": 0, "size": 2 }, { "x": 2, "y": 0 }, { "x": 2, "y": 1 },
             { "x": 0, "y": 2 }, { "x": 1, "y": 2 }, { "x": 2, "y": 2 } ]
}
```

Das vollständige Standard-Preset gibt `vthumb presets show Standard` aus.
