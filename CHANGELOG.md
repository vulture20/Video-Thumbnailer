# Changelog

Alle nennenswerten Änderungen an Video-Thumbnailer stehen in dieser Datei.
Format nach [Keep a Changelog](https://keepachangelog.com/de/1.1.0/), Versionierung nach [Semantic Versioning](https://semver.org/lang/de/).
Neue Einträge kommen mit jedem Pull Request unter „Unveröffentlicht“ und wandern beim Release in einen Versionsabschnitt.

## [Unveröffentlicht]

## [0.5.0] – 2026-11-26

Installer für Windows, AppImage für Linux und DMG für macOS, sichererer ffmpeg-Download mit fest hinterlegten Prüfsummen, neue Farb- und Zeitstempel-Optionen unter „Erweitert“.

### Hinzugefügt
- Installer und Pakete, alle unsigniert und vom Release-Workflow gebaut: Windows-Setup (`…-setup.exe`, Startmenü-Eintrag, optional Desktop-Symbol, ohne Administratorrechte, Deinstallation über die Systemsteuerung), Linux-AppImage (Oberfläche, mit `vthumb` als erstem Argument die Kommandozeile) und macOS-DMG mit `Video-Thumbnailer.app` samt Icon im Dock. Die ZIPs gibt es weiterhin; `checksums.sha256` enthält alle Dateien ([#19])
- Workflow „ffmpeg pins“: sucht wöchentlich nach neueren ffmpeg-Builds, prüft sie und trägt sie direkt in die Liste im Repo ein; die App holt sich die Liste beim Update-Check, neue ffmpeg-Versionen brauchen also kein neues Release ([#19])
- Feinoptionen unter „Erweitert“: Konturfarbe und Schattenfarbe der Zeitstempel, Millisekunden im Zeitstempel (00:12:34.567) sowie Titel- und Textfarbe des Info-Kopfs ([#18])
- Farbfelder zeigen ein Farbmuster neben dem Wert (mit Schachbrett für Transparenz); ungültige Eingaben werden mit „?“ markiert ([#18])

### Geändert
- Zeitstempel unter „Erweitert“: Farbe und Deckkraft stehen jetzt in eigenen Zeilen ([#18])
- ffmpeg-Download sicherer: Installiert wird ein fest hinterlegter Build, dessen SHA-256-Prüfsumme in diesem Repo steht (Liste wird von GitHub geladen, eingebaute Kopie als Rückfall) statt auf dem Download-Server; unter macOS wird zusätzlich die Code-Signatur des Herausgebers (Team-ID) geprüft. `vthumb ffmpeg install --latest` lädt auf Wunsch weiterhin den neuesten Build ([#19])
- ffmpeg-Quellen: Windows x64 jetzt von gyan.dev, Linux von ffmpeg.martin-riedl.de (beide halten ältere Versionen dauerhaft bereit, BtbN nur 14 Tage). Bestehende Installationen bekommen den Wechsel als ffmpeg-Update angeboten ([#19])
- CI prüft den echten ffmpeg-Download jetzt auf Windows, Linux und macOS ([#19])
- Copyright und Herausgeber in den Programmdateien (EXE-Eigenschaften): „Copyright (C) 2026 Thorsten Schröpel“ statt „Video-Thumbnailer contributors“; ebenso in der README ([#17])

## [0.4.0] – 2026-11-26

Animierte Sheets (MP4/WebM/GIF), einklappbare Warteschlange, kleinerer ffmpeg-Download unter Windows, abschaltbare Vorschau, ffmpeg-Download auf macOS repariert.

### Hinzugefügt
- Animierte Sheets als MP4, WebM oder GIF: jede Kachel spielt einen kurzen Ausschnitt ab ihrem Zeitpunkt ab, Zeitstempel laufen mit; Schalter „Animiertes Sheet“ in der Ausgabe, Länge je Ausschnitt und Bilder pro Sekunde einstellbar; CLI `-a/--animated mp4|webm|gif`, `--clip-seconds`, `--fps`, `--still` ([#15])
- Animierte Sheets heißen `<Name>.sheet.mp4` usw. und werden beim Durchsuchen von Ordnern übersprungen ([#15])
- CI auf macOS erstellt zusätzlich ein animiertes MP4-Sheet mit dem heruntergeladenen ffmpeg ([#15])
- Schmale Fenster: Die Warteschlange klappt unter 1120 px Breite automatisch zu einer schmalen Leiste ein (Anzahl, Hinzufügen, Status-Punkte) und lässt sich jederzeit von Hand mit « / » ein- und ausklappen; Mindestbreite jetzt 820 statt 1040 px ([#14])
- Test mit echten HDR10- und HLG-Videos: Tonemapping liefert Farben und Helligkeit nahe am SDR-Bild ([#14])
- Live-Vorschau abschaltbar (Schalter über der Vorschau, wird gespeichert); ausgeschaltet läuft dafür kein ffmpeg ([#12])

### Geändert
- ffmpeg-Download unter Windows deutlich kleiner: „shared“-Build von BtbN (ca. 90 statt 198 MB Download, ca. 200 statt 340 MB auf der Platte). Bestehende Installationen bekommen den Wechsel als ffmpeg-Update angeboten ([#14])
- CI prüft auf macOS den echten ffmpeg-Download (ffmpeg.martin-riedl.de) und erstellt damit ein Test-Sheet ([#13])

### Behoben
- macOS: ffmpeg-Download schlug mit „404“ fehl, weil der Download-Dienst keine HEAD-Anfragen beantwortet; Fehlermeldungen nennen jetzt die betroffene Adresse ([#13])
- Vorschau erkennt, wenn sich eine Videodatei auf der Festplatte geändert hat (Größe oder Änderungszeit), und liest sie neu ([#12])

## [0.3.0] – 2026-11-26

Vorschau mit echten Bildern des gewählten Videos; Versionsnummer in den Release-Dateien. (Veröffentlicht vom Stand nach #11; #12–#14 folgen im nächsten Release.)

### Hinzugefügt
- Vorschau mit echten Bildern und echten Videodaten des gewählten Videos: Bilder von genau den Zeitpunkten des fertigen Bogens, Info-Kopf mit Größe, Dauer, Auflösung und Codecs. Lädt im Hintergrund und füllt sich nach und nach („Lade Bilder… n von m“); ohne ffmpeg oder bei nicht lesbaren Dateien weiter mit Beispielbildern ([#11])

### Geändert
- Release-Dateien tragen die Version im Namen, z. B. `Video-Thumbnailer-0.3.0-win-x64.zip` (auch der Ordner im ZIP) ([#10])

## [0.2.0] – 2026-11-26

Zweisprachig (Deutsch/Englisch), Modi „Einfach“/„Erweitert“, Dark-Mode und neue Oberfläche „Leuchttisch“ mit Live-Vorschau.

### Hinzugefügt
- Eigenes App-Icon (Filmstreifen mit Vorschauraster und Play-Bild) für Windows (`.ico`, in `VideoThumbnailer.exe` und `vthumb.exe`), macOS (`.icns`) und Linux (hicolor-Icons und `.desktop`-Datei unter `packaging/linux`); Fenstericon in der GUI ([#3])
- README-Banner und Social-Preview-Bild ([#3])
- Diese `CHANGELOG.md` ([#4])
- Oberfläche auf Deutsch und Englisch: folgt standardmäßig der Systemsprache, umschaltbar in den Einstellungen, ohne Neustart ([#5])
- Kommandozeile auf Deutsch und Englisch (Hilfe, Meldungen, Beschreibungen der Basis-Presets); Sprache über `--lang` oder `VTHUMB_LANG`, sonst wie in der GUI eingestellt bzw. Systemsprache ([#6])
- Texte im Sheet auf Deutsch und Englisch: Standard-Info-Kopf („Größe:“, „Dauer:“), „kein Bild“, „keine“/„k. A.“ und Dezimalkomma; Sprache pro Preset wählbar („Wie Oberfläche“, Deutsch, Englisch), per CLI `--sheet-lang` ([#6])
- Meldungen der Verarbeitung und des ffmpeg-Downloads in der Oberfläche übersetzt ([#6])
- Einstellungen in den Modi „Einfach“ (Standard, nur das Wichtigste) und „Erweitert“ (alle Einstellungen) ([#7])
- Dark-Mode: Darstellung „Wie System“, „Hell“ oder „Dunkel“, wirkt sofort ([#7])
- Live-Vorschau des Kontaktbogens in der Mitte des Fensters: wird mit dem echten Renderer gezeichnet (Raster, Info-Kopf, Zeitstempel, Farben), nur die Bilder sind Platzhalter; darunter Bildanzahl und Größe des Bogens in Pixeln ([#8])
- Presets als Kacheln mit kleinem Rastersymbol ([#8])

### Geändert
- Einstellungsspalte etwas breiter, Beschriftungen brechen bei Bedarf um ([#5])
- Presets speichern den Standard-Info-Kopf nicht mehr als festen Text, damit er der Sheet-Sprache folgt; ältere Presets mit der englischen Standardvorlage werden genauso behandelt ([#6])
- Neue Oberfläche „Leuchttisch“: Warteschlange links, Vorschau in der Mitte, Einstellungen rechts, Fußleiste mit ffmpeg-Status, Fortschritt, Pause, Abbrechen und Start; eigene Farben aus dem Icon (Blau für Auswahl, Orange nur für Start) in hell und dunkel ([#8])
- Warteschlange: nur noch „+ Dateien…“, „+ Ordner…“ und „Unterordner einbeziehen“ sichtbar; Leeren, Wiederholen und Sortieren im Menü „⋯“; farbiger Status je Video ([#8])
- Sprache und Darstellung im Zahnrad-Menü oben rechts statt in der Einstellungsspalte; „Einfach“/„Erweitert“ als Umschalter ([#8])
- Ausgabe: Format als drei Knöpfe (JPG/PNG/WebP), Qualität als Schieberegler (bei PNG gesperrt); Zeitstempel und Info-Kopf als Schalter ([#8])
- ffmpeg-Status und Download-Knopf im einfachen Modus in der Fußleiste; die ausführlichen ffmpeg-Einstellungen nur unter „Erweitert“ ([#8])

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

[Unveröffentlicht]: https://github.com/vulture20/Video-Thumbnailer/compare/v0.5.0...HEAD
[0.5.0]: https://github.com/vulture20/Video-Thumbnailer/compare/v0.4.0...v0.5.0
[0.4.0]: https://github.com/vulture20/Video-Thumbnailer/compare/v0.3.0...v0.4.0
[0.3.0]: https://github.com/vulture20/Video-Thumbnailer/compare/v0.2.0...v0.3.0
[0.2.0]: https://github.com/vulture20/Video-Thumbnailer/compare/v0.1.0...v0.2.0
[0.1.0]: https://github.com/vulture20/Video-Thumbnailer/releases/tag/v0.1.0
[#1]: https://github.com/vulture20/Video-Thumbnailer/pull/1
[#2]: https://github.com/vulture20/Video-Thumbnailer/pull/2
[#3]: https://github.com/vulture20/Video-Thumbnailer/pull/3
[#4]: https://github.com/vulture20/Video-Thumbnailer/pull/4
[#5]: https://github.com/vulture20/Video-Thumbnailer/pull/5
[#6]: https://github.com/vulture20/Video-Thumbnailer/pull/6
[#7]: https://github.com/vulture20/Video-Thumbnailer/pull/7
[#8]: https://github.com/vulture20/Video-Thumbnailer/pull/8
[#11]: https://github.com/vulture20/Video-Thumbnailer/pull/11
[#12]: https://github.com/vulture20/Video-Thumbnailer/pull/12
[#14]: https://github.com/vulture20/Video-Thumbnailer/pull/14
[#10]: https://github.com/vulture20/Video-Thumbnailer/pull/10
[#13]: https://github.com/vulture20/Video-Thumbnailer/pull/13
[#15]: https://github.com/vulture20/Video-Thumbnailer/pull/15
[#18]: https://github.com/vulture20/Video-Thumbnailer/pull/18
[#17]: https://github.com/vulture20/Video-Thumbnailer/pull/17
[#19]: https://github.com/vulture20/Video-Thumbnailer/pull/19
