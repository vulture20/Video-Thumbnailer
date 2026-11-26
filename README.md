<p align="center"><img src="docs/images/banner.png" alt="Video-Thumbnailer – contact sheets from your videos" width="100%"></p>

# Video-Thumbnailer

Freier, plattformübergreifender Generator für Video-Kontaktbögen (Thumbnail-Sheets) für **Windows, Linux und macOS** – mit grafischer Oberfläche und Kommandozeile.
Free, cross-platform video contact sheet generator with GUI and command line.

![Beispiel-Sheet](docs/images/example-sheet.jpg)

## Funktionen

- Einzelne Dateien oder ganze Ordner (rekursiv) per Dialog, Drag & Drop oder Kommandozeile
- Videos auf einem Webserver per HTTP(S)-Adresse, ohne sie vorher herunterzuladen: ffmpeg lädt nur die Stellen um die Aufnahmezeitpunkte (Knopf mit Weltkugel, Adresse aus dem Browser hineinziehen oder als Argument in der Kommandozeile)
- Alle Formate, die ffmpeg lesen kann (MP4, MKV, AVI, MOV, WebM, WMV, TS …), inkl. 10-Bit/HDR, AV1, gedrehte und anamorphe Videos
- Raster aus Spalten × Zeilen, Breite, Abstand, Rand; automatische Kachelhöhe, Anpassung für Hochkant-Videos
- Spezial-Raster mit großen und kleinen Kacheln (z. B. ein großes Bild oben oder in der Mitte), eigene Vorlagen als JSON
- Bildgröße fest, so breit wie der Bildschirm oder so groß, dass das Sheet ohne Scrollen ganz auf den Bildschirm passt
- Reihenfolge zeilenweise, spaltenweise oder zufällig
- Schwarze oder einfarbige Bilder (Abblende, Schnitt) werden automatisch durch ein gutes Bild kurz davor oder danach ersetzt (abschaltbar, Suchbereich einstellbar)
- Aufnahmezeitpunkte von Hand: Kachel in der Vorschau anklicken und den Zeitpunkt per Schieberegler, Eingabe oder bildgenau wählen (bei animierten Sheets auch die Länge des Ausschnitts); in der Kommandozeile mit `--times`
- Zeitstempel in jeder Kachel (5 Positionen, Farbe, Deckkraft, Kontur- und Schattenfarbe, optional mit Millisekunden)
- Hintergrundfarbe oder Hintergrundbild (füllen, strecken, kacheln, zentriert, mit Deckkraft)
- Kachel-Effekte: Rahmen, Schlagschatten, Graustufen, abgerundete oder abgeschnittene Ecken, Rahmen um das ganze Sheet
- Wasserzeichen als Text oder Bild (Ecke, Mitte oder wiederholt, mit Deckkraft), über dem Sheet oder in jeder Kachel
- Info-Kopf mit frei konfigurierbaren Video-Infos (Name, Größe, Dauer, Auflösung, Codecs, Bitrate, FPS, Audio …), eigene Titel- und Textfarbe, dazu ein eigener Kommentartext
- Ausgabe als JPG, PNG oder WebP; optional jede Aufnahme als Einzelbild
- Animierte Sheets als MP4 (H.264 oder H.265), WebM, OGV, GIF, animiertes WebP oder APNG: jede Kachel spielt einen kurzen Ausschnitt ab ihrem Zeitpunkt ab, mit mitlaufendem Zeitstempel; Videoformate optional mit dem Ton einer Kachel
- Warteschlange mit Pause, Abbruch, Fortschritt und paralleler Verarbeitung über alle CPU-Kerne; klappt in schmalen Fenstern zu einer Leiste ein
- Ausgabe in einen Ordner (optional mit Ordnerstruktur), neben das Video oder in einen Unterordner; Dateinamen-Muster
- Presets als offene JSON-Dateien, 4 mitgelieferte Basis-Presets
- Oberfläche, Kommandozeile und Texte im Sheet auf Deutsch und Englisch (folgt der Systemsprache, umschaltbar)
- Live-Vorschau des Kontaktbogens beim Einstellen, mit echten Bildern und Videodaten des gewählten Videos: Raster, Info-Kopf, Zeitstempel, Farben und Bildgröße wie im Ergebnis (abschaltbar)
- Einstellungen im Modus „Einfach“ (nur das Wichtigste) oder „Erweitert“ (alles); Darstellung hell, dunkel oder wie das System
- ffmpeg wird nicht mitgeliefert, sondern nach Rückfrage automatisch geladen: ein fest hinterlegter Build mit SHA-256-Prüfsumme aus diesem Repo, mit Update-Funktion. Ein vorhandenes ffmpeg wird automatisch gefunden.
- Installer für Windows, AppImage für Linux, DMG für macOS sowie portable ZIPs

![Oberfläche, Modus „Einfach“](docs/images/gui.png)

![Modus „Erweitert“ in dunkler Darstellung, mit Farbfeldern und Farbwähler am Farbmuster](docs/images/gui-dark.png)

![Schmales Fenster: die Warteschlange ist zu einer Leiste eingeklappt](docs/images/gui-narrow.png)

## Download und Start

Fertige Programme gibt es unter [Releases](https://github.com/vulture20/Video-Thumbnailer/releases). Es muss kein .NET installiert sein.

| System | Paket | Hinweis |
|---|---|---|
| Windows | `…-win-x64-setup.exe` (bzw. `arm64`) | Installer mit Startmenü-Eintrag, ohne Administratorrechte. Nicht signiert: SmartScreen warnt, „Weitere Informationen“ → „Trotzdem ausführen“ |
| Linux | `…-linux-x64.AppImage` (bzw. `arm64`) | ausführbar machen und starten (`chmod +x …AppImage`); Kommandozeile: `./…AppImage vthumb --help` |
| macOS | `…-osx-arm64.dmg` (Apple Silicon) bzw. `osx-x64` (Intel) | App in „Programme“ ziehen. Nicht notarisiert: beim ersten Start Rechtsklick → „Öffnen“ bestätigen; die Kommandozeile liegt in `Video-Thumbnailer.app/Contents/MacOS/vthumb` |
| alle | `…-<system>.zip` | portable Fassung: `VideoThumbnailer` (Oberfläche) und `vthumb` (Kommandozeile) als Einzeldateien, dazu `LICENSE`, `README.md` und `licenses/` (Lizenz der Schrift) |

Die Prüfsummen aller Dateien stehen in `checksums.sha256`.

**Portabler Modus:** Liegt eine leere Datei `portable.txt` neben der Programmdatei, werden Einstellungen, Presets und ffmpeg im Ordner `data` daneben gespeichert.

## Kommandozeile

```text
vthumb [Optionen] <Datei|Ordner|URL>...
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

# Animiertes Sheet: jede Kachel spielt 3 Sekunden ab, als MP4 (oder h265, webm, ogv, gif, webp, apng)
vthumb -a mp4 --clip-seconds 3 --fps 10 film.mkv     # -> film.sheet.mp4

# Animiertes WebP bzw. H.265-Video mit dem Ton der größten Kachel
vthumb -a webp film.mkv                              # -> film.sheet.webp
vthumb -a h265 --audio --layout big-center film.mkv

# Zeitstempel gelb mit roter Kontur und Millisekunden, grüner Titel im Info-Kopf
vthumb --timestamp-color "#FFFF00" --outline-color "#FF0000" --milliseconds --title-color "#00FF00" film.mkv

# Video direkt vom Webserver (nur die nötigen Teile werden geladen), Sheet nach ~/Sheets
vthumb -o ~/Sheets "https://example.org/videos/vortrag.mp4"

# Zeitpunkte von Hand: Kachel 1 bei 1:00, Kachel 2 automatisch, Kachel 3 bei 5:30 (Ausschnitt 2 s lang)
vthumb --columns 3 --rows 1 --times "1:00,-,5:30+2" film.mkv

# Schwarze Bilder nicht ersetzen bzw. weiter suchen (Standard: an, ±2 s)
vthumb --no-skip-black film.mkv
vthumb --black-search 5 film.mkv

# Großes Bild in der Mitte, Sheet so groß, dass es ganz auf einen 2560×1440-Bildschirm passt
vthumb --layout big-center --fit-screen both --screen 2560x1440 film.mkv

# Hintergrundbild, abgedunkelt (Deckkraft 40 % über dunklem Hintergrund)
vthumb --background-image ~/Bilder/kino.jpg --background-opacity 40 film.mkv

# Abgerundete Kacheln mit hellem Rahmen und Schatten, Graustufen, Kommentar und Wasserzeichen
vthumb --gap 12 --corners rounded --tile-border 2 --tile-border-color "#E6E6E6" --tile-shadow --grayscale \
       --comment "Urlaub 2026" --watermark "© 2026" --watermark-opacity 0.4 film.mkv

# Logo als Wasserzeichen in jeder Kachel, oben links, ein Zehntel der Kachelbreite
vthumb --watermark-image logo.png --watermark-per-tile --watermark-pos top-left --watermark-size 0.1 film.mkv

# Eigene Einstellungen als Preset speichern
vthumb --columns 5 --rows 6 --no-header --save-preset "5x6 ohne Kopf"
```

Ohne `--preset` werden die zuletzt in der Oberfläche benutzten Einstellungen verwendet. Jede Einstellung eines Presets lässt sich auch per Option ändern (Raster, Zeitstempel samt Farben, Kontur, Schatten und Millisekunden, Info-Kopf samt Farben und Schriftgrößen, Ausgabe, Animation). Farben als `#RRGGBB` oder `#AARRGGBB` (AA = Deckkraft). Alle Optionen: `vthumb --help`.

**Videos per URL:** `http://` und `https://` werden direkt von ffmpeg gelesen. Schnell geht das, wenn der Server Range-Anfragen unterstützt (übliche Webserver und CDNs tun das); sonst muss ffmpeg bis zur Stelle lesen, und Formate mit Index am Dateiende (manche MP4) lassen sich dann gar nicht öffnen. Nach 30 s ohne Daten wird abgebrochen. Da es „neben dem Video“ nicht gibt, landen die Sheets im Ausgabeordner, ohne einen solchen in der Oberfläche im Bilder-Ordner des Benutzers und in der Kommandozeile im aktuellen Ordner. Name, Endung und `{folder}`/`{path}` stammen aus der Adresse; Benutzername, Passwort und Query-Parameter (oft Zugangsschlüssel) erscheinen nicht im Sheet.

**Zeitpunkte von Hand:** `--times` nimmt eine Liste je Kachel in Zeitfolge (bei „zufällig“ also nicht nach Position): Sekunden, `mm:ss` oder `hh:mm:ss` (mit Nachkommastellen), `-` für automatisch, `+s` für die Länge des Ausschnitts bei animierten Sheets. Die Liste gilt für alle angegebenen Videos und gehört nicht zum Preset. Von Hand gewählte Zeitpunkte werden nie als „schwarz“ ersetzt. In der Oberfläche gelten sie für das Video in der Warteschlange und bleiben bis zum Schließen des Programms erhalten.

**Spezial-Raster:** Eingebaut sind `big-top` (großes Bild oben, 21 Bilder), `big-center` (großes Bild in der Mitte, 13), `two-big` (zwei große oben, 14) und `big-first` (erstes Bild groß oben links, 17). Eigene Vorlagen stehen im Preset unter `template` oder in einer eigenen JSON-Datei für `--layout`: ein Raster aus `columns` × `rows` Feldern im Seitenverhältnis des Videos und darauf die Kacheln mit linkem oberen Feld und Größe in Feldern (immer quadratisch, damit kein Bild verzerrt wird); Felder ohne Kachel bleiben frei. Die Zeitpunkte laufen nach dem linken oberen Feld jeder Kachel, zeilenweise oder spaltenweise.

```json
{ "name": "Breitbild", "columns": 3, "rows": 3,
  "tiles": [ { "x": 0, "y": 0, "size": 2 }, { "x": 2, "y": 0 }, { "x": 2, "y": 1 },
             { "x": 0, "y": 2 }, { "x": 1, "y": 2 }, { "x": 2, "y": 2 } ] }
```

**An Bildschirm anpassen:** In der Oberfläche zählt der Bildschirm, auf dem das Fenster ist (nutzbare Fläche ohne Taskleiste, in physischen Pixeln). Die Kommandozeile nimmt `--screen BxH`, sonst den zuletzt von der Oberfläche gesehenen Bildschirm, sonst die feste Breite des Presets.

**Hintergrundbild und Wasserzeichen-Bild:** Relative Pfade in einer Preset-Datei gelten relativ zu deren Ordner; beim Export wird ein Bild im selben Ordner relativ eingetragen, sodass Preset und Bild zusammen weitergegeben werden können.

**Kachel-Effekte:** Der Rahmen wird innen an der Kachel gezeichnet, die Bildgröße ändert sich also nicht. Der Schatten fällt in den Abstand zwischen den Kacheln; ohne Abstand ist er kaum zu sehen (Faustregel: Abstand etwa so groß wie Versatz plus Weichheit, Standard je 4 px). Der Sheet-Rahmen liegt im Rand. Einzelbilder (`--frames`) bleiben unverändert.

**Wasserzeichen:** Ein Bild hat Vorrang vor dem Text. Die Größe ist die Breite des Wasserzeichens im Verhältnis zur Sheet-Breite (mit `--watermark-per-tile` zur Kachelbreite), höchstens so hoch wie die Fläche. Es liegt über dem Info-Kopf, in Kacheln unter dem Zeitstempel. Einzelbilder bekommen kein Wasserzeichen.

**Animierte Formate:** MP4 (H.264) läuft überall; H.265 ist kleiner, spielt aber nicht jeder Browser ab. WebP und GIF eignen sich für Webseiten und Foren, APNG ist verlustfrei und entsprechend groß, OGV (Theora) gibt es für ältere freie Umgebungen. Mit `--audio` (bzw. „Mit Ton“) läuft der Ton der größten Kachel während ihres Ausschnitts, bei gleich großen Kacheln der der ersten; GIF, WebP und APNG können keinen Ton enthalten.

Sprache: `--lang de|en|auto` (oder Umgebungsvariable `VTHUMB_LANG`) für Hilfe und Meldungen; ohne Angabe gilt die in der Oberfläche gewählte Sprache, sonst die Systemsprache. `--sheet-lang de|en|auto` legt die Sprache der Texte im Sheet fest (z. B. „Größe:“/„Size:“, Dezimalkomma).
Exit-Codes: 0 ok, 1 einzelne Videos fehlgeschlagen, 2 Aufruffehler, 3 ffmpeg fehlt, 130 abgebrochen.

### Platzhalter im Info-Kopf

`{filename}` `{name}` `{ext}` `{folder}` `{path}` `{title}` `{container}` `{size}` `{size:B|KB|MB|GB|TB}` `{duration}` `{bitrate}`
`{width}` `{height}` `{resolution}` `{vcodec}` `{vprofile}` `{fps}` `{pixfmt}` `{hdr}` `{vbitrate}` `{rotation}`
`{audio}` `{acodec}` `{samplerate}` `{channels}` `{channellayout}` `{abitrate}` `{alang}` `{audiotracks}` `{comment}`

Die erste Zeile wird fett und größer dargestellt. `{comment}` ist der eigene Kommentartext; steht er nicht in der Vorlage, kommt der Kommentar als letzte Zeile(n) dazu.

### Platzhalter im Dateinamen

`{name}` `{ext}` `{preset}` `{cols}` `{rows}` `{date}` `{time}`

## ffmpeg

| System | Quelle |
|---|---|
| Windows x64 | [gyan.dev](https://www.gyan.dev/ffmpeg/builds/) (GPL-„full_build-shared“, über [GyanD/codexffmpeg](https://github.com/GyanD/codexffmpeg/releases)) |
| Linux x64/arm64, macOS arm64/Intel | [ffmpeg.martin-riedl.de](https://ffmpeg.martin-riedl.de) (macOS-Builds signiert und notarisiert) |
| Windows arm64 | [BtbN/FFmpeg-Builds](https://github.com/BtbN/FFmpeg-Builds) (neuester Build, siehe unten) |

Installiert wird ein **fest hinterlegter** ffmpeg-Build aus [ffmpeg-pins.json](src/VideoThumbnailer.Core/Ffmpeg/ffmpeg-pins.json): Adresse und SHA-256-Prüfsumme stehen in diesem Repo, nicht auf dem Download-Server. Unter macOS wird zusätzlich die Apple-Signatur des Herausgebers geprüft. Die App liest die Liste beim Update-Check von GitHub (ohne Verbindung gilt die eingebaute Liste); ein wöchentlicher Workflow trägt neuere Builds dort ein, nachdem er sie heruntergeladen und geprüft hat. Für ein neues ffmpeg braucht es also keine neue App-Version. Unter Windows arm64 gibt es keinen dauerhaft abrufbaren Build; dort wird wie bisher der neueste Build gegen die Prüfsumme des Anbieters geprüft. `vthumb ffmpeg install --latest` macht das auf Wunsch auch auf den anderen Systemen.

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
- `assets/branding` – Icon und Banner (SVG-Quellen, Build-Skript `build.py`)
- `packaging/windows`, `packaging/linux`, `packaging/macos` – Installer (Inno Setup), AppImage (mit `.desktop`-Datei und hicolor-Icons) und DMG
- `tools/ffmpeg-pins.py` – erzeugt die Liste der fest hinterlegten ffmpeg-Builds

Entscheidungen und offene Punkte: [docs/DECISIONS.md](docs/DECISIONS.md) · Änderungen: [CHANGELOG.md](CHANGELOG.md) · Ideen für später: [Issues](https://github.com/vulture20/Video-Thumbnailer/issues)

## Lizenz

Copyright (C) 2026 Thorsten Schröpel

[GNU Affero General Public License v3.0](LICENSE).
Die mitgelieferte Schrift DejaVu Sans steht unter einer eigenen freien Lizenz (`licenses/DejaVu-LICENSE.txt`).
ffmpeg ist nicht Bestandteil dieses Programms und wird separat unter seiner eigenen Lizenz (GPL) bezogen.
