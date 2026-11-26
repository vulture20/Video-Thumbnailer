namespace VideoThumbnailer.Cli;

internal static class HelpText
{
    public const string Main = """
        vthumb - create video contact sheets (thumbnail sheets)

        Usage:
          vthumb [options] <file|folder>...      create sheets
          vthumb presets list|show|import|export|delete
          vthumb ffmpeg status|check|install|update|uninstall
          vthumb --help | --version

        Without --preset the settings last used in the GUI are applied (or 'Standard').

        Input:
          -r, --recursive            scan sub folders (default: on)
              --no-recursive         only the given folders
          -n, --dry-run              list videos and output paths, create nothing

        Preset and layout:
          -p, --preset <name|file>   preset name or path to a .vtpreset.json file
          -c, --columns <n>          tiles per row
              --rows <n>             rows
          -w, --width <px>           total sheet width
              --tile-width <px>      fixed tile width (overrides --width)
              --gap <px> / --margin <px>
              --order row|column|random
              --skip-start <%> / --skip-end <%>   ignore start/end of the video
              --no-portrait-swap     keep columns/rows for portrait videos
              --background <#AARRGGBB>  --font <family>

        Overlays:
              --no-timestamps        --timestamp-pos TopLeft|TopRight|BottomLeft|BottomRight|Center
              --no-header            --header-pos TopLeft|TopRight|BottomLeft|BottomRight
              --header-template <t>  e.g. "{filename}\n{size:GB} · {duration} · {resolution}"

        Output:
          -f, --format jpg|png|webp  -q, --quality <1-100>
          -o, --output <folder>      write all sheets into this folder
              --keep-structure       with -o: recreate the input folder structure
              --next-to-video        write next to each video (default)
              --subfolder <name>     write into <video folder>/<name>
              --pattern <pattern>    file name, placeholders {name} {ext} {preset} {cols} {rows} {date} {time}
              --overwrite skip|overwrite|rename
              --frames               also save every frame as its own image
              --save-preset <name>   save the resulting settings as a preset

        Processing:
          -j, --jobs <n>             max. parallel ffmpeg processes (default: CPU cores)
              --videos <n>           max. videos in parallel
              --ffmpeg <folder>      use ffmpeg/ffprobe from this folder
          -y, --yes                  download ffmpeg without asking if it is missing
              --quiet                only print errors and the summary

        Exit codes: 0 ok, 1 some videos failed, 2 usage error, 3 ffmpeg missing, 130 cancelled.
        """;
}
