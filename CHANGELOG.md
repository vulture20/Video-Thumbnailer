# Changelog

Alle nennenswerten Änderungen an Video-Thumbnailer stehen in dieser Datei.
Format nach [Keep a Changelog](https://keepachangelog.com/de/1.1.0/), Versionierung nach [Semantic Versioning](https://semver.org/lang/de/).
Neue Einträge kommen mit jedem Pull Request unter „Unveröffentlicht“ und wandern beim Release in einen Versionsabschnitt.

## [Unveröffentlicht]

### Hinzugefügt
- Untertitel-Infos: Untertitelspuren im Video und Untertiteldateien daneben (`Film.srt`, `Film.de.srt`, `Film.en.forced.ass`, `.vtt`, `.sup`, `.idx/.sub`) werden erkannt, mit Sprache, Format, „erzwungen“, „SDH“ und „extern“. Neue Platzhalter `{subtitles}` (Zusammenfassung), `{slang}` (Sprachen) und `{subtracks}` (Anzahl), auch als Spalten im CSV/XML-Export ([#50], #35)
- Raster-Editor („Raster bearbeiten…“ unter dem Layout): große, kleine und leere Kacheln per Ziehen auf einem Feldraster anordnen, freie Felder bleiben Hintergrund. Übernehmen oder als eigenes Raster `<Name>.vtgrid` im Einstellungsordner speichern; eigene Raster stehen neben den eingebauten in der Layout-Auswahl, auf der Kommandozeile per `--layout <Name>` und öffnen sich per Doppelklick ([#50], #36)
- Pack-Bild: ein Übersichtsbild aus vielen Videos (z. B. einer Staffel), je Video eine Zeile mit Name, Dauer, Auflösung, Größe und so vielen Bildern, wie das Raster Spalten hat. Zu hohe Bilder werden auf `pack-1`, `pack-2` … aufgeteilt (WebP höchstens 16383 px). Oberfläche „⋯“ → „Pack-Bild erstellen…“, Kommandozeile `vthumb pack <Videos/Ordner> -o pack.jpg` ([#50], #37)
- Begleitdatei `<Name>.vtsheet` (offenes JSON, beschrieben in `docs/vtsheet.md`) mit Video-Pfad (relativ und absolut), Sheet-Größe und je Kachel Rechteck und Zeitpunkt. Standard aus, unter „Erweitert“ → „Ausgabe“ bzw. `--sidecar`. Zeitpunkte von Hand werden daraus wiederhergestellt, wenn das Video erneut in die Warteschlange kommt (Kommandozeile: ohne `--times`) ([#50], #38)
- Viewer: zeigt ein Sheet mit Begleitdatei, markiert die Kachel unter der Maus und startet das Video per Klick an dieser Stelle in mpv, VLC, MPC-HC/MPC-BE, PotPlayer oder einem eigenen Befehl (`{file}`, `{seconds}`, `{ms}`, `{time}`); ohne passenden Player öffnet der Systemstandard das Video am Anfang. „In den Papierkorb…“ verschiebt das Video nach Rückfrage, optional mit Sheet und Begleitdateien, und löscht nie endgültig. Öffnen per Doppelklick auf einen erledigten Eintrag der Warteschlange, „⋯“ → „Sheet öffnen…“ oder Doppelklick auf eine `.vtsheet` ([#50], #39)
- Dateizuordnung für `.vtsheet` und `.vtgrid` im Windows-Installer (abwählbar), in der macOS-App und als MIME-Typen für Linux ([#50], #36, #39)
- Vorschau-Clip („Trailer“): kurze Ausschnitte aus gleichmäßig verteilten Stellen des Videos nacheinander, mit Überblendung (Standard 0,5 s, 0 = harter Schnitt), Zeit jedes Ausschnitts in der Ecke und optional mit Ton, als MP4, H.265, WebM, OGV, GIF, WebP oder APNG. Standard 12 × 2,5 s, 640 px, höchstens 2 Minuten. Zusätzlich zum Sheet oder allein; Dateiname `<Name>.preview.<Endung>`, beim Ordner-Scan übersprungen. Oberfläche unter „Ausgabe“, Kommandozeile `--trailer <Format>`, `--trailer-only`, `--trailer-clips`, `--trailer-seconds`, `--trailer-fade`, `--trailer-size`, `--trailer-fps`, `--trailer-timestamps`, `--trailer-audio` ([#49], #31)
- Technische Daten aller Videos als eine CSV- oder XML-Datei je Lauf (Spalten wie die Platzhalter, Zahlen ohne Einheit): Oberfläche „⋯“ → „Video-Infos exportieren…“, Kommandozeile `--export-info <Datei>` und `--info-only` ([#49], #32)
- WebVTT-Vorschaubilder für Web-Player: `<Name>.thumbnails.vtt` mit Sprite `<Name>.thumbnails.jpg` aus den aufgenommenen Bildern (Breite einstellbar). Oberfläche unter „Erweitert“ → „Ausgabe“, Kommandozeile `--webvtt`, `--webvtt-width` ([#49], #32)
- Preset-Dateien per Doppelklick öffnen: Dateizuordnung für `.vtpreset` im Windows-Installer (pro Benutzer, abwählbar), in der macOS-App und als MIME-Typ für Linux (im AppImage nach Einbindung). Ein schon laufendes Programm übernimmt Presets und Videos aus einem zweiten Start, statt ein zweites Fenster zu öffnen; macOS-„Datei öffnen“-Ereignisse werden ausgewertet ([#49], #33)
- Tastenkürzel: Strg/Cmd+O, Strg/Cmd+Umschalt+O, Strg/Cmd+U, Strg/Cmd+Eingabe oder F5, Strg/Cmd+P, Esc, Entf und Strg/Cmd+A in der Warteschlange, Strg/Cmd+E (Einfach/Erweitert), Strg/Cmd+L (Live-Vorschau), Strg/Cmd+B (Warteschlange). Die Kürzel stehen in den Tooltips; fehlende Tooltips ergänzt ([#49], #34)

### Geändert
- Doppelklick auf einen erledigten Eintrag der Warteschlange öffnet das Sheet im Viewer, wenn es eine Begleitdatei hat; sonst wie bisher im Systemprogramm ([#50], #39)
- Exportierte Presets heißen jetzt `<Name>.vtpreset` statt `<Name>.vtpreset.json` (gleicher Inhalt), weil Windows und macOS nur die letzte Endung kennen. Alte Dateien lassen sich weiter importieren ([#49], #33)

### Behoben
- Der Knopf mit der Weltkugel in der Warteschlange ist so hoch wie „+ Dateien…“ und „+ Ordner…“ ([#49])
- Eine beim Start übergebene Preset-Datei wurde zwar ausgewählt, ihre Einstellungen aber nicht geladen ([#49], #33)

## [0.6.0] – 2026-11-26

Videos per URL, Spezial-Raster mit großen Kacheln, Kachel-Effekte, Wasserzeichen, Kommentar und Hintergrundbild, Aufnahmezeitpunkte von Hand, schwarze Bilder überspringen, weitere Animationsformate mit Ton, Farb- und Schriftauswahl sowie alle Einstellungen auf der Kommandozeile.

### Hinzugefügt
- Kachel-Effekte unter „Erweitert“: Rahmen je Kachel (innen gezeichnet, Farbe und Breite), Schlagschatten (fällt in den Abstand), Graustufen, abgerundete oder abgeschnittene Ecken mit einstellbarer Größe und ein Rahmen um das ganze Sheet. Alles standardmäßig aus; gilt auch für animierte Sheets und die Live-Vorschau. Kommandozeile `--tile-border`, `--tile-border-color`, `--tile-shadow`, `--tile-shadow-color`, `--tile-shadow-offset`, `--tile-shadow-blur`, `--grayscale`, `--corners square|rounded|cut`, `--corner-radius`, `--sheet-border`, `--sheet-border-color` ([#47], #27)
- Eigener Kommentartext im Info-Kopf (z. B. „Urlaub 2026“, auch mehrzeilig): steht unter den Zeilen des Info-Kopfs oder dort, wo die Vorlage `{comment}` enthält; gespeichert im Preset, auf der Kommandozeile `--comment` ([#47], #28)
- Wasserzeichen als Text oder Bild (PNG mit Transparenz) mit Position (Ecken, Mitte, wiederholt), Größe relativ zur Sheet-Breite und Deckkraft, einmal über dem Sheet oder in jeder Kachel; auch in animierten Sheets. Kommandozeile `--watermark`, `--watermark-image`, `--watermark-pos`, `--watermark-size`, `--watermark-opacity`, `--watermark-color`, `--watermark-per-tile` ([#47], #29)
- Animierte Sheets zusätzlich als MP4 mit H.265, animiertes WebP, APNG und OGV (Theora); optional mit dem Ton der größten (sonst ersten) Kachel bei MP4, H.265, WebM und OGV. Fehlt dem ffmpeg ein Encoder, sagt die Meldung welcher. Kommandozeile `-a h265|webp|apng|ogv`, `--audio` ([#47], #30)
- Spezial-Raster mit großen und kleinen Kacheln: neue Kachelreihe „Layout“ mit „Raster“ und vier Vorlagen (Groß oben, Groß Mitte, Zwei groß, Groß zuerst). Große Kacheln werden in voller Größe aufgenommen und kommen in der Zeitfolge dort dran, wo sie beginnen. Eigene Vorlagen als offenes JSON im Preset (`template`: Felder und Kacheln); Kommandozeile `--layout big-top|big-center|two-big|big-first|<Datei>|grid` ([#46], #24)
- Bildgröße „Bildschirmbreite“ und „An Bildschirm anpassen“: das Sheet wird so breit wie der Bildschirm, auf dem das Fenster ist, bzw. so groß, dass es ganz ohne Scrollen darauf passt (physische Pixel ohne Taskleiste). Im Preset steht der Modus, die feste Breite bleibt Ersatz. Kommandozeile `--fit-screen off|width|both` und `--screen BxH`, sonst der zuletzt von der Oberfläche gesehene Bildschirm ([#46], #25)
- Hintergrundbild hinter den Kacheln (unter „Erweitert“): füllen, strecken, kacheln oder zentriert, mit Deckkraft über der Hintergrundfarbe (Standard 0,5) für lesbare Zeitstempel; fehlt das Bild, gibt es einen Hinweis statt eines Fehlers. Beim Preset-Export wird ein Bild neben der Datei relativ gespeichert. Kommandozeile `--background-image`, `--background-mode`, `--background-opacity` ([#46], #26)
- Videos per HTTP(S)-Adresse, ohne sie herunterzuladen: ffmpeg liest nur die Stellen um die Aufnahmezeitpunkte (Range-Anfragen, 30 s Zeitüberschreitung, automatisches Wiederverbinden). Oberfläche: Knopf mit Weltkugel in der Warteschlange oder Adresse aus dem Browser hineinziehen; Kommandozeile: URL statt Datei. Sheets landen im Ausgabeordner (sonst Bilder-Ordner bzw. aktueller Ordner); Name und `{folder}`/`{path}` kommen aus der Adresse, ohne Zugangsdaten und Query ([#45], #21)
- Schwarze oder einfarbige Bilder (Abblende, Schnitt) werden durch das nächste gute Bild bis ±2 s davor/danach ersetzt, auch bei animierten Sheets und in der Live-Vorschau; der Zeitstempel zeigt den tatsächlichen Zeitpunkt. Standardmäßig an, abschaltbar unter „Erweitert“, `--no-skip-black`, Suchbereich `--black-search` ([#45], #22)
- Aufnahmezeitpunkte von Hand: Kachel in der Live-Vorschau anklicken, Zeitpunkt per Schieberegler, Eingabe oder bildgenau wählen (die Vorschau zieht sofort mit), bei animierten Sheets auch die Länge des Ausschnitts; „✎ n von Hand ✕“ setzt alle zurück. Kommandozeile: `--times "1:00,-,5:30+2"`. Die Zeitpunkte gelten je Video, nicht im Preset ([#45], #23)
- Farbwähler: Klick auf das Farbmuster neben einem Farbfeld öffnet Spektrum, Palette und Regler mit Deckkraft; die Hex-Eingabe bleibt ([#42])
- Schriftauswahl: „…“ neben der Schriftart öffnet eine durchsuchbare Liste der installierten Schriften, jede in ihrer eigenen Schrift, mit Vorschau von Info-Kopf und Zeitstempel ([#42])
- Kommandozeile kann jetzt jede Preset-Einstellung setzen: Zeitstempel (`--timestamp-size`, `--timestamp-color`, `--timestamp-opacity`, `--timestamp-bold`, `--timestamp-padding`, `--outline-color`, `--outline-width`, `--no-outline`, `--shadow`/`--no-shadow`, `--shadow-color`, `--shadow-offset`, `--milliseconds`), Info-Kopf (`--title-size`, `--header-size`, `--title-color`, `--header-color`, `--line-spacing`), Einzelbilder (`--frames-folder`, `--no-frames`) sowie `--timestamps`, `--header`, `--portrait-swap`, `--no-keep-structure` und `--description` zum Zurückschalten bzw. für `--save-preset` ([#40])

### Geändert
- Release-Notizen auf GitHub kommen automatisch aus dem CHANGELOG-Abschnitt der Version, auch wenn das Release im Browser angelegt wird ([#48])
- Dokumentation auf den Stand von v0.5.0 gebracht: erledigte und doppelte offene Punkte in `docs/DECISIONS.md` bereinigt, überholte Entscheidungen markiert, README (Funktionen, Inhalt der ZIPs, Aufbau) aktualisiert; spätere Ideen aus der Spezifikation als Issues #21–#39 ([#41])
- Farben auf der Kommandozeile werden geprüft: ungültige Werte (auch bei `--background`) brechen mit einer Meldung ab, statt still die Standardfarbe zu nehmen ([#40])
- README-Screenshots neu: aktuelle Oberfläche mit echten Bildern, Schalter „Animiertes Sheet“, Farbfeldern in „Erweitert“ (dunkel) und eingeklappter Warteschlange im schmalen Fenster; erzeugt per Test `Readme_screenshots` ([#44])

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

[Unveröffentlicht]: https://github.com/vulture20/Video-Thumbnailer/compare/v0.6.0...HEAD
[0.6.0]: https://github.com/vulture20/Video-Thumbnailer/compare/v0.5.0...v0.6.0
[0.5.0]: https://github.com/vulture20/Video-Thumbnailer/compare/v0.4.0...v0.5.0
[0.4.0]: https://github.com/vulture20/Video-Thumbnailer/compare/v0.3.0...v0.4.0
[0.3.0]: https://github.com/vulture20/Video-Thumbnailer/compare/v0.2.0...v0.3.0
[0.2.0]: https://github.com/vulture20/Video-Thumbnailer/compare/v0.1.0...v0.2.0
[0.1.0]: https://github.com/vulture20/Video-Thumbnailer/releases/tag/v0.1.0
[#1]: https://github.com/vulture20/Video-Thumbnailer/pull/1
[#2]: https://github.com/vulture20/Video-Thumbnailer/pull/2
[#47]: https://github.com/vulture20/Video-Thumbnailer/pull/47
[#42]: https://github.com/vulture20/Video-Thumbnailer/pull/42
[#46]: https://github.com/vulture20/Video-Thumbnailer/pull/46
[#45]: https://github.com/vulture20/Video-Thumbnailer/pull/45
[#44]: https://github.com/vulture20/Video-Thumbnailer/pull/44
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
[#41]: https://github.com/vulture20/Video-Thumbnailer/pull/41
[#40]: https://github.com/vulture20/Video-Thumbnailer/pull/40
[#49]: https://github.com/vulture20/Video-Thumbnailer/pull/49
[#50]: https://github.com/vulture20/Video-Thumbnailer/pull/50
[#48]: https://github.com/vulture20/Video-Thumbnailer/pull/48
