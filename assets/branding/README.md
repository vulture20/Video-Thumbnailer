# Branding

Eigene Gestaltung für Video-Thumbnailer (Filmstreifen + 3×3-Vorschauraster + Play-Symbol), steht wie das Projekt unter AGPL-3.0.

## Quellen

| Datei | Zweck |
|---|---|
| `icon.svg` | Master-Icon, ab 40 px |
| `icon-small.svg` | pixelgenaue Variante für 20–32 px (ohne Perforation) |
| `icon-16.svg` | pixelgenaue Variante für 16 px (ohne Play-Dreieck) |
| `make_banners.py` | erzeugt `banner.svg` und `social-preview.svg` aus `icon.svg` |
| `build.py` | erzeugt alle Ausgabedateien |

Farben: Film `#2D3340`→`#171A21`, Rand `#4A5366`, Kacheln `#7DD3FC`→`#3B9EEA`, Akzent `#FDBA4D`→`#F26B1D`.
Schrift in Banner/Preview: Inter (SIL OFL), nur in den PNGs verwendet.

## Erzeugte Dateien

| Datei | Verwendung |
|---|---|
| `src/VideoThumbnailer.Gui/Assets/app.ico` | Windows-Programmicon (GUI und `vthumb`), Fenstericon der Oberfläche |
| `assets/branding/app.ico` | dieselbe Datei für andere Zwecke (16–256 px) |
| `assets/branding/app.icns` | macOS (16–1024 px, inkl. @2x), für ein späteres `.app`-Bundle |
| `assets/branding/png/icon-*.png` | PNG in 16–1024 px |
| `packaging/linux/icons/hicolor/…` | Linux-Icon-Theme (16–512 px + scalable) |
| `docs/images/banner.png` | README-Banner (1280×320) |
| `docs/images/social-preview.png` | GitHub-Social-Preview (1280×640), muss in den Repo-Einstellungen hochgeladen werden |

## Neu erzeugen

Benötigt Python 3 mit Pillow, Node.js mit Playwright (Chromium) und die Schrift Inter:

```bash
python3 assets/branding/build.py
```
