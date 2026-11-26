# Changelog

Alle nennenswerten Änderungen an Video-Thumbnailer stehen in dieser Datei.
Format nach [Keep a Changelog](https://keepachangelog.com/de/1.1.0/), Versionierung nach [Semantic Versioning](https://semver.org/lang/de/).
Neue Einträge kommen mit jedem Pull Request unter „Unveröffentlicht“ und wandern beim Release in einen Versionsabschnitt.

## [Unveröffentlicht]

### Hinzugefügt
- Eigenes App-Icon (Filmstreifen mit Vorschauraster und Play-Bild) für Windows (`.ico`, in `VideoThumbnailer.exe` und `vthumb.exe`), macOS (`.icns`) und Linux (hicolor-Icons und `.desktop`-Datei unter `packaging/linux`); Fenstericon in der GUI ([#3])
- README-Banner und Social-Preview-Bild ([#3])
- Diese `CHANGELOG.md` ([#4])
- Oberfläche auf Deutsch und Englisch: folgt standardmäßig der Systemsprache, umschaltbar in den Einstellungen, ohne Neustart ([#5])
- Kommandozeile auf Deutsch und Englisch (Hilfe, Meldungen, Beschreibungen der Basis-Presets); Sprache über `--lang` oder `VTHUMB_LANG`, sonst wie in der GUI eingestellt bzw. Systemsprache ([#6])
- Texte im Sheet auf Deutsch und Englisch: Standard-Info-Kopf („Größe:“, „Dauer:“), „kein Bild“, „keine“/„k. A.“ und Dezimalkomma; Sprache pro Preset wählbar („Wie Oberfläche“, Deutsch, Englisch), per CLI `--sheet-lang` ([#6])
- Meldungen der Verarbeitung und des ffmpeg-Downloads in der Oberfläche übersetzt ([#6])

### Geändert
- Einstellungsspalte etwas breiter, Beschriftungen brechen bei Bedarf um ([#5])
- Presets speichern den Standard-Info-Kopf nicht mehr als festen Text, damit er der Sheet-Sprache folgt; ältere Presets mit der englischen Standardvorlage werden genauso behandelt ([#6])

## [0.1.0] – 2026-11-26

Erste Version.

### Hinzugefügt
- Kontaktbögen (Thumbnail-Sheets) aus allen Formaten, die ffmpeg lesen kann, inkl. 10-Bit/HDR (mit Tonemapping), AV1, gedrehten und anamorphen Videos ([#1])
- Raster aus Spalten × Zeilen mit Gesamt- oder Kachelbreite, Abstand und Rand; automatische Kachelhöhe; Spalten/Zeilen-Tausch für Hochkant-Videos; Reihenfolge zeilenweise, spaltenweise oder zufällig; Anfang/Ende überspringen ([#1])
- Zeitstempel in jeder Kachel (5 Positionen, Farbe, Deckkraft, Kontur, Schatten) und Info-Kopf mit frei konfigurierbaren Platzhaltern (4 Positionen) ([#1])
- Ausgabe als JPG, PNG oder WebP, optional jede Aufnahme als Einzelbild; Ausgabeordner (mit Ordnerstruktur), neben dem Video oder in einem Unterordner; Dateinamen-Muster; Überspringen/Überschreiben/Umbenennen vorhandener Dateien ([#1])
- Warteschlange mit paralleler Verarbeitung, Pause, Abbruch, Fortschritt und Statistik ([#1])
- Presets als JSON (`*.vtpreset.json`) mit 4 eingebauten Presets: Standard, Compact, Detailed, Web ([#1])
- ffmpeg-Verwaltung: Suche (eigener Ordner, heruntergeladen, PATH), Download nach Rückfrage von BtbN/FFmpeg-Builds bzw. ffmpeg.martin-riedl.de mit SHA-256-Prüfung, atomare Installation und Update-Erkennung ([#1])
- Kommandozeile `vthumb` mit allen Einstellungen als Optionen sowie `presets …` und `ffmpeg …` ([#1])
- Avalonia-GUI mit Drag & Drop, Einstellungen, Preset-Verwaltung und ffmpeg-Status ([#1])
- Portabler Modus über `portable.txt` oder `VIDEO_THUMBNAILER_HOME` ([#1])
- CI für Windows, Linux und macOS; Release-Workflow mit selbstständigen Einzeldateien für win/linux/osx × x64/arm64 ([#1])
- `docs/DECISIONS.md` mit Entscheidungen und offenen Punkten ([#1])

### Geändert
- Release-Workflow hängt die Dateien auch an ein im Browser angelegtes Release an ([#2])

[Unveröffentlicht]: https://github.com/vulture20/Video-Thumbnailer/compare/v0.1.0...HEAD
[0.1.0]: https://github.com/vulture20/Video-Thumbnailer/releases/tag/v0.1.0
[#1]: https://github.com/vulture20/Video-Thumbnailer/pull/1
[#2]: https://github.com/vulture20/Video-Thumbnailer/pull/2
[#3]: https://github.com/vulture20/Video-Thumbnailer/pull/3
[#4]: https://github.com/vulture20/Video-Thumbnailer/pull/4
[#5]: https://github.com/vulture20/Video-Thumbnailer/pull/5
[#6]: https://github.com/vulture20/Video-Thumbnailer/pull/6
