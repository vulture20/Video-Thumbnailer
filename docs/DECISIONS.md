# Entscheidungen & offene Punkte

Dieses Dokument wird bei jeder Änderung mitgeführt. Neue Entscheidungen bekommen ein Datum und eine kurze Begründung.
Die Feature-Spezifikation (mit Prioritäten P1–P3) liegt außerhalb des Repos; dieser Stand ist mit ihrem Abschnitt „Entscheidungen & offene Punkte“ abgeglichen (zuletzt nach v0.5.0).
Überholte Entscheidungen bleiben stehen und sind mit **Überholt:** markiert. Spätere Ideen ohne Entscheidung stehen als [Issues](https://github.com/vulture20/Video-Thumbnailer/issues) im Repo (siehe „Spätere Ideen“ unten).

## Entschieden

### Projekt
| Datum | Entscheidung | Begründung / Anmerkung |
|---|---|---|
| 26.11.2026 | Clean-Room-Neuentwicklung auf Basis von ffmpeg, eigene UI; kein Code, keine Assets und keine Dateiformate aus kommerziellen Produkten | Rechtssicherheit, eigene Gestaltung |
| 26.11.2026 | Öffentliches GitHub-Repo `vulture20/Video-Thumbnailer` | |
| 26.11.2026 | Lizenz AGPL-3.0 (`AGPL-3.0-only`) | |
| 26.11.2026 | Rechteinhaber ist Thorsten Schröpel: `Copyright (C) 2026 Thorsten Schröpel`, ebenso Autor/Herausgeber (`Authors`, `Company`) in den Programmdateien | Wunsch von Vulli; zeigt sich in den Datei-Eigenschaften der EXE |
| 26.11.2026 | Plattformen Windows, Linux, macOS ab v1 | |
| 26.11.2026 | Animierte Sheets (MP4/WebM/GIF …) erst nach v1 | MVP klein halten; umgesetzt nach v0.3.0, siehe „Animierte Sheets“ |
| 26.11.2026 | Kommandozeile ist Teil von v1 | |
| 26.11.2026 | Arbeit über Pull Requests | |
| 26.11.2026 | MVP-Umfang bestätigt: alle P1-Punkte der Spezifikation, veröffentlicht als v0.1.0 und unter Windows getestet | von Vulli bestätigt |
| 26.11.2026 | Release v0.2.0 mit #3–#8 (Icon, CHANGELOG, Deutsch/Englisch in GUI, CLI und Sheet, Einfach/Erweitert, Dark-Mode, Leuchttisch-Oberfläche mit Live-Vorschau). Minor-Version, da neue Funktionen ohne Bruch von Presets/CLI | Wunsch von Vulli |
| 26.11.2026 | Release-Dateien heißen `Video-Thumbnailer-<Version>-<Plattform>.zip` (z. B. `Video-Thumbnailer-0.3.0-win-x64.zip`), der Ordner im ZIP ebenso; `checksums.sha256` bleibt ohne Version. Gilt ab dem nächsten Release | Wunsch von Vulli: heruntergeladene Dateien sind eindeutig zuzuordnen |
| 26.11.2026 | Release v0.3.0 mit #10–#11 (echte Vorschaubilder, Versionsnummer in den Dateinamen; Tag auf dem Stand nach #11, daher ohne #12–#14), als reguläres Release (kein Pre-release), damit GitHub es als „Latest“ anzeigt | Wunsch von Vulli |
| 26.11.2026 | #12–#14 (abschaltbare Vorschau, macOS-Download-Fix, einklappbare Warteschlange, kleineres ffmpeg) und die animierten Sheets (#15) erscheinen gemeinsam in v0.4.0; v0.3.0 bleibt wie veröffentlicht | Entscheidung von Vulli: veröffentlichte Versionen nicht nachträglich ändern |
| 26.11.2026 | Release v0.4.0 mit #12–#15; Tag wird erst nach dem Merge dieses Release-PRs auf `main` gesetzt | v0.3.0 wurde versehentlich vor den letzten PRs getaggt |
| 26.11.2026 | Release v0.5.0 mit #17–#19 (Copyright, Feinoptionen unter „Erweitert“, Installer/Pakete, fest hinterlegte ffmpeg-Builds); Tag nach dem Merge dieses Release-PRs auf `main` | erstes Release mit Installer, AppImage und DMG |
| 26.11.2026 | Release v0.6.0 mit #40–#47 (u. a. Issues #21–#30); Release-Notizen kommen automatisch aus dem CHANGELOG-Abschnitt der Version (`tools/release-notes.py` im Release-Workflow), auch wenn das Release im Browser angelegt wird | Wunsch von Vulli; Notizen und CHANGELOG bleiben gleich |
| 26.11.2026 | Kein Mac für Tests vorhanden: macOS wird nur über CI geprüft. Zusätzlich zu Build und Tests lädt die CI auf macOS ffmpeg über die eingebaute Download-Funktion (martin-riedl.de, mit Prüfsumme) und erstellt damit ein echtes Sheet (als Artefakt abrufbar). Die GUI auf macOS bleibt ungetestet | Vulli hat keinen Mac |
| 26.11.2026 | Von Vulli unter Windows geprüft: v0.1.0 (Programm), die Oberfläche „Leuchttisch“ und v0.5.0 (Installer, Startmenü-Eintrag, Oberfläche, Icon, ffmpeg-Download von gyan.dev) | erledigt die offenen Windows-Tests |
| 26.11.2026 | HDR mit echtem Material von Vulli geprüft: Ergebnis passt, Tonemapping bleibt wie es ist | erledigt den offenen HDR-Punkt |
| 26.11.2026 | Aufteilung „Einfach“/„Erweitert“ passt so (Vulli, nach erster Nutzung) | |
| 26.11.2026 | Social-Preview in den Repo-Einstellungen hochgeladen (Vulli) | |
| 26.11.2026 | Offene P2/P3-Punkte der Spezifikation sind GitHub-Issues (#21–#39, Labels `enhancement` und `P2`/`P3`) | Ideen nachvollziehbar, ohne dass sie wie vergessen aussehen |
| 26.11.2026 | `CHANGELOG.md` nach „Keep a Changelog“, auf Deutsch, rückwirkend ab v0.1.0. Jeder PR ergänzt seinen Eintrag unter „Unveröffentlicht“; beim Release wird daraus der Versionsabschnitt | Änderungen nachvollziehbar, Grundlage für Release-Notizen |

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
| 26.11.2026 | Jede Preset-Einstellung hat eine CLI-Option (#40). Schalter gibt es paarweise (`--shadow`/`--no-shadow` usw.), damit sich Presets in beide Richtungen übersteuern lassen. `--shadow-color`/`--shadow-offset` schalten den Schatten ein, `--frames-folder` die Einzelbilder. `--timestamp-opacity` nimmt 0–1 oder Prozent (Werte über 1 gelten als %). Ein Test vergleicht alle Felder von `SheetSettings` mit den Optionen, damit neue Einstellungen nicht ohne Option bleiben | Wunsch von Vulli: Kommandozeile um alle fehlenden Einstellungen ergänzen |
| 26.11.2026 | Farben auf der Kommandozeile werden beim Einlesen geprüft (`#RRGGBB`/`#AARRGGBB`); ungültig = Aufruffehler (Exit-Code 2) | Tippfehler fallen sofort auf statt als Standardfarbe im Sheet |
| 26.11.2026 | Speicherorte: Einstellungen/Presets unter `%APPDATA%\VideoThumbnailer`, `~/.config/video-thumbnailer`, `~/Library/Application Support/VideoThumbnailer`; ffmpeg unter `%LOCALAPPDATA%\VideoThumbnailer\ffmpeg`, `~/.local/share/video-thumbnailer/ffmpeg` bzw. macOS wie oben. Portabler Modus: Datei `portable.txt` neben der Programmdatei oder Umgebungsvariable `VIDEO_THUMBNAILER_HOME` | Standardpfade je Betriebssystem, USB-Stick-Betrieb möglich |
| 26.11.2026 | Auslieferung als selbstständige Einzeldatei je Plattform (win-x64, win-arm64, linux-x64, linux-arm64, osx-x64, osx-arm64), ohne vorinstalliertes .NET | „direkt überall lauffähig“ |
| 26.11.2026 | Code und Kommentare auf Englisch, Projektdokumentation auf Deutsch | öffentliches Repo |
| 26.11.2026 | GUI zweisprachig Deutsch/Englisch (vorgezogen aus P2). Standard ist die Systemsprache (Deutsch, wenn die Oberflächensprache des Systems Deutsch ist, sonst Englisch); in den Einstellungen umschaltbar (Systemsprache/English/Deutsch), wirkt sofort ohne Neustart und wird in `settings.json` gespeichert | Wunsch nach deutscher Oberfläche, Englisch für alle anderen |
| 26.11.2026 | Übersetzungen als eigene Tabelle im Code (`Localization/Strings.cs`) statt `.resx`; XAML bindet über `{l:T Schlüssel}`. Ein Test prüft, dass jeder benutzte Schlüssel existiert und beide Sprachen dieselben Platzhalter haben | einfach, ohne Satelliten-Assemblies, live umschaltbar, fehlende Texte fallen im Test auf |
| 26.11.2026 | Kommandozeile ebenfalls Deutsch/Englisch (Hilfe, Meldungen, Beschreibungen der Basis-Presets). Reihenfolge: `--lang de\|en\|auto` > Umgebungsvariable `VTHUMB_LANG` > in der GUI gewählte Sprache > Systemsprache (unter Windows `GetUserDefaultUILanguage`, sonst `LC_ALL`/`LC_MESSAGES`/`LANG`/`LANGUAGE`) | Wunsch: offene Punkte erledigen; gleiche Sprache in GUI und CLI |
| 26.11.2026 | Texte im Sheet folgen einer eigenen „Sheet-Sprache“ je Preset: Standard „Wie Oberfläche“, wählbar Deutsch/Englisch (GUI, `--sheet-lang`). So kann man z. B. mit deutscher Oberfläche englische Sheets erzeugen | Sheets werden oft weitergegeben; Sprache gehört zum Ergebnis, nicht nur zur Oberfläche |
| 26.11.2026 | Standard-Info-Kopf je Sprache („Größe:/Dauer:“ bzw. „Size:/Duration:“). Presets speichern ihn nicht als festen Text (`template: null`); die bisherige englische Standardvorlage gilt ebenfalls als Standard. Eigene Vorlagen bleiben unverändert | bestehende Presets/Einstellungen wechseln automatisch die Sprache, eigene Texte werden nicht überschrieben |
| 26.11.2026 | Deutsche Sheets mit Dezimalkomma (1,50 GB, 23,976 fps) und „kein Bild“, „keine“, „k. A.“ | übliche deutsche Schreibweise |
| 26.11.2026 | Texte aus Core/CLI in eigenen Tabellen (`CoreText`, `CliText`) wie in der GUI; Tests prüfen beide Sprachen und Platzhalter | einheitlich, ohne `.resx` |
| 26.11.2026 | Kommandozeile bleibt vorerst Englisch | **Überholt:** seit #6 zweisprachig, siehe oben |

### ffmpeg-Bezug
| Datum | Entscheidung | Begründung / Anmerkung |
|---|---|---|
| 26.11.2026 | ffmpeg wird nicht mitgeliefert. Beim ersten Bedarf (GUI: beim ersten Start; CLI: beim ersten Lauf) wird kurz gefragt und dann automatisch heruntergeladen. CLI: `--yes` lädt ohne Frage, `vthumb ffmpeg install/update/check/status/uninstall` | wie beschlossen |
| 26.11.2026 | Suchreihenfolge: eigener Ordner aus den Einstellungen → von uns heruntergeladenes ffmpeg → `PATH` (plus Homebrew-/Standardpfade) | vorhandene Installationen werden genutzt, nichts doppelt geladen |
| 26.11.2026 | Quelle Windows/Linux: **BtbN/FFmpeg-Builds** (GitHub), Release „latest“, **GPL**-Build (Linux statisch, Windows „shared“ seit #14) des neuesten Release-Zweigs (z. B. n9.0), nicht „master“ | stabil, statisch gelinkt, x64 und arm64. **Überholt** durch #19: Windows x64 von gyan.dev, Linux von martin-riedl.de; BtbN nur noch für Windows arm64 und `--latest` |
| 26.11.2026 | Quelle macOS: **ffmpeg.martin-riedl.de** (arm64/amd64, signiert und notarisiert), ffmpeg und ffprobe als getrennte ZIPs | BtbN baut kein macOS |
| 26.11.2026 | Prüfsumme: SHA-256 aus der vom Anbieter veröffentlichten Datei (`checksums.sha256` bzw. `*.sha256`); ohne gültige Prüfsumme wird nicht installiert | schützt gegen beschädigte Downloads. Seit #19 nur noch für `--latest` und Windows arm64, siehe „Fest hinterlegte ffmpeg-Builds“ |
| 26.11.2026 | Installation atomar: Entpacken in einen Arbeitsordner, Testlauf `ffmpeg -version`, dann Austausch des `bin`-Ordners; bei Fehlern bleibt die alte Version erhalten. `manifest.json` merkt sich Quelle, Version und Prüfsumme | Update kann nichts kaputt machen |
| 26.11.2026 | Update-Erkennung über die Prüfsumme (neuer Build = neue Prüfsumme). GUI prüft höchstens einmal pro Woche im Hintergrund (abschaltbar) und bietet das Update an; CLI über `vthumb ffmpeg check/update` | auch tägliche „latest“-Builds werden erkannt, ohne GitHub-API-Limits |

### Fest hinterlegte ffmpeg-Builds (#19)
| Datum | Entscheidung | Begründung / Anmerkung |
|---|---|---|
| 26.11.2026 | Installiert werden genau die Dateien aus `src/VideoThumbnailer.Core/Ffmpeg/ffmpeg-pins.json` (feste Versions-URL, SHA-256, Inhalt). Die App lädt die Liste beim Suchen nach ffmpeg/Updates von GitHub (`raw.githubusercontent.com`, `main`); ist sie nicht erreichbar, ungültig, ohne Eintrag für das System oder älter als die eingebaute Kopie, gilt die eingebaute Kopie. Die Prüfsumme kommt damit aus dem Repo, nicht vom Download-Server | Wunsch von Vulli: stärkerer Schutz. Ein später auf dem Server ausgetauschter Build (samt Prüfsummendatei) wird abgelehnt |
| 26.11.2026 | macOS zusätzlich: `codesign --verify --strict` und Team-ID des Herausgebers aus der Pin-Datei (martin-riedl.de: `KU3N25YGLU`) | ein gefälschter Build bräuchte das Apple-Zertifikat des Herausgebers |
| 26.11.2026 | Keine GPG-Prüfung: keiner der Anbieter veröffentlicht Signaturen | fest hinterlegte Hashes sind die stärkste verfügbare Prüfung |
| 26.11.2026 | Quellen gewechselt, weil BtbN datierte Builds nur 14 Tage aufhebt (fest hinterlegte Adressen wären schnell tot): Windows x64 → gyan.dev (GitHub-Releases von GyanD/codexffmpeg, „full_build-shared“, ca. 95 MB, ffplay wird nicht entpackt), Linux x64/arm64 → ffmpeg.martin-riedl.de (wie macOS, Release-Builds seit 2022 abrufbar). Windows arm64 bleibt bei BtbN „latest“ mit Anbieter-Prüfsumme, weil es dafür keine dauerhafte Quelle gibt | dauerhafte Adressen; gleiche Funktionen (GPL-Builds mit x264/x265, zimg). Bestehende Installationen sehen den Wechsel als Update |
| 26.11.2026 | `tools/ffmpeg-pins.py` erzeugt die Pin-Datei: lädt jede Datei, berechnet SHA-256 selbst und vergleicht mit der Prüfsumme des Anbieters (gyan: GitHub-Digest), liest auf macOS die Team-ID. Workflow „ffmpeg pins“ läuft montags und von Hand und committet neuere Builds direkt auf `main` (kein PR); bei PRs, die die Liste ändern, prüft er alle hinterlegten Dateien erneut | Wunsch von Vulli: neue ffmpeg-Versionen ohne Programmänderung und ohne Release. Geschützt wird gegen später ausgetauschte Dateien; ein Anbieter, der schon beim Eintragen kompromittiert ist, würde nicht erkannt |
| 26.11.2026 | `vthumb ffmpeg install\|update\|check --latest` installiert den neuesten Build (BtbN für Windows, martin-riedl.de sonst), geprüft nur gegen die Anbieter-Prüfsumme. Die GUI nutzt immer den hinterlegten Build | für Nutzer, die sofort die neueste ffmpeg-Version wollen |

### Installer und Pakete (#19)
| Datum | Entscheidung | Begründung / Anmerkung |
|---|---|---|
| 26.11.2026 | Windows: Inno Setup (`packaging/windows/installer.iss`), x64 und arm64. Standard: Installation nur für den Benutzer ohne Administratorrechte (der Dialog bietet „für alle Benutzer“), Startmenü-Eintrag, Desktop-Symbol optional, Deutsch/Englisch, feste AppId für Updates/Deinstallation. `vthumb.exe` liegt im Programmordner, wird aber nicht in den PATH eingetragen | kostenlos, auf den GitHub-Runnern vorhanden, kleiner als MSI-Werkzeuge; PATH-Änderungen bleiben dem Nutzer überlassen |
| 26.11.2026 | Linux: AppImage (`packaging/linux/make-appimage.sh`, appimagetool), x86_64 und aarch64. Startet die Oberfläche; `vthumb` als erstes Argument oder ein Link namens `vthumb` startet die Kommandozeile | eine Datei, läuft auf allen gängigen Distributionen; Flatpak/Pakete der Distributionen später bei Bedarf |
| 26.11.2026 | macOS: `Video-Thumbnailer.app` (Info.plist, `app.icns`, GUI und `vthumb` in `Contents/MacOS`) als DMG mit Verknüpfung auf „Programme“ (`packaging/macos/make-dmg.sh`), arm64 und x64. Nur Ad-hoc-signiert (nötig für Apple Silicon), nicht notarisiert | ohne Apple-Entwicklerkonto; Gatekeeper-Warnung beim ersten Start bleibt |
| 26.11.2026 | Alles unsigniert und im Release-Workflow gebaut, Namen `Video-Thumbnailer-<Version>-<rid>-setup.exe`, `….AppImage`, `….dmg`; ZIPs bleiben. Der Workflow installiert/öffnet je ein Paket pro System (win-x64, linux-x64, osx-arm64) und ruft `vthumb --version` auf | arm64-Windows/-Linux und Intel-Mac können auf den Runnern nicht gestartet werden |

### Gestaltung (Icon, Banner)
| Datum | Entscheidung | Begründung / Anmerkung |
|---|---|---|
| 26.11.2026 | Eigenes Icon: dunkler Filmstreifen mit Perforation, darin ein 3×3-Raster aus hellblauen Vorschaubildern, in der Mitte ein orangefarbenes Bild mit Play-Symbol (Variante A, am 26.11.2026 freigegeben; Alternative „blauer Filmstreifen“ verworfen) | zeigt sofort „Video → Bildraster“; hebt sich auf hellen und dunklen Oberflächen ab |
| 26.11.2026 | SVG als Master (`assets/branding/icon.svg`), dazu pixelgenaue Varianten für 16 px und 20–32 px (ohne Perforation, 16 px ohne Play-Dreieck); alle PNG/ICO/ICNS werden per `assets/branding/build.py` erzeugt und mit eingecheckt | kleine Größen bleiben scharf; Build braucht keine Grafikwerkzeuge |
| 26.11.2026 | Windows: `app.ico` (16–256 px) als `ApplicationIcon` für `VideoThumbnailer.exe` und `vthumb.exe`, dieselbe Datei als Fenstericon der Avalonia-Oberfläche (auch Dialoge) | ein Icon für Explorer, Taskleiste und Fenster |
| 26.11.2026 | macOS: `app.icns` (16–1024 px) liegt bereit, wird aber erst mit einem `.app`-Bundle genutzt; bis dahin zeigt das Fenster das Avalonia-Fenstericon | **Überholt:** seit #19 Icon von `Video-Thumbnailer.app` im DMG |
| 26.11.2026 | Linux: Icons im hicolor-Schema (16–512 px + scalable SVG) und `video-thumbnailer.desktop` unter `packaging/linux`, Installation von Hand beschrieben | Grundlage für spätere Pakete (AppImage/Flatpak). Seit #19 im AppImage enthalten; die Anleitung in `packaging/linux/README.md` gilt nur noch für die ZIPs |
| 26.11.2026 | README-Banner (1280×320) und GitHub-Social-Preview (1280×640) mit englischem Text („Contact sheets from your videos“), Schrift Inter (SIL OFL), als PNG eingebunden | öffentliches Repo; PNG zeigt überall dieselbe Schrift |

### Oberfläche (Einfach/Erweitert, Darstellung)
| Datum | Entscheidung | Begründung / Anmerkung |
|---|---|---|
| 26.11.2026 | Einstellungen in zwei Modi: „Einfach“ (Standard) und „Erweitert“, umschaltbar ganz oben in der Einstellungsspalte (seit #8 als Segment-Umschalter); die Wahl wird in `settings.json` gespeichert (`advancedMode`) | Wunsch nach übersichtlicherer Oberfläche |
| 26.11.2026 | „Einfach“ zeigt: Preset-Kacheln, Spalten, Zeilen, Bildbreite, Zeitstempel an/aus, Info-Kopf an/aus, Format/Qualität, Speicherort (mit Ordner bzw. Unterordnername). Sprache und Darstellung stehen im Zahnrad-Menü, ffmpeg-Status und Download-Knopf in der Fußleiste (beides in jedem Modus). Alles andere nur unter „Erweitert“, mit Hinweis darauf am Ende (angepasst mit #8) | das Nötigste für ein gutes Sheet; ffmpeg-Download muss auch für Einsteiger erreichbar sein |
| 26.11.2026 | Ausgeblendete Einstellungen bleiben wirksam (sie werden nur nicht angezeigt) und gehören weiterhin zum Preset | ein Wechsel des Modus ändert nie das Ergebnis |
| 26.11.2026 | Darstellung „Wie System“ (Standard), „Hell“ oder „Dunkel“ über die Theme-Varianten des Fluent-Themes, wirkt sofort und wird gespeichert (`theme`) | Dark-Mode-Wunsch; folgt standardmäßig dem Betriebssystem |
| 26.11.2026 | Die Darstellung betrifft nur die Oberfläche, nicht die Farben im Sheet (die kommen aus dem Preset) | Sheets sollen unabhängig vom Rechner gleich aussehen |

### Oberfläche „Leuchttisch“ (#8)
| Datum | Entscheidung | Begründung / Anmerkung |
|---|---|---|
| 26.11.2026 | Neues Fensterlayout nach Konzept B „Leuchttisch“ (von Vulli aus drei Entwürfen gewählt; A „Aufgeräumt“ und C „Drei Schritte“ verworfen): Warteschlange links (280 px), Vorschau in der Mitte, Einstellungen rechts (350 px), Fußleiste mit ffmpeg-Status, Gesamtfortschritt, Pause, Abbrechen und Start. Mindestbreite des Fensters 1040 px | Ergebnis vor dem Start sehen; Stapelverarbeitung bleibt sichtbar. C ist für wiederholte Arbeit zu langsam |
| 26.11.2026 | Live-Vorschau wird mit dem echten `SheetRenderer` gezeichnet (`SheetPreview` im Core): Raster, Abstände, Info-Kopf, Zeitstempel, Farben und Größe entsprechen dem Ergebnis. Statt Videobildern gemalte Platzhalter-Szenen, Videodaten im Kopf sind Beispielwerte (Dateiname und -größe vom gewählten Video). Neuberechnung 150 ms nach der letzten Änderung im Hintergrund, auf höchstens 1200 px Breite verkleinert. Darunter Bildanzahl und Bogengröße in Pixeln | eine Logik für Vorschau und Ergebnis, kein zweiter Zeichencode; schnell genug ohne ffmpeg |
| 26.11.2026 | Die Vorschau zeigt das zuletzt in der Warteschlange ausgewählte Video, sonst das erste, ohne Videos „Beispielvideo.mp4“ | |
| 26.11.2026 | Presets als Kacheln (4 pro Zeile) mit Rastersymbol aus Spalten × Zeilen; Beschreibung als Tooltip | Wahl auf einen Blick statt Namen in einer Liste |
| 26.11.2026 | „Ausgabe“ in der Gestaltung von Entwurf A (Wunsch von Vulli): Format als drei Knöpfe JPG/PNG/WebP, Qualität als Schieberegler 1–100 (bei PNG gesperrt, da verlustfrei), darunter „Speichern in“ | |
| 26.11.2026 | Zeitstempel und Info-Kopf als Schalter (ToggleSwitch) statt Häkchen | |
| 26.11.2026 | Sprache und Darstellung in einem Zahnrad-Menü oben rechts in der Einstellungsspalte | Programmeinstellungen, keine Bogen-Einstellungen; hält die Spalte frei |
| 26.11.2026 | Warteschlange: sichtbar nur „+ Dateien…“, „+ Ordner…“ und „Unterordner einbeziehen“; Auswahl entfernen, Erledigte/Alle leeren, Fehlgeschlagene wiederholen und Sortieren im Menü „⋯“. Je Video farbiger Status (grau wartet, blau läuft mit Fortschritt, grün fertig, rot Fehler) | weniger Knöpfe, Zustand auf einen Blick |
| 26.11.2026 | Eigene Farben als Theme-Ressourcen für hell und dunkel, abgeleitet aus dem Icon: leicht bläuliche Grautöne, Blau (#2585D0 hell / #5BB8F5 dunkel) für Auswahl, Fortschritt und Akzent, Orange (#E2711A / #F7892E) ausschließlich für den Start-Knopf | einheitlicher Auftritt mit Icon und Banner; Orange heißt „Los“ |
| 26.11.2026 | Paarfelder unter „Erweitert“ (Abstand/Rand, Anfang/Ende, Schriftgrößen, Deckkraft) ohne Auf/Ab-Pfeile, Eingabe per Tastatur oder Mausrad | sonst in der 350-px-Spalte zu schmal |

### Vorschau mit echten Bildern (#11)
| Datum | Entscheidung | Begründung / Anmerkung |
|---|---|---|
| 26.11.2026 | Die Vorschau zeigt echte Bilder und die echten Videodaten (Info-Kopf, Seitenverhältnis, Dauer, Zeitpunkte) des gewählten Videos, sobald ffmpeg vorhanden ist (Wunsch von Vulli, Stufe 2). Die Bilder stammen von genau den Zeitpunkten des fertigen Bogens (`TimestampPlanner`), die Reihenfolge „Zufällig“ nutzt denselben Startwert wie der Bogen | was man sieht, ist was man bekommt |
| 26.11.2026 | Ablauf: sofort zeichnen mit dem, was schon da ist (fehlende Bilder als Platzhalter), dann fehlende Bilder im Hintergrund holen und höchstens alle 300 ms neu zeichnen. Über der Vorschau steht „Lade Bilder… n von m“, danach „Echte Bilder aus dem Video“. Jede neue Änderung bricht den laufenden Ladevorgang ab | Einstellungen bleiben flüssig, auch bei langsamen Videos |
| 26.11.2026 | Cache in `PreviewSource` (Core): Videodaten (ffprobe) einmal je Datei, Bilder je Datei und Zeitpunkt in 320 px Breite, höchstens 200 Bilder (ca. 50 MB), älteste fliegen zuerst raus. Beim Ändern von Spalten/Zeilen werden nur neue Zeitpunkte geholt; ein fehlgeschlagenes Lesen wird beim nächsten Mal wiederholt | 320 px reichen, weil die Vorschau höchstens 1200 px breit ist |
| 26.11.2026 | Höchstens halb so viele ffmpeg-Prozesse wie CPU-Kerne, maximal 4, für die Vorschau; sie läuft auch während einer Verarbeitung | Vorschau soll die Verarbeitung nicht ausbremsen |
| 26.11.2026 | Platzhalter bleiben als Rückfall: ohne Video („Beispielbilder, echtes Layout“), ohne ffmpeg („Beispielbilder (ffmpeg fehlt)“) und bei nicht lesbaren Dateien („Beispielbilder (Video nicht lesbar)“). Nach einem ffmpeg-Download wird die Vorschau automatisch neu geladen | |

### Vorschau: Dateiänderungen, abschaltbar (#12)
| Datum | Entscheidung | Begründung / Anmerkung |
|---|---|---|
| 26.11.2026 | Der Vorschau-Cache merkt sich Größe und Änderungszeit jeder gelesenen Datei. Weichen sie ab (Datei ersetzt, neu kodiert, noch im Schreiben), werden Videodaten und Bilder dieser Datei vergessen und neu gelesen (`PreviewSource.ForgetIfChanged`). Geprüft wird bei jeder Vorschau-Aktualisierung und wenn das Fenster wieder den Fokus bekommt; ein Bild, das während einer Änderung entstand, wird verworfen | billiger als ein Dateiwächter, deckt den üblichen Fall „Datei draußen geändert, zurück ins Programm“ ab |
| 26.11.2026 | Live-Vorschau abschaltbar (Wunsch von Vulli): Schalter „Live-Vorschau“ oben rechts über der Vorschau, Standard an, gespeichert in `settings.json` (`livePreview`). Aus: laufendes Laden wird abgebrochen, Bild freigegeben, keine Vorschau gezeichnet und kein ffmpeg/ffprobe für die Vorschau gestartet; in der Mitte steht ein Hinweis, dass die Einstellungen trotzdem gelten | für langsame Rechner, Netzlaufwerke oder Akku; Schalter dort, wo die Vorschau ist |

### Schmale Fenster, kleineres ffmpeg, HDR (#14)
| Datum | Entscheidung | Begründung / Anmerkung |
|---|---|---|
| 26.11.2026 | Warteschlange einklappbar: unter 1120 px Fensterbreite klappt sie automatisch zu einer 52 px breiten Leiste (Anzahl, „+“, Ordner, ein Status-Punkt je Video); wird das Fenster wieder breit, klappt sie nur auf, wenn sie automatisch eingeklappt wurde. Von Hand jederzeit mit « / » umschaltbar. Mindestbreite 820 statt 1040 px | Vorschau und Einstellungen behalten im schmalen Fenster ihren Platz; eine Entscheidung von Hand wird nicht überschrieben |
| 26.11.2026 | ffmpeg unter Windows als BtbN-„gpl-shared“-Build: ffmpeg.exe/ffprobe.exe plus die DLLs aus `bin` (ffplay wird nicht entpackt). Gemessen: 87–90 MB statt 198 MB Download, ca. 200 statt 340 MB installiert. Linux bleibt beim statischen Build (62 MB, shared spart nur ca. 9 MB und bräuchte Bibliothekspfade) | gleicher Funktionsumfang (GPL, zimg, x264/x265-Decoder), etwa halbe Größe. Bestehende Windows-Installationen sehen den Wechsel als Update (andere Prüfsumme). **Überholt** durch #19: Windows x64 lädt den „full_build-shared“ von gyan.dev |
| 26.11.2026 | HDR-Tonemapping geprüft mit echten HDR10- (PQ, BT.2020, 10 bit, HDR10-Metadaten) und HLG-Videos (x265): Farben stimmen, kein Grünstich, nicht ausgewaschen; etwas dunkler und weniger satt als SDR (mittlere Helligkeit ca. 80–85 % der SDR-Fassung), typisch für „hable“. Filter bleibt unverändert; ein Integrationstest sichert das ab (übersprungen ohne libx265/zscale) | größere Eingriffe (z. B. andere Kurve) nur bei Rückmeldung mit echtem Material |

### Animierte Sheets
| Datum | Entscheidung | Begründung / Anmerkung |
|---|---|---|
| 26.11.2026 | Animiertes Sheet = gleiches Raster wie das Standbild, jede Kachel spielt einen kurzen Ausschnitt ab ihrem Zeitpunkt ab; Zeitstempel laufen mit, Info-Kopf bleibt stehen | gleiche Gestaltung und Einstellungen wie beim Standbild |
| 26.11.2026 | Formate MP4 (H.264, `yuv420p`, `faststart`), WebM (VP9) und GIF (eigene Palette, Bayer-Dithering, Endlosschleife). Ohne Ton | überall abspielbar bzw. für Web/Foren; Ton ergibt bei vielen gleichzeitigen Ausschnitten keinen Sinn. Ergänzt durch #30: weitere Formate und Ton einer Kachel als Option |
| 26.11.2026 | Standard: 3 s je Ausschnitt, 10 Bilder/s (einstellbar 0,5–30 s, 1–60 Bilder/s). Die Qualität (Schieberegler) steuert bei MP4/WebM den CRF-Wert, bei GIF hat sie keine Wirkung | kleine Dateien, flüssig genug für eine Übersicht |
| 26.11.2026 | Animiert ist ein Schalter im Preset (`animation.enabled`); dann wird statt des Bildes die Animation gespeichert. Einzelbilder („jede Aufnahme speichern“) nur beim Standbild | ein Durchlauf, ein Ergebnis; Standbild und Animation lassen sich über zwei Presets erzeugen |
| 26.11.2026 | Animierte Sheets heißen immer `<Name>.sheet.<mp4\|webm\|gif>`; Ordner-Scans überspringen solche Dateien | sonst würde `film.mp4` neben dem Video überschrieben bzw. beim nächsten Lauf selbst als Video erkannt |
| 26.11.2026 | Ablauf: je Kachel ein ffmpeg-Prozess dekodiert den Ausschnitt in Kachelgröße als JPEG-Folge (im Speicher), dann zeichnet der vorhandene Renderer jedes Bild des Sheets und ein weiterer ffmpeg-Prozess kodiert sie | wiederverwendet Layout, Schriften und Zeitstempel-Stil; wenig Speicher (JPEG statt Rohbilder) |
| 26.11.2026 | Bei Animation wird der Ausschnitt am Ende mit eingeplant (Zeitpunkte enden um die Ausschnittlänge früher), damit die letzte Kachel nicht leerläuft; endet ein Ausschnitt trotzdem früher, bleibt sein letztes Bild stehen | |
| 26.11.2026 | Live-Vorschau zeigt bei Animation weiterhin ein Standbild (erstes Bild jeder Kachel) | Vorschau bleibt schnell |

### Feinoptionen in der Oberfläche (#18)
| Datum | Entscheidung | Begründung / Anmerkung |
|---|---|---|
| 26.11.2026 | Konturfarbe, Schattenfarbe, Millisekunden (Zeitstempel) und Titel-/Textfarbe (Info-Kopf) sind in der GUI einstellbar, nur im Modus „Erweitert“ (Wunsch von Vulli). Kein eigener Optionsdialog: die Felder stehen bei ihrem Abschnitt | wer sie braucht, ist ohnehin in „Erweitert“; Einfach bleibt schlank |
| 26.11.2026 | ~~Kein Farbwähler~~ (abgelöst durch #42, Farbwähler am Farbmuster). Farben bleiben Textfelder (#RRGGBB oder #AARRGGBB, AA = Deckkraft) mit Farbmuster daneben (Schachbrett zeigt Transparenz). Ungültige Werte bleiben stehen, werden mit „?“ markiert, und der Bogen nimmt solange die Standardfarbe. Geprüft wird mit demselben Parser wie beim Rendern | kein zusätzliches Paket (Avalonia-ColorPicker ist eigenständig); die Werte passen 1:1 zum Preset-JSON |
| 26.11.2026 | Schattenfarbe ist nur bei aktivem Schlagschatten bearbeitbar; Farbe/Deckkraft und Kontur/Konturfarbe stehen in getrennten Zeilen | in der 350 px breiten Spalte passen #AARRGGBB-Werte sonst nicht ganz hinein |

### Farbwähler und Schriftauswahl (#42)
| Datum | Entscheidung | Begründung / Anmerkung |
|---|---|---|
| 26.11.2026 | Farbwähler (Wunsch von Vulli) mit dem offiziellen Paket `Avalonia.Controls.ColorPicker` 12.2.0: Klick auf das Farbmuster öffnet ein Aufklappfenster mit Spektrum, Palette und Reglern inkl. Deckkraft. Jede Änderung wird sofort als #AARRGGBB ins Feld geschrieben (Vorschau zieht mit); die Hex-Eingabe bleibt. Ersetzt die Entscheidung „kein Farbwähler“ aus #18 | gleiche Quelle und Version wie Avalonia; ein Aufklappfenster statt eines eigenen Fensters, weil man die Wirkung in der Vorschau sofort sieht |
| 26.11.2026 | Schriftauswahl als eigenes Fenster („…“ neben „Schriftart“): durchsuchbare Liste der installierten Schriften, so wie der Bogen-Renderer (SkiaSharp) sie findet, jede in ihrer eigenen Schrift; oben „mitgelieferte DejaVu Sans“; darunter eine Vorschau von Info-Kopf und Zeitstempel. Freitext im Feld bleibt möglich | Liste stammt aus derselben Quelle wie das Rendern, also funktioniert jede auswählbare Schrift auch im Bogen |
| 26.11.2026 | Eine Schrift für den ganzen Bogen (Info-Kopf und Zeitstempel), wie bisher im Preset (`fontFamily`); keine getrennten Schriften je Bereich | hält Presets und CLI unverändert; getrennte Schriften wären ein eigenes Thema |
| 26.11.2026 | Schriften, die die Oberfläche nicht lesen kann (in der Testumgebung brachte eine Systemschrift Avalonia zum Absturz), werden in der Liste in der Standardschrift gezeigt statt die App abstürzen zu lassen | Absicherung gegen kaputte oder exotische Schriftdateien |

### README-Screenshots (#44)
| Datum | Entscheidung | Begründung / Anmerkung |
|---|---|---|
| 26.11.2026 | Die Bilder in `docs/images` (`gui.png`, `gui-de.png`, `gui-dark.png`, `gui-narrow.png`) erzeugt der Test `Readme_screenshots` (headless, echtes Testvideo per ffmpeg): `VTHUMB_README_SHOTS=$PWD/docs/images dotnet test --filter Readme_screenshots` (absoluter Pfad). Neu ist das Bild mit eingeklappter Warteschlange im schmalen Fenster | Bilder lassen sich nach jeder Oberflächenänderung mit einem Befehl neu erzeugen |

### Videos per URL (#21)
| Datum | Entscheidung | Begründung / Anmerkung |
|---|---|---|
| 26.11.2026 | Eingabe `http://` und `https://` (keine anderen Protokolle). ffmpeg/ffprobe lesen die Adresse direkt, mit Range-Anfragen springen sie zu den Aufnahmezeitpunkten; es wird nichts zwischengespeichert | kein vollständiger Download, kein eigener HTTP-Code |
| 26.11.2026 | Oberfläche: Knopf mit Weltkugel neben „+ Dateien…“/„+ Ordner…“ (auch in der eingeklappten Leiste) öffnet ein Eingabefeld, mehrere Adressen durch Leerzeichen getrennt; eine aus dem Browser gezogene Adresse wird ebenfalls übernommen. Kommandozeile: URL statt Datei | kein eigener Dialog nötig; in die 280 px breite Warteschlange passen keine drei beschrifteten Knöpfe |
| 26.11.2026 | Zeitüberschreitung 30 s ohne Daten (`-rw_timeout`), automatisches Wiederverbinden bis 5 s (`-reconnect`, `-reconnect_on_network_error`). Ein Fehler (Server weg, 404) lässt nur dieses Video fehlschlagen | hängende Server blockieren die Warteschlange nicht |
| 26.11.2026 | Server ohne Range-Unterstützung werden nicht gesondert behandelt: ffmpeg liest dann bis zur Stelle (langsam); MP4 mit Index am Dateiende lassen sich so nicht öffnen und schlagen mit der ffprobe-Meldung fehl | seltener Fall; ein Download-Modus wäre ein eigenes Thema |
| 26.11.2026 | Dateiname, Endung, `{name}`/`{ext}` und der Seed für „zufällig“ kommen aus dem letzten Pfadteil der Adresse (URL-dekodiert); `{folder}` ist die Adresse bis zum letzten „/“, `{path}` die ganze Adresse, jeweils **ohne** Benutzername/Passwort, Query und Fragment. Die Dateigröße liefert ffprobe (Content-Length) | Query-Parameter enthalten oft Zugangsschlüssel, die nicht auf einem Bild landen sollen |
| 26.11.2026 | Ausgabe: „neben dem Video“ und „Unterordner“ gibt es bei URLs nicht; die Sheets gehen in den eingestellten Ausgabeordner, sonst in der Oberfläche in den Bilder-Ordner des Benutzers, in der Kommandozeile in den aktuellen Ordner. Ordnerstruktur beibehalten entfällt | vorhersehbarer Ort ohne Rückfrage |
| 26.11.2026 | Live-Vorschau funktioniert auch mit URLs (Bilder laden im Hintergrund, bei langsamen Quellen entsprechend später); eine URL wird je Programmlauf nur einmal mit ffprobe gelesen (keine Prüfung auf Änderungen wie bei Dateien) | Vorschau bleibt bedienbar; Änderungsprüfung bräuchte zusätzliche Anfragen |

### Schwarze Bilder überspringen (#22)
| Datum | Entscheidung | Begründung / Anmerkung |
|---|---|---|
| 26.11.2026 | Erkennung im Programm am fertig dekodierten Bild statt mit dem ffmpeg-Filter `blackframe`: Helligkeit (Rec. 709) auf einem Raster von höchstens 64 × 64 Punkten. „Leer“ ist ein Bild mit Standardabweichung &lt; 6 (einfarbig, auch weiß oder blau) oder mit Mittelwert &lt; 24 und Standardabweichung &lt; 12 (schwarz/fast schwarz). Abspann (weiße Schrift auf Schwarz) gilt nicht als leer | kein zusätzlicher ffmpeg-Lauf für Bilder, die gut sind; erkennt auch einfarbige Bilder, die `blackframe` nicht sieht |
| 26.11.2026 | Suche nach einem Ersatz in Schritten von 0,5 s abwechselnd danach und davor (+0,5, −0,5, +1, −1 …) bis zum Suchbereich, Standard ±2 s (einstellbar 0,5–30 s); das erste gute Bild gewinnt. Findet sich keins, bleibt das ursprüngliche Bild | nächstgelegenes Bild verändert die Verteilung am wenigsten |
| 26.11.2026 | Standardmäßig **an** (`skipBlackFrames`, `blackFrameSearchSeconds` im Preset), auch für bestehende Presets; abschaltbar unter „Erweitert“ (Layout) und mit `--no-skip-black`, Suchbereich mit `--black-search` | die Spezifikation verlangt den automatischen Ersatz; bei guten Bildern kostet er nichts |
| 26.11.2026 | Der Zeitstempel zeigt den tatsächlich verwendeten Zeitpunkt, ebenso der Name der Einzelbilder | |
| 26.11.2026 | Animierte Sheets: Ist das erste Bild eines Ausschnitts leer, beginnt der Ausschnitt beim nächsten guten Bild. Live-Vorschau: ersetzt genauso (die Vorschau zeigt also, was im Sheet landet) | gleiche Logik überall |
| 26.11.2026 | Von Hand gewählte Zeitpunkte (#23) werden nie ersetzt | die Wahl ist Absicht |

### Aufnahmezeitpunkte von Hand (#23)
| Datum | Entscheidung | Begründung / Anmerkung |
|---|---|---|
| 26.11.2026 | Zeitpunkte gehören zum einzelnen Video, nicht zum Preset: in der Oberfläche zum Eintrag der Warteschlange (bis zum Schließen des Programms, nicht gespeichert), in der Kommandozeile per `--times` für alle angegebenen Videos | ein Preset gilt für viele Videos; Speichern je Video gehört zur JSON-Begleitdatei (#38) |
| 26.11.2026 | Zuordnung je Kachel in Zeitfolge (Index 0 = früheste Kachel), nicht nach Position im Raster; leere Einträge bleiben automatisch. Ändern sich Spalten/Zeilen, bleiben die Zeitpunkte am Index stehen (überzählige werden ignoriert, kommen bei mehr Kacheln wieder); bei „zufällig“ wandert die Kachel mit ihrem Zeitpunkt. Zeitpunkte außerhalb des Videos werden auf Anfang/Ende begrenzt | einfache, vorhersehbare Regel, auch für die Kommandozeile |
| 26.11.2026 | Bedienung: Kachel in der Live-Vorschau anklicken (nur bei echten Bildern eines Videos aus der Warteschlange) öffnet einen Dialog mit Schieberegler über das ganze Video, Zeitfeld (hh:mm:ss, mm:ss oder Sekunden), Schritten ±10 s, ±1 s, ±1 Bild; Tastatur über den Schieberegler (Pfeile 1 s, Bild auf/ab 10 s). Jede Änderung erscheint sofort in der Vorschau; „Abbrechen“ stellt den alten Stand her, „Automatisch“ setzt die Kachel zurück. Über der Vorschau zeigt „✎ n von Hand ✕“ die Anzahl und setzt alle zurück; in der Warteschlange markiert ✎ solche Videos | die Vorschau dient als Bildanzeige des Dialogs, kein zweiter Player nötig |
| 26.11.2026 | Animierte Sheets: je Kachel optional eine eigene Länge des Ausschnitts (Dialog bzw. `5:30+2`); die Animation dauert so lange wie der längste Ausschnitt, kürzere bleiben auf ihrem letzten Bild stehen | wie bisher bei Ausschnitten, die am Videoende kürzer sind |
| 26.11.2026 | Kommandozeile: `--times "1:00,5:30+2,-,1:02:10.5"` (Komma oder Semikolon als Trenner; Komma als Dezimalzeichen nur im Dialog) | |

### Spezial-Raster mit großen und kleinen Kacheln (#24)
| Datum | Entscheidung | Begründung / Anmerkung |
|---|---|---|
| 26.11.2026 | Eigenes, offenes Format als JSON im Preset (`template`): Raster aus `columns` × `rows` Feldern im Seitenverhältnis des Videos, dazu eine Liste `tiles` mit linkem oberen Feld (`x`, `y`) und Größe (`size`). Eine Kachel ist immer quadratisch in Feldern (`size` × `size`), Felder ohne Kachel bleiben Hintergrund. Ohne `template` gilt wie bisher das einfache Raster aus Spalten × Zeilen | quadratische Felder halten jedes Bild im Seitenverhältnis des Videos, ohne Verzerren oder Zuschneiden; das Format ist einfach genug für den späteren Baukasten-Editor (#36) und von Hand schreibbar |
| 26.11.2026 | Eine große Kachel überspannt auch die Abstände dazwischen; ihr Bild wird in ihrer Größe aufgenommen (nicht hochskaliert). Weil die Abstände Breite und Höhe nicht genau im Verhältnis vergrößern, wird das Bild um wenige Pixel mittig beschnitten | scharfe große Bilder; der Beschnitt liegt unter 1 % |
| 26.11.2026 | Reihenfolge der Zeitpunkte: nach dem linken oberen Feld jeder Kachel, zeilenweise (bzw. spaltenweise bei „spaltenweise“); „zufällig“ mischt wie bisher. Eine große Kachel kommt also dort dran, wo sie beginnt, und zeigt keinen besonderen Zeitpunkt. Zeitpunkte von Hand (#23) gelten je Kachel in dieser Zeitfolge | gleiche, vorhersehbare Regel wie beim einfachen Raster; wer der großen Kachel einen bestimmten Moment geben will, wählt ihn von Hand |
| 26.11.2026 | Vier eingebaute Vorlagen: „Groß oben“ (4 × 6 Felder, eine große Kachel oben in der Mitte, 21 Bilder), „Groß Mitte“ (4 × 4, große Kachel in der Mitte, 13 Bilder), „Zwei groß“ (4 × 5, zwei große oben, 14 Bilder), „Groß zuerst“ (5 × 5, das erste Bild 3 × 3 groß oben links, 17 Bilder). Am Preset ändert sich nichts: die vier Basis-Presets bleiben beim einfachen Raster | eine Handvoll typischer Aufteilungen; Standardverhalten bleibt gleich |
| 26.11.2026 | Oberfläche: Kachelreihe „Layout“ (auch im Modus „Einfach“) mit Symbol wie bei den Presets: „Raster“ plus die eingebauten Vorlagen; ein Preset mit eigener Vorlage bekommt eine zusätzliche Kachel mit deren Namen. Bei einer Vorlage sind Spalten und Zeilen ausgegraut; die Preset-Kacheln zeigen ihre Vorlage. Unter der Vorschau steht „Groß oben = 21 Bilder“ statt „4 × 5 = 20 Bilder“ | gleiche Bedienung wie bei den Presets |
| 26.11.2026 | Hochkant-Videos: Die Vorlage wird wie das einfache Raster gedreht (Spalten und Zeilen getauscht), wenn „Hochkant-Videos: tauschen“ an ist und sie mehr Zeilen als Spalten hat | gleiche Regel wie bisher |
| 26.11.2026 | Kommandozeile: `--layout big-top|big-center|two-big|big-first`, eine JSON-Datei (nur die Vorlage oder ein ganzes Preset) oder `grid` für das einfache Raster. Ungültige Vorlagen (überlappend, außerhalb, leer, mehr als 1000 Kacheln) werden beim Start mit einer Meldung abgelehnt. `{cols}`/`{rows}` im Dateinamen nennen die Felder der Vorlage | |

### Bildgröße „An Bildschirm anpassen“ (#25)
| Datum | Entscheidung | Begründung / Anmerkung |
|---|---|---|
| 26.11.2026 | Neue Einstellung „Bildgröße“ (`fitToScreen`): „Feste Breite“ (`Off`, Standard, wie bisher), „Bildschirmbreite“ (`Width`: Sheet so breit wie der Bildschirm) und „An Bildschirm anpassen“ (`WidthAndHeight`: so groß wie möglich, ohne dass Breite oder Höhe über den Bildschirm hinausgehen; das Sheet ist dann ggf. schmaler) | die Spezifikation nennt Breite „und ggf. Höhe“; beides ist nützlich |
| 26.11.2026 | Im Preset wird der Modus gespeichert, nicht die ausgerechnete Zahl; `sheetWidth` bleibt als feste Ersatzbreite erhalten | ein Preset passt sich so auf jedem Rechner an |
| 26.11.2026 | Maßgeblich ist der Bildschirm, auf dem das Programmfenster gerade ist (bei mehreren Monitoren), und zwar dessen nutzbare Fläche ohne Taskleiste/Dock in **physischen Pixeln** (bei 150 % Skalierung auf einem 4K-Monitor also 3840 px breit, nicht 2560). Wird das Fenster auf einen anderen Bildschirm geschoben, zieht die Vorschau mit | Bildbetrachter zeigen „100 %“ meist Pixel für Pixel; so ist das Sheet scharf und passt ohne Scrollen |
| 26.11.2026 | Kommandozeile und Betrieb ohne Bildschirm: `--screen BxH` gibt die Größe vor; ohne die Option gilt der Bildschirm, den die Oberfläche zuletzt gesehen hat (in `settings.json`, `lastScreen`); ist keiner bekannt, gilt die feste Breite aus dem Preset, mit Hinweis. Modus per `--fit-screen off|width|both` | Headless-Läufe bleiben vorhersagbar, ohne dass jedes Mal eine Größe angegeben werden muss |
| 26.11.2026 | Eine feste Kachelbreite (`tileWidth`, `--tile-width`) gilt nur bei „Feste Breite“; bei „Bildschirmbreite“ und „An Bildschirm anpassen“ wird sie ignoriert | der Bildschirm bestimmt die Größe |

### Hintergrundbild (#26)
| Datum | Entscheidung | Begründung / Anmerkung |
|---|---|---|
| 26.11.2026 | Bild hinter den Kacheln (`backgroundImage.path`), gezeichnet über der Hintergrundfarbe. Darstellung „Füllen“ (Standard: bedeckt das Sheet, an den Seiten abgeschnitten), „Strecken“, „Kacheln“ (Originalgröße wiederholt), „Zentriert“ (Originalgröße) | die vier Arten aus dem Issue; „Füllen“ verzerrt nicht und lässt keine Ränder |
| 26.11.2026 | Ein Regler „Deckkraft“ (0–1, Standard **0,5**) statt getrennter Werte für Deckkraft und Abdunkeln: das Bild wird mit der Hintergrundfarbe gemischt, bei dunkler Farbe also abgedunkelt, bei heller aufgehellt | ein Regler reicht für Lesbarkeit von Zeitstempeln und Info-Kopf; 0,5 ist auf dem dunklen Standardhintergrund gut lesbar |
| 26.11.2026 | Pfad im Preset: die Oberfläche speichert absolute Pfade. Relative Pfade in einer Preset-Datei gelten relativ zum Ordner dieser Datei (beim Laden in absolute umgewandelt), auf der Kommandozeile relativ zum aktuellen Ordner. Beim Export wird ein Bild im Ordner der Exportdatei (oder darunter) relativ gespeichert, sonst absolut; das Bild selbst wird nicht eingebettet oder mitkopiert | Preset und Bild lassen sich zusammen weitergeben, ohne die Preset-Datei aufzublähen |
| 26.11.2026 | Fehlt das Bild oder ist es nicht lesbar, entsteht das Sheet mit der Hintergrundfarbe und der Eintrag bekommt einen Hinweis; die Kommandozeile prüft `--background-image` sofort | ein verschobenes Bild soll keinen Stapellauf scheitern lassen |
| 26.11.2026 | Oberfläche nur unter „Erweitert“ (Zeile „Hintergrundbild“ mit „…“ und ✕, darunter Darstellung und Deckkraft); Kommandozeile `--background-image <Datei|none>`, `--background-mode`, `--background-opacity`. Auch animierte Sheets bekommen das Bild (einmal gelesen, nicht je Einzelbild) | |

### Kachel-Effekte (#27)
| Datum | Entscheidung | Begründung / Anmerkung |
|---|---|---|
| 26.11.2026 | Neue Preset-Gruppe `tileStyle`: Rahmen (`borderWidth`, `borderColor`), Schlagschatten (`shadow`, `shadowColor`, `shadowOffset`, `shadowBlur`), Graustufen (`grayscale`), Ecken (`corners`: `Square`/`Rounded`/`Cut`, `cornerRadius`). Dazu `sheetBorderWidth`/`sheetBorderColor` für einen Rahmen um das ganze Sheet. **Alles standardmäßig aus**, die eingebauten Presets bleiben unverändert | bestehende Sheets sehen genauso aus wie vorher |
| 26.11.2026 | Rahmen und Schatten verändern das Layout **nicht**: Der Kachelrahmen wird innen gezeichnet (folgt den Ecken), der Schatten fällt in Abstand/Rand, der Sheet-Rahmen liegt im Rand. Abstand und Rand stellt man bei Bedarf selbst größer | sonst müssten Kachelgröße, „An Bildschirm anpassen“ und die Aufnahmegröße der Bilder mitrechnen; so bleibt jede Rechnung wie sie ist, und die Wirkung sieht man sofort in der Vorschau |
| 26.11.2026 | Alle Schatten werden vor allen Kacheln gezeichnet; ein Schatten größer als der Abstand liegt also unter der Nachbarkachel, nie darüber | sauberes Bild auch bei kleinem Abstand |
| 26.11.2026 | Ecken schneiden das Bild ab (Clip), Zeitstempel und Wasserzeichen je Kachel ebenso; bei „eckig“ wird nichts geclippt | gleiche Form für Bild, Rahmen und Schatten |
| 26.11.2026 | Graustufen nach Rec. 709 (wie die Schwarzbild-Erkennung), nur die Kachelbilder, nicht Hintergrundbild, Zeitstempel oder Info-Kopf | Effekt gehört zu den Kacheln |
| 26.11.2026 | Gilt für Standbild, animierte Sheets (jedes Bild wird ohnehin neu gezeichnet) und die Live-Vorschau. Einzelbilder („jede Aufnahme speichern“) bleiben unverändert | Einzelbilder sind die Rohaufnahmen |
| 26.11.2026 | Oberfläche nur unter „Erweitert“, eigener Block unter dem Layout; ein Wert pro Zeile (Breite und Farbe getrennt). Versatz und Weichheit des Schattens nur im Preset und auf der Kommandozeile (wie beim Zeitstempel-Schatten) | Paare passen nicht in die 350 px breite Spalte; die Standardwerte (je 4 px) passen meistens |
| 26.11.2026 | Kommandozeile: `--tile-border`, `--tile-border-color`, `--no-tile-border`, `--tile-shadow`/`--no-tile-shadow`, `--tile-shadow-color`/`-offset`/`-blur` (schalten den Schatten ein), `--grayscale`/`--no-grayscale`, `--corners square\|rounded\|cut`, `--corner-radius`, `--sheet-border`, `--sheet-border-color`, `--no-sheet-border` | Muster wie bei den Zeitstempel-Optionen |

### Kommentartext (#28)
| Datum | Entscheidung | Begründung / Anmerkung |
|---|---|---|
| 26.11.2026 | Kein eigener Bereich mit Position/Schrift/Farbe, sondern Teil des Info-Kopfs: `header.comment` (mehrzeilig möglich). Steht `{comment}` in der Vorlage, kommt der Text dorthin; sonst als letzte Zeile(n) unter die Video-Infos, in Textgröße und -farbe des Kopfs. Ohne Kommentar verschwindet eine Zeile, die nur `{comment}` enthält | funktioniert ohne Anpassen der Vorlage, lässt sich aber frei platzieren; Schrift und Farbe gibt es schon beim Info-Kopf |
| 26.11.2026 | Ist der Info-Kopf aus, erscheint auch der Kommentar nicht. Wer nur den Kommentar will, setzt die Vorlage auf `{comment}` | eine Stelle für Text im Sheet |
| 26.11.2026 | Gespeichert im Preset; `--comment "…"` (`\n` für Zeilen) setzt ihn für einen Lauf, `--no-comment` entfernt ihn. Oberfläche: Feld „Kommentar“ im Info-Kopf unter „Erweitert“ | beides aus dem Issue: je Preset und je Lauf |

### Wasserzeichen (#29)
| Datum | Entscheidung | Begründung / Anmerkung |
|---|---|---|
| 26.11.2026 | Preset-Gruppe `watermark`: `text`, `imagePath`, `position` (`TopLeft`, `TopRight`, `BottomLeft`, `BottomRight` (Standard), `Center`, `Tiled` = wiederholt), `size` (Breite relativ zur Fläche, Standard 0,2), `opacity` (Standard 0,5), `color` (Text), `perTile`. Ein Bild hat Vorrang vor dem Text; ohne beides kein Wasserzeichen (Standard) | eine Einstellung, ein Wasserzeichen; die Größe relativ zur Breite passt sich jeder Sheet-Größe an |
| 26.11.2026 | Wahlweise einmal über dem ganzen Sheet (Standard, über Kacheln und Info-Kopf) oder in jeder Kachel (`perTile`, Größe dann relativ zur Kachel, unter dem Zeitstempel). Abstand zum Rand 3 % der kürzeren Seite. Nie größer als die Fläche (lange Texte werden verkleinert) | beide Varianten aus dem Issue; der Zeitstempel bleibt lesbar |
| 26.11.2026 | Text in fetter Sheet-Schrift ohne Kontur; Schriftgröße so, dass der Text die eingestellte Breite hat. „Wiederholt“ setzt das Wasserzeichen versetzt in Abständen von 1,5 × Breite bzw. 2,5 × Höhe | schlicht; Lesbarkeit regelt die Deckkraft |
| 26.11.2026 | Auch in animierten Sheets (Bild wird einmal gelesen, siehe `ImageFileCache`) und der Live-Vorschau; **nicht** in Einzelbildern | Einzelbilder sind die Rohaufnahmen, wie bei den Kachel-Effekten |
| 26.11.2026 | Bildpfade wie beim Hintergrundbild: relativ zur Preset-Datei, beim Export im selben Ordner relativ gespeichert; fehlt das Bild, entsteht das Sheet ohne Wasserzeichen mit Hinweis. `--watermark-image` prüft die Datei sofort | gleiche Regeln wie #26 |
| 26.11.2026 | Kommandozeile: `--watermark <Text>` (löscht ein Bild), `--watermark-image <Datei\|none>`, `--no-watermark`, `--watermark-pos`, `--watermark-size`/`--watermark-opacity` (0–1 oder %), `--watermark-color`, `--watermark-per-tile`/`--watermark-sheet`. Oberfläche unter „Erweitert“ im Block der Kachel-Effekte | |

### Weitere Animationsformate und Ton (#30)
| Datum | Entscheidung | Begründung / Anmerkung |
|---|---|---|
| 26.11.2026 | Alle vier Formate aus dem Issue umgesetzt: **H.265** (MP4, `libx265`, Tag `hvc1` für Apple-Player, CRF 18–44), **animiertes WebP** (`libwebp_anim`, sonst `libwebp`; Qualität = WebP-Qualität), **APNG** (verlustfrei, Qualitätsregler ohne Wirkung) und **OGV** (Theora, Qualität 1–10). Dateiendungen `.sheet.mp4`, `.sheet.webp`, `.sheet.png`, `.sheet.ogv` | jeder Encoder kostet nur ein paar Zeilen. APNG wird groß und OGV ist kaum noch verbreitet, aber beide sind ausdrücklich gewünscht und schaden niemandem, der sie nicht wählt. APNG als `.png`, weil das jeder Browser und Bildbetrachter öffnet |
| 26.11.2026 | Vor dem Kodieren wird einmal je Programmlauf `ffmpeg -encoders` gelesen. Fehlt der nötige Encoder, schlägt nur dieses Video mit „Diesem ffmpeg fehlt der Encoder …“ fehl | ein ffmpeg aus der Distribution oder ein schlanker Build hat nicht jeden Encoder; eine klare Meldung statt einer ffmpeg-Fehlermeldung. Ob die fest hinterlegten Builds (gyan.dev, martin-riedl.de) alle Encoder haben, prüft die CI: sie erzeugt mit dem heruntergeladenen ffmpeg auf allen drei Systemen je ein Sheet in H.265, WebP, APNG und OGV (mit Ton) |
| 26.11.2026 | **Ton**: Schalter `animation.audio` (Standard **aus**). Es läuft der Ton **einer** Kachel während ihres Ausschnitts: der größten (Spezial-Raster), bei gleich großen der frühesten mit Bild. Kein Nacheinander aller Ausschnitte | viele Tonspuren gleichzeitig ergeben nur Lärm; nacheinander passt nicht zur Länge der Animation (20 Kacheln × 3 s ≠ 3 s). Die große Kachel ist der Blickfang. Ersetzt die frühere Entscheidung „ohne Ton“ nicht als Standard, sondern ergänzt sie als Option |
| 26.11.2026 | Ton nur bei MP4/H.265 (AAC 128 kbit/s), WebM (Opus, sonst Vorbis) und OGV (Vorbis, sonst Opus). Bei GIF, WebP und APNG wird der Schalter ignoriert (Oberfläche: ausgegraut). Videos ohne Ton ergeben ein stummes Sheet ohne Fehler | diese Formate können keinen Ton enthalten |
| 26.11.2026 | Oberfläche: sieben Formatknöpfe in zwei Reihen (Videos MP4, H.265, WebM, OGV; Bilder GIF, WebP, APNG), darunter „Mit Ton“. Kommandozeile `-a mp4\|h265\|webm\|ogv\|gif\|webp\|apng`, `--audio`/`--no-audio` | alles in „Einfach“ wie bisher die drei Formate |

## Offen

| Seit | Punkt | Stand |
|---|---|---|
| 26.11.2026 | Test auf einem echten Mac: Oberfläche (Schriften, Schalter, Symbole, Titelleiste), DMG mit Dock-Icon und Gatekeeper-Dialog. Die CI prüft auf macOS nur Download, Prüfsumme, Signatur und ein erstelltes Sheet | kein Mac vorhanden |
| 26.11.2026 | Code-Signierung (Windows) und Notarisierung (macOS) fehlen; SmartScreen bzw. Gatekeeper warnen beim ersten Start | braucht Zertifikat bzw. Apple-Entwicklerkonto (kostenpflichtig) |
| 26.11.2026 | Icon-Einbindung unter Linux (AppImage, Fenster) und macOS nur über CI geprüft; unter Windows von Vulli bestätigt | Blick auf Linux und macOS fehlt |
| 26.11.2026 | Pakete für Windows arm64, Linux arm64 und Intel-Mac werden nur gebaut, nie gestartet (die Runner können sie nicht ausführen) | Rückmeldung von Nutzern mit solchen Geräten |
| 26.11.2026 | Windows arm64 hat keinen fest hinterlegten ffmpeg-Build (BtbN hebt Builds nur 14 Tage auf, gyan.dev baut kein arm64) | bei einer dauerhaften arm64-Quelle nachziehen |
| 26.11.2026 | Workflow „ffmpeg pins“ hat seit #19 noch keinen neuen Build eingetragen. Seine Commits mit `GITHUB_TOKEN` starten keine CI; wird `main` geschützt, schlägt sein direkter Push fehl. Die Adresse der Liste (`FfmpegPins.RemoteUrl`) enthält den Repo-Namen | ersten echten Lauf beobachten |
| 26.11.2026 | Release-Notizen auf GitHub sind die automatisch erzeugte PR-Liste, nicht der CHANGELOG-Abschnitt. Das Release v0.3.0 heißt „v.0.3.0“ (Tippfehler im Titel) | Notizen beim nächsten Release aus dem CHANGELOG übernehmen; Titel von Hand korrigieren |

## Spätere Ideen (P2/P3, noch ohne Entscheidung)

Aus der Spezifikation, je ein Issue. Erledigt sind aus P2 bereits: Kommandozeile, Live-Vorschau, Deutsch/Englisch, Preset-Import/-Export, „Einfach“/„Erweitert“ statt Optionsdialog, Hintergrundfarbe, animierte Sheets (MP4/WebM/GIF), Videos per URL (#21), schwarze Bilder überspringen (#22), Aufnahmezeitpunkte von Hand (#23), Spezial-Raster (#24), „An Bildschirm anpassen“ (#25), Hintergrundbild (#26), Kachel-Effekte (#27), Kommentartext (#28), Wasserzeichen (#29) und weitere Animationsformate mit Ton (#30). Die Wahl der Render-Engine (P3) entfällt: ffmpeg ist das einzige Backend.

| Prio | Idee | Issue |
|---|---|---|
| P2 | Durchgehende Vorschau-Animation / GIF-Slideshow | #31 |
| P2 | Export der Video-Infos als CSV, XML, WebVTT | #32 |
| P2 | Preset-Datei per Doppelklick öffnen (Dateizuordnung) | #33 |
| P2 | Tastenkürzel | #34 |
| P3 | Untertitel-Infos auslesen | #35 |
| P3 | Baukasten-Editor für eigene Raster | #36 |
| P3 | Mehrere Sheets zu einem „Pack“-Bild | #37 |
| P3 | JSON-Begleitdatei mit Kachel-Zeitpunkten | #38 |
| P3 | Viewer: Video an der Kachel starten | #39 |

## Umfang v0.1.0

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
- CLI `vthumb` mit den wichtigsten Einstellungen als Optionen, alle übrigen über Presets (siehe offene Punkte)
