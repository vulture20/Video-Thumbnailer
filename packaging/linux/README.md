# Linux-Integration (optional)

Am einfachsten ist das AppImage aus den Releases (`make-appimage.sh` und `AppRun` in diesem Ordner, gebaut vom Release-Workflow). Die Release-ZIPs enthalten nur die Programmdateien. Wer sie ins Startmenü bringen möchte:

```bash
# Programmdatei irgendwo in den PATH legen, z. B.
install -Dm755 VideoThumbnailer ~/.local/bin/VideoThumbnailer

# Icons und Starter (aus diesem Ordner)
cp -r icons/hicolor ~/.local/share/icons/
install -Dm644 video-thumbnailer.desktop ~/.local/share/applications/video-thumbnailer.desktop
gtk-update-icon-cache ~/.local/share/icons/hicolor 2>/dev/null || true
```

Die Icons werden aus `assets/branding` erzeugt (`python3 assets/branding/build.py`).
