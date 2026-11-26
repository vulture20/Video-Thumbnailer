# Entscheidungen & offene Punkte

Dieses Dokument wird bei jeder Änderung mitgeführt. Neue Entscheidungen bekommen ein Datum und eine kurze Begründung.
Die Feature-Spezifikation (mit Prioritäten P1–P3) liegt außerhalb des Repos; dieser Stand ist mit ihrem Abschnitt „Entscheidungen & offene Punkte“ abgeglichen.

## Entschieden

### Projekt
| Datum | Entscheidung | Begründung / Anmerkung |
|---|---|---|
| 26.11.2026 | Clean-Room-Neuentwicklung auf Basis von ffmpeg, eigene UI; kein Code, keine Assets und keine Dateiformate aus kommerziellen Produkten | Rechtssicherheit, eigene Gestaltung |
| 26.11.2026 | Öffentliches GitHub-Repo `vulture20/Video-Thumbnailer` | |
| 26.11.2026 | Lizenz AGPL-3.0 (`AGPL-3.0-only`) | |
| 26.11.2026 | Plattformen Windows, Linux, macOS ab v1 | |
| 26.11.2026 | Animierte Sheets (MP4/WebM/GIF …) erst nach v1 | MVP klein halten |
| 26.11.2026 | Kommandozeile ist Teil von v1 | |
| 26.11.2026 | Arbeit über Pull Requests | |

### Technik
| Datum | Entscheidung | Begründung / Anmerkung |
|---|---|---|
| 26.11.2026 | C# auf **.NET 10** (LTS bis Nov. 2028) | aktuelle LTS-Version |
| 26.11.2026 | Aufbau: `VideoThumbnailer.Core` (Bibliothek) + `vthumb` (CLI) + `VideoThumbnailer` (Avalonia-GUI); CLI und GUI teilen den Kern vollständig | eine Logik, zwei Oberflächen |
| 26.11.2026 | GUI mit **Avalonia 12.2**, Fluent-Theme, MVVM mit CommunityToolkit.Mvvm | plattformübergreifend |
| 26.11.2026 | Rendering mit **SkiaSharp 3.119** (gleiche Version wie Avalonia, damit keine Konflikte) | Text, Kontur, Schatten, JPG/PNG/WebP-Encoder in einem |
| 26.11.2026 | Mitgelieferte Schrift **DejaVu Sans** (Bitstream-Vera-Lizenz, frei weitergebbar) als Standard; eigene Systemschrift wählbar | gleiches Ergebnis auf allen Systemen, auch ohne installierte Schriften (Server/Headless) |
| 26.11.2026 | Frame-Extraktion: ein ffmpeg-Prozess pro Kachel mit schnellem Input-Seek (`-ss` vor `-i`), Skalierung direkt in ffmpeg auf Kachelgröße | einfach, robust, gut parallelisierbar |
| 26.11.2026 | Parallelität: globale Obergrenze für gleichzeitige ffmpeg/ffprobe-Prozesse (Standard = CPU-Kerne, max. 16) und getrennt dafür die Zahl parallel bearbeiteter Videos (Standard = Hälfte) | auch eine einzelne lange Datei nutzt alle Kerne |
| 26.11.2026 | HDR (PQ/HLG) wird per `zscale`+`tonemap` (Hable) auf SDR abgebildet; ohne zscale automatisch Rückfall auf normale Konvertierung | |
| 26.11.2026 | Fehlende Frames (z. B. am Dateiende) werden 2 s bzw. 10 s früher erneut versucht, sonst als graue „no frame“-Kachel gezeichnet; das Sheet wird trotzdem erzeugt | ein kaputter Frame soll keinen Batch stoppen |
| 26.11.2026 | Hochkant-Videos: Spalten und Zeilen werden getauscht, wenn Zeilen > Spalten (abschaltbar) | Umsetzung von „Anpassung für Hochkant-Videos“ |
| 26.11.2026 | Rotation (Display-Matrix) und anamorphe Pixel (SAR) werden für das Seitenverhältnis berücksichtigt | |
| 26.11.2026 | Dateien werden erst als `.part` geschrieben und dann umbenannt | keine halben Bilder bei Abbruch |
| 26.11.2026 | Presets als JSON (`*.vtpreset.json`, camelCase, Enums als Text, `schemaVersion`); 4 eingebaute Presets: Standard, Compact, Detailed, Web | offenes, lesbares Format |
| 26.11.2026 | Info-Kopf über eine Textvorlage mit Platzhaltern (`{filename}`, `{size:GB}`, `{duration}` …); erste Zeile fett und größer | frei konfigurierbare Felder |
| 26.11.2026 | CLI ohne `--preset` nutzt die zuletzt in der GUI verwendeten Einstellungen (sonst „Standard“) | wie in der Spezifikation |
| 26.11.2026 | CLI-Name `vthumb`, eigener kleiner Argument-Parser ohne Zusatzpaket | wenige Abhängigkeiten |
| 26.11.2026 | Speicherorte: Einstellungen/Presets unter `%APPDATA%\VideoThumbnailer`, `~/.config/video-thumbnailer`, `~/Library/Application Support/VideoThumbnailer`; ffmpeg unter `%LOCALAPPDATA%\VideoThumbnailer\ffmpeg`, `~/.local/share/video-thumbnailer/ffmpeg` bzw. macOS wie oben. Portabler Modus: Datei `portable.txt` neben der Programmdatei oder Umgebungsvariable `VIDEO_THUMBNAILER_HOME` | Standardpfade je Betriebssystem, USB-Stick-Betrieb möglich |
| 26.11.2026 | Auslieferung als selbstständige Einzeldatei je Plattform (win-x64, win-arm64, linux-x64, linux-arm64, osx-x64, osx-arm64), ohne vorinstalliertes .NET | „direkt überall lauffähig“ |
| 26.11.2026 | Code, Kommentare und Oberfläche auf Englisch, Projektdokumentation auf Deutsch | öffentliches Repo; Lokalisierung DE/EN ist P2 |

### ffmpeg-Bezug
| Datum | Entscheidung | Begründung / Anmerkung |
|---|---|---|
| 26.11.2026 | ffmpeg wird nicht mitgeliefert. Beim ersten Bedarf (GUI: beim ersten Start; CLI: beim ersten Lauf) wird kurz gefragt und dann automatisch heruntergeladen. CLI: `--yes` lädt ohne Frage, `vthumb ffmpeg install/update/check/status/uninstall` | wie beschlossen |
| 26.11.2026 | Suchreihenfolge: eigener Ordner aus den Einstellungen → von uns heruntergeladenes ffmpeg → `PATH` (plus Homebrew-/Standardpfade) | vorhandene Installationen werden genutzt, nichts doppelt geladen |
| 26.11.2026 | Quelle Windows/Linux: **BtbN/FFmpeg-Builds** (GitHub), Release „latest“, statischer **GPL**-Build des neuesten Release-Zweigs (z. B. n9.0), nicht „master“ | stabil, statisch gelinkt, x64 und arm64 |
| 26.11.2026 | Quelle macOS: **ffmpeg.martin-riedl.de** (arm64/amd64, signiert und notarisiert), ffmpeg und ffprobe als getrennte ZIPs | BtbN baut kein macOS |
| 26.11.2026 | Prüfsumme: SHA-256 aus der vom Anbieter veröffentlichten Datei (`checksums.sha256` bzw. `*.sha256`); ohne gültige Prüfsumme wird nicht installiert | schützt gegen beschädigte Downloads |
| 26.11.2026 | Installation atomar: Entpacken in einen Arbeitsordner, Testlauf `ffmpeg -version`, dann Austausch des `bin`-Ordners; bei Fehlern bleibt die alte Version erhalten. `manifest.json` merkt sich Quelle, Version und Prüfsumme | Update kann nichts kaputt machen |
| 26.11.2026 | Update-Erkennung über die Prüfsumme (neuer Build = neue Prüfsumme). GUI prüft höchstens einmal pro Woche im Hintergrund (abschaltbar) und bietet das Update an; CLI über `vthumb ffmpeg check/update` | auch tägliche „latest“-Builds werden erkannt, ohne GitHub-API-Limits |

## Offen

| Seit | Punkt | Stand |
|---|---|---|
| 26.11.2026 | MVP-Umfang final bestätigen (aktuell alle P1-Punkte der Spezifikation) | v1-Umfang siehe unten |
| 26.11.2026 | macOS-Quelle (martin-riedl.de): URL-Schema und `.sha256`-Dateien konnten in der Entwicklungsumgebung nicht live geprüft werden | auf einem Mac mit `vthumb ffmpeg install` testen; Fallback: Homebrew-ffmpeg wird automatisch gefunden |
| 26.11.2026 | Prüfsumme kommt vom selben Server wie der Download; schützt nicht gegen einen kompromittierten Anbieter. Stärker wären Signaturen (GPG) oder fest hinterlegte Hashes | bewusst akzeptiert für v1 |
| 26.11.2026 | Downloadgröße: Linux-Build von BtbN ca. 150 MB (tar.xz), Windows ähnlich. Kleinere Varianten (z. B. „gpl-shared“ oder LGPL) prüfen? | |
| 26.11.2026 | Code-Signierung (Windows) und Notarisierung/App-Bundle (macOS) fehlen; macOS zeigt beim ersten Start eine Gatekeeper-Warnung | Installer/Pakete (MSI, .app/.dmg, AppImage/Flatpak) später |
| 26.11.2026 | HDR-Tonemapping mit echten HDR10/HLG-Videos prüfen (Testvideos ohne korrekte Farbmetadaten) | |
| 26.11.2026 | Oberfläche aktuell nur Englisch; Deutsch/Englisch-Umschaltung ist P2 | |
| 26.11.2026 | GUI-Einstellungen für Kontur-/Schattenfarbe, Kopf-Farben und Millisekunden im Zeitstempel fehlen (im Preset-JSON und teils per CLI möglich) | Optionsdialog ist P2 |
| 26.11.2026 | Live-Vorschau der Einstellungen (P2) | |

## Umfang v1 (Stand dieses PRs)

Umgesetzt (P1):
- Dateien und Ordner hinzufügen (Dialog, Drag & Drop, Kommandozeile), rekursiv umschaltbar
- alle Formate, die ffmpeg lesen kann, inkl. 10-Bit/HDR, AV1, Rotation, anamorphe Videos
- Raster Spalten × Zeilen, Gesamt- oder Kachelbreite, Abstand, Rand, automatische Kachelhöhe, Hochkant-Anpassung
- Reihenfolge zeilenweise, spaltenweise, zufällig; Anfang/Ende überspringen (%)
- Zeitstempel: 5 Positionen, Schriftgröße, Farbe, Deckkraft, Kontur, Schatten
- Info-Kopf: 4 Positionen, frei konfigurierbare Felder über Platzhalter
- Ausgabe JPG/PNG/WebP mit Qualität; jede Aufnahme zusätzlich als Einzelbild
- Warteschlange: sortieren, Auswahl entfernen, Erledigte/alle leeren, Fehlgeschlagene wiederholen
- Start, Pause/Fortsetzen, Abbrechen, Fortschritt und Statistik
- Parallelverarbeitung über alle CPU-Kerne
- Ausgabeordner / neben dem Video / Unterordner neben dem Video; Ordnerstruktur beibehalten; Dateinamen-Muster; Verhalten bei vorhandenen Dateien (überspringen, überschreiben, umbenennen)
- Presets speichern, laden, löschen, importieren, exportieren; 4 Basis-Presets; Preset-Datei als Programmargument öffnet sie
- ffmpeg-Download mit Nachfrage, SHA-256-Prüfung und Update-Mechanismus
- CLI `vthumb` mit allen Einstellungen als Optionen
