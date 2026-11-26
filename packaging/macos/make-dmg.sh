#!/usr/bin/env bash
# Builds Video-Thumbnailer.app from a publish folder and packs it into a DMG (unsigned, ad-hoc code signature).
#   packaging/macos/make-dmg.sh <publish dir> <version> <output.dmg>
set -euo pipefail
SRC=$1 VERSION=$2 OUT=$3
HERE=$(cd "$(dirname "$0")" && pwd)
ROOT=$(cd "$HERE/../.." && pwd)
WORK=$(mktemp -d)
APP="$WORK/dmg/Video-Thumbnailer.app"

mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources"
cp "$SRC/VideoThumbnailer" "$SRC/vthumb" "$APP/Contents/MacOS/"
# Anything else the publish produced (native libraries, licences) goes next to the binaries.
find "$SRC" -mindepth 1 -maxdepth 1 ! -name VideoThumbnailer ! -name vthumb -exec cp -R {} "$APP/Contents/MacOS/" \;
cp "$ROOT/assets/branding/app.icns" "$APP/Contents/Resources/app.icns"
sed -e "s/@VERSION@/$VERSION/" -e "s/@SHORT_VERSION@/${VERSION%%-*}/" "$HERE/Info.plist" > "$APP/Contents/Info.plist"
plutil -lint "$APP/Contents/Info.plist"
# Apple Silicon only runs signed code; an ad-hoc signature is enough for that (not a Developer ID, not notarized).
codesign --force --deep --sign - "$APP"
codesign --verify --deep --strict "$APP"

ln -s /Applications "$WORK/dmg/Applications"
cp "$ROOT/LICENSE" "$WORK/dmg/LICENSE.txt"
# hdiutil sometimes fails with "Resource busy" on CI runners; retry a few times.
for i in 1 2 3 4; do
  hdiutil create -volname "Video-Thumbnailer" -srcfolder "$WORK/dmg" -fs HFS+ -format UDZO -ov "$OUT" && break
  [ "$i" = 4 ] && exit 1
  sleep $((i * 5))
done
rm -rf "$WORK"
