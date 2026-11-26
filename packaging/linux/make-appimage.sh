#!/usr/bin/env bash
# Builds an AppImage from a publish folder (unsigned).
#   packaging/linux/make-appimage.sh <publish dir> <x86_64|aarch64> <output.AppImage>
# Needs appimagetool (x86_64) in PATH or as $APPIMAGETOOL; it fetches the runtime for the target architecture.
set -euo pipefail
SRC=$1 ARCH=$2 OUT=$3
HERE=$(cd "$(dirname "$0")" && pwd)
APPDIR=$(mktemp -d)/Video-Thumbnailer.AppDir

mkdir -p "$APPDIR/usr/bin" "$APPDIR/usr/share/applications" "$APPDIR/usr/share/icons"
cp -R "$SRC"/. "$APPDIR/usr/bin/"
cp -R "$HERE/icons/hicolor" "$APPDIR/usr/share/icons/"
cp "$HERE/video-thumbnailer.desktop" "$APPDIR/usr/share/applications/"
cp "$HERE/video-thumbnailer.desktop" "$APPDIR/"
cp "$HERE/icons/hicolor/256x256/apps/video-thumbnailer.png" "$APPDIR/video-thumbnailer.png"
ln -s video-thumbnailer.png "$APPDIR/.DirIcon"
install -m755 "$HERE/AppRun" "$APPDIR/AppRun"

ARCH=$ARCH APPIMAGE_EXTRACT_AND_RUN=1 "${APPIMAGETOOL:-appimagetool}" --no-appstream "$APPDIR" "$OUT"
