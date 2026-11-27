# Linux-Integration (optional)

Am einfachsten ist das AppImage aus den Releases (`make-appimage.sh` und `AppRun` in diesem Ordner, gebaut vom Release-Workflow). Die Release-ZIPs enthalten nur die Programmdateien. Wer sie ins Startmenü bringen möchte:

```bash
# Programmdatei irgendwo in den PATH legen, z. B.
install -Dm755 VideoThumbnailer ~/.local/bin/VideoThumbnailer

# Icons und Starter (aus diesem Ordner)
cp -r icons/hicolor ~/.local/share/icons/
install -Dm644 video-thumbnailer.desktop ~/.local/share/applications/video-thumbnailer.desktop
gtk-update-icon-cache ~/.local/share/icons/hicolor 2>/dev/null || true

# Preset-Dateien (*.vtpreset) per Doppelklick öffnen
xdg-mime install --novendor video-thumbnailer-preset.xml
xdg-mime default video-thumbnailer.desktop application/x-vtpreset+json
update-desktop-database ~/.local/share/applications 2>/dev/null || true
```

Beim AppImage wirkt die Dateizuordnung erst, wenn es ins System eingebunden ist (z. B. mit AppImageLauncher oder appimaged; der MIME-Typ liegt unter `usr/share/mime/packages`). Ohne Einbindung lässt sich eine Preset-Datei über „Öffnen mit“ und das AppImage öffnen.

Die Icons werden aus `assets/branding` erzeugt (`python3 assets/branding/build.py`).
