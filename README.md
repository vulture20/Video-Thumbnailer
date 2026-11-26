# Video-Thumbnailer

Freier, plattformübergreifender Generator für Video-Kontaktbögen (Thumbnail-Sheets) für **Windows, Linux und macOS** – mit grafischer Oberfläche und Kommandozeile.
Free, cross-platform video contact sheet generator with GUI and command line.

![Beispiel-Sheet](docs/images/example-sheet.jpg)

## Funktionen (v1)

- Einzelne Dateien oder ganze Ordner (rekursiv) per Dialog, Drag & Drop oder Kommandozeile
- Alle Formate, die ffmpeg lesen kann (MP4, MKV, AVI, MOV, WebM, WMV, TS …), inkl. 10-Bit/HDR, AV1, gedrehte und anamorphe Videos
- Raster aus Spalten × Zeilen, Breite, Abstand, Rand; automatische Kachelhöhe, Anpassung für Hochkant-Videos
- Reihenfolge zeilenweise, spaltenweise oder zufällig
- Zeitstempel in jeder Kachel (5 Positionen, Farbe, Deckkraft, Kontur, Schatten)
- Info-Kopf mit frei konfigurierbaren Video-Infos (Name, Größe, Dauer, Auflösung, Codecs, Bitrate, FPS, Audio …)
- Ausgabe als JPG, PNG oder WebP; optional jede Aufnahme als Einzelbild
- Warteschlange mit Pause, Abbruch, Fortschritt und paralleler Verarbeitung über alle CPU-Kerne
- Ausgabe in einen Ordner (optional mit Ordnerstruktur), neben das Video oder in einen Unterordner; Dateinamen-Muster
- Presets als offene JSON-Dateien, 4 mitgelieferte Basis-Presets
- ffmpeg wird nicht mitgeliefert, sondern nach Rückfrage automatisch geladen (mit SHA-256-Prüfung und Update-Funktion). Ein vorhandenes ffmpeg wird automatisch gefunden.

![Oberfläche](docs/images/gui.png)

## Download und Start

Fertige Programme gibt es unter [Releases](https://github.com/vulture20/Video-Thumbnailer/releases), je Plattform als ZIP mit zwei Einzeldateien:

- `VideoThumbnailer` (bzw. `.exe`) – grafische Oberfläche
- `vthumb` (bzw. `.exe`) – Kommandozeile

Es muss kein .NET installiert sein. Unter macOS ist das Programm noch nicht signiert: beim ersten Start per Rechtsklick → „Öffnen“ bestätigen.

**Portabler Modus:** Liegt eine leere Datei `portable.txt` neben der Programmdatei, werden Einstellungen, Presets und ffmpeg im Ordner `data` daneben gespeichert.

## Kommandozeile

```text
vthumb [Optionen] <Datei|Ordner>...
vthumb presets list|show|import|export|delete
vthumb ffmpeg status|check|install|update|uninstall
```

Beispiele:

```bash
# Alle Videos eines Ordners (rekursiv), Sheets neben die Videos
vthumb ~/Videos

# Preset "Compact", 6 Spalten, WebP, alles in einen Ordner mit gleicher Unterordnerstruktur
vthumb -p Compact --columns 6 -f webp -o ~/Sheets --keep-structure ~/Videos

# Nur anzeigen, was passieren würde
vthumb --dry-run ~/Videos

# Eigene Einstellungen als Preset speichern
vthumb --columns 5 --rows 6 --no-header --save-preset "5x6 ohne Kopf"
```

Ohne `--preset` werden die zuletzt in der Oberfläche benutzten Einstellungen verwendet. Alle Optionen: `vthumb --help`.
Exit-Codes: 0 ok, 1 einzelne Videos fehlgeschlagen, 2 Aufruffehler, 3 ffmpeg fehlt, 130 abgebrochen.

### Platzhalter im Info-Kopf

`{filename}` `{name}` `{ext}` `{folder}` `{path}` `{title}` `{container}` `{size}` `{size:B|KB|MB|GB|TB}` `{duration}` `{bitrate}`
`{width}` `{height}` `{resolution}` `{vcodec}` `{vprofile}` `{fps}` `{pixfmt}` `{hdr}` `{vbitrate}` `{rotation}`
`{audio}` `{acodec}` `{samplerate}` `{channels}` `{channellayout}` `{abitrate}` `{alang}` `{audiotracks}`

Die erste Zeile wird fett und größer dargestellt.

### Platzhalter im Dateinamen

`{name}` `{ext}` `{preset}` `{cols}` `{rows}` `{date}` `{time}`

## ffmpeg

| System | Quelle |
|---|---|
| Windows x64/arm64, Linux x64/arm64 | [BtbN/FFmpeg-Builds](https://github.com/BtbN/FFmpeg-Builds) (statischer GPL-Build des aktuellen Release-Zweigs) |
| macOS arm64/Intel | [ffmpeg.martin-riedl.de](https://ffmpeg.martin-riedl.de) |

Reihenfolge der Suche: eigener Ordner (Einstellungen bzw. `--ffmpeg`) → heruntergeladenes ffmpeg → `PATH`.
Details zu Speicherorten, Prüfsummen und Updates: [docs/DECISIONS.md](docs/DECISIONS.md).

## Selbst bauen

Voraussetzung: [.NET 10 SDK](https://dotnet.microsoft.com/download).

```bash
dotnet build
dotnet test                                   # Integrationstests brauchen ffmpeg im PATH
dotnet run --project src/VideoThumbnailer.Gui
dotnet run --project src/VideoThumbnailer.Cli -- --help

# Einzeldatei für eine Plattform
dotnet publish src/VideoThumbnailer.Gui -c Release -r linux-x64 --self-contained \
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true
```

Aufbau:

- `src/VideoThumbnailer.Core` – ffmpeg-Verwaltung, Video-Infos, Layout, Rendering (SkiaSharp), Warteschlange, Presets
- `src/VideoThumbnailer.Cli` – Kommandozeile `vthumb`
- `src/VideoThumbnailer.Gui` – Oberfläche (Avalonia)
- `tests/VideoThumbnailer.Tests` – Unit-, Integrations- und GUI-Tests (headless)

Entscheidungen und offene Punkte: [docs/DECISIONS.md](docs/DECISIONS.md)

## Lizenz

[GNU Affero General Public License v3.0](LICENSE).
Die mitgelieferte Schrift DejaVu Sans steht unter einer eigenen freien Lizenz (`licenses/DejaVu-LICENSE.txt`).
ffmpeg ist nicht Bestandteil dieses Programms und wird separat unter seiner eigenen Lizenz (GPL) bezogen.
