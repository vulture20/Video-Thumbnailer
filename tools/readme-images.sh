#!/usr/bin/env bash
# Regenerates the pictures in docs/images that README.md shows:
#   example-*.{jpg,webp}  sheets made by the command line from a colourful demo reel
#   gui*.png, grid-editor.png, viewer.png  screenshots of the GUI (test Readme_screenshots, headless)
# Needs the .NET 10 SDK and ffmpeg/ffprobe in PATH. Usage: tools/readme-images.sh   (from anywhere)
set -euo pipefail
root=$(cd "$(dirname "$0")/.." && pwd)
img="$root/docs/images"
work=$(mktemp -d)
trap 'rm -rf "$work"' EXIT
# own settings folder, so the presets and the last GUI settings of the user do not leak into the pictures
export VIDEO_THUMBNAILER_HOME="$work/home" VTHUMB_LANG=en

# --- demo reel: seven 8-second scenes from ffmpeg's built-in generators (no copyrighted footage) -------------------
S=8 R=25 W=1280 H=720
demo="$work/demo-reel.mp4"
ffmpeg -hide_banner -loglevel error -y \
 -f lavfi -i "mandelbrot=s=640x360:r=$R:start_scale=3:end_scale=0.002:end_pts=$((S*R)):outer=normalized_iteration_count,scale=$W:$H,trim=duration=$S,setsar=1" \
 -f lavfi -i "gradients=s=${W}x$H:r=$R:c0=0x0b1d51:c1=0xff7e5f:c2=0xfeb47b:c3=0x6a3093:n=4:speed=0.03:type=radial,trim=duration=$S,setsar=1" \
 -f lavfi -i "life=s=160x90:r=$R:mold=10:ratio=0.3:life_color=0x7cfc00:death_color=0x1e3a5f:mold_color=0x0a0f1e,scale=$W:$H:flags=neighbor,trim=duration=$S,setsar=1" \
 -f lavfi -i "gradients=s=${W}x$H:r=$R:c0=0x00c9ff:c1=0x92fe9d:c2=0x0f2027:n=3:speed=0.05:type=spiral,trim=duration=$S,setsar=1" \
 -f lavfi -i "cellauto=s=160x90:r=$R:rule=30:scroll=1,format=gray,lutrgb=r='val*0.95':g='val*0.55+40':b='val*0.25+60',scale=$W:$H:flags=neighbor,trim=duration=$S,setsar=1" \
 -f lavfi -i "sierpinski=s=${W}x$H:r=$R:type=carpet,format=gray,lutrgb=r='val*0.35+20':g='val*0.6+30':b='val*0.9+25',trim=duration=$S,setsar=1" \
 -f lavfi -i "gradients=s=${W}x$H:r=$R:c0=0x141e30:c1=0xf857a6:c2=0xff5858:c3=0x243b55:n=4:speed=0.04:type=linear,trim=duration=$S,setsar=1" \
 -f lavfi -i "sine=frequency=330:duration=$((S*7))" \
 -filter_complex "[0][1][2][3][4][5][6]concat=n=7:v=1:a=0,format=yuv420p[v]" \
 -map "[v]" -map 7:a -c:v libx264 -preset veryfast -crf 23 -c:a aac -b:a 64k -shortest "$demo"
for i in 1 2 3; do  # three "episodes" for the pack picture
  ffmpeg -hide_banner -loglevel error -y -ss $(((i-1)*18)) -t 18 -i "$demo" -c copy "$work/episode-0$i.mp4"
done

# --- example sheets from the command line --------------------------------------------------------------------------
vthumb() { dotnet run --project "$root/src/VideoThumbnailer.Cli" -c Release --no-build -- "$@"; }
dotnet build "$root/src/VideoThumbnailer.Cli" -c Release -v q >/dev/null
vthumb -y -p Standard -w 1600 -q 85 -o "$img" --overwrite overwrite --pattern example-sheet "$demo"
vthumb -y -p Standard -w 1600 -q 85 -o "$img" --overwrite overwrite --pattern example-layout \
       --layout big-center --gap 14 --margin 20 --corners rounded --tile-shadow --background "#1B2430" \
       --title-color "#FFB86C" --timestamp-pos bottom-left "$demo"
vthumb -y -p Compact -w 900 -a webp --clip-seconds 2 --fps 8 -q 60 -o "$img" --overwrite overwrite \
       --pattern example-animated "$demo"
mv -f "$img/example-animated.sheet.webp" "$img/example-animated.webp"
vthumb pack -y -p Standard --tiles 6 -w 1600 -q 85 -o "$img/example-pack.jpg" "$work"/episode-0*.mp4

# --- GUI screenshots ----------------------------------------------------------------------------------------------
unset VIDEO_THUMBNAILER_HOME VTHUMB_LANG
VTHUMB_README_SHOTS="$img" VTHUMB_README_VIDEO="$demo" \
  dotnet test "$root/tests/VideoThumbnailer.Tests" --filter "FullyQualifiedName~Readme_screenshots"
ls -l "$img"
