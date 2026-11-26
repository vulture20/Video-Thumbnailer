using System.Globalization;
using VideoThumbnailer.Core.Localization;
using VideoThumbnailer.Core.Settings;

namespace VideoThumbnailer.Cli;

public sealed class UsageException(string message) : Exception(message);

/// <summary>Parsed command line for sheet generation. Values that are null were not given.</summary>
public sealed class GenerateOptions
{
    public List<string> Inputs { get; } = [];
    public string? Preset { get; set; }
    public bool? Recursive { get; set; }
    public int? Jobs { get; set; }
    public int? Videos { get; set; }
    public string? FfmpegDir { get; set; }
    public bool Yes { get; set; }
    public bool DryRun { get; set; }
    public bool Quiet { get; set; }
    public string? SavePresetAs { get; set; }
    /// <summary>Changes to apply on top of the preset, in command line order.</summary>
    public List<Action<SheetSettings>> Overrides { get; } = [];
}

public static class ArgParser
{
    public static GenerateOptions ParseGenerate(IReadOnlyList<string> args)
    {
        var o = new GenerateOptions();
        for (var i = 0; i < args.Count; i++)
        {
            var a = args[i];
            string Next()
            {
                if (i + 1 >= args.Count) throw new UsageException(CliText.T("NeedsValue", a));
                return args[++i];
            }
            int NextInt() => int.TryParse(Next(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v)
                ? v : throw new UsageException(CliText.T("NeedsInt", a));
            double NextDouble() => double.TryParse(Next(), NumberStyles.Float, CultureInfo.InvariantCulture, out var v)
                ? v : throw new UsageException(CliText.T("NeedsNumber", a));

            // allow --key=value
            if (a.StartsWith("--", StringComparison.Ordinal) && a.Contains('='))
            {
                var idx = a.IndexOf('=');
                var list = args.ToList();
                list[i] = a[..idx];
                list.Insert(i + 1, a[(idx + 1)..]);
                args = list;
                a = args[i];
            }

            switch (a)
            {
                case "-p": case "--preset": o.Preset = Next(); break;
                case "-o": case "--output":
                {
                    var dir = Next();
                    o.Overrides.Add(s => { s.Output.Location = OutputLocation.OutputFolder; s.Output.Folder = dir; });
                    break;
                }
                case "--next-to-video": o.Overrides.Add(s => s.Output.Location = OutputLocation.NextToVideo); break;
                case "--subfolder":
                {
                    var name = Next();
                    o.Overrides.Add(s => { s.Output.Location = OutputLocation.SubfolderNextToVideo; s.Output.SubfolderName = name; });
                    break;
                }
                case "--keep-structure": o.Overrides.Add(s => s.Output.KeepFolderStructure = true); break;
                case "-r": case "--recursive": o.Recursive = true; break;
                case "--no-recursive": o.Recursive = false; break;
                case "-c": case "--columns": { var v = NextInt(); o.Overrides.Add(s => s.Columns = v); break; }
                case "--rows": { var v = NextInt(); o.Overrides.Add(s => s.Rows = v); break; }
                case "-w": case "--width": { var v = NextInt(); o.Overrides.Add(s => { s.SheetWidth = v; s.TileWidth = null; }); break; }
                case "--tile-width": { var v = NextInt(); o.Overrides.Add(s => s.TileWidth = v); break; }
                case "--gap": { var v = NextInt(); o.Overrides.Add(s => s.Gap = v); break; }
                case "--margin": { var v = NextInt(); o.Overrides.Add(s => s.Margin = v); break; }
                case "--background": { var v = Next(); o.Overrides.Add(s => s.BackgroundColor = v); break; }
                case "--font": { var v = Next(); o.Overrides.Add(s => s.FontFamily = v); break; }
                case "-f": case "--format": { var v = ParseFormat(Next()); o.Overrides.Add(s => s.Output.Format = v); break; }
                case "-a": case "--animated":
                {
                    var v = ParseAnimationFormat(Next());
                    o.Overrides.Add(s => { s.Animation.Enabled = true; s.Animation.Format = v; });
                    break;
                }
                case "--still": o.Overrides.Add(s => s.Animation.Enabled = false); break;
                case "--clip-seconds": { var v = NextDouble(); o.Overrides.Add(s => s.Animation.ClipSeconds = v); break; }
                case "--fps": { var v = NextInt(); o.Overrides.Add(s => s.Animation.FramesPerSecond = v); break; }
                case "-q": case "--quality": { var v = NextInt(); o.Overrides.Add(s => s.Output.Quality = v); break; }
                case "--pattern": { var v = Next(); o.Overrides.Add(s => s.Output.FileNamePattern = v); break; }
                case "--order": { var v = ParseOrder(Next()); o.Overrides.Add(s => s.Order = v); break; }
                case "--frames": o.Overrides.Add(s => s.Output.SaveFrames = true); break;
                case "--no-timestamps": o.Overrides.Add(s => s.Timestamp.Enabled = false); break;
                case "--timestamp-pos": { var v = ParseEnum<OverlayPosition>(Next(), a); o.Overrides.Add(s => { s.Timestamp.Enabled = true; s.Timestamp.Position = v; }); break; }
                case "--no-header": o.Overrides.Add(s => s.Header.Enabled = false); break;
                case "--header-pos": { var v = ParseEnum<HeaderPosition>(Next(), a); o.Overrides.Add(s => { s.Header.Enabled = true; s.Header.Position = v; }); break; }
                case "--header-template": { var v = Next().Replace("\\n", "\n"); o.Overrides.Add(s => s.Header.Template = v); break; }
                case "--overwrite": { var v = ParseEnum<OverwriteMode>(Next(), a); o.Overrides.Add(s => s.Output.Overwrite = v); break; }
                case "--skip-start": { var v = NextDouble(); o.Overrides.Add(s => s.SkipStartPercent = v); break; }
                case "--skip-end": { var v = NextDouble(); o.Overrides.Add(s => s.SkipEndPercent = v); break; }
                case "--no-portrait-swap": o.Overrides.Add(s => s.PortraitSwapGrid = false); break;
                case "-j": case "--jobs": o.Jobs = NextInt(); break;
                case "--videos": o.Videos = NextInt(); break;
                case "--ffmpeg": o.FfmpegDir = Next(); break;
                case "-y": case "--yes": o.Yes = true; break;
                case "-n": case "--dry-run": o.DryRun = true; break;
                case "--quiet": o.Quiet = true; break;
                case "--save-preset": o.SavePresetAs = Next(); break;
                case "--lang": Next(); break; // handled in CliApp before parsing
                case "--sheet-lang":
                {
                    var v = Next();
                    var l = Lang.Normalize(v);
                    if (l is null && v.ToLowerInvariant() is not ("auto" or "system")) throw new UsageException(CliText.T("UnknownLanguage", v));
                    o.Overrides.Add(s => s.TextLanguage = l);
                    break;
                }
                case "--": o.Inputs.AddRange(args.Skip(i + 1)); i = args.Count; break;
                default:
                    if (a.StartsWith('-') && a.Length > 1 && !File.Exists(a) && !Directory.Exists(a))
                        throw new UsageException(CliText.T("UnknownOption", a));
                    o.Inputs.Add(a);
                    break;
            }
        }
        return o;
    }

    public static ImageFormat ParseFormat(string v) => v.ToLowerInvariant().TrimStart('.') switch
    {
        "jpg" or "jpeg" => ImageFormat.Jpeg,
        "png" => ImageFormat.Png,
        "webp" => ImageFormat.Webp,
        _ => throw new UsageException(CliText.T("UnknownFormat", v)),
    };

    public static AnimationFormat ParseAnimationFormat(string v) => v.ToLowerInvariant().TrimStart('.') switch
    {
        "mp4" or "h264" => AnimationFormat.Mp4,
        "webm" or "vp9" => AnimationFormat.Webm,
        "gif" => AnimationFormat.Gif,
        _ => throw new UsageException(CliText.T("UnknownAnimationFormat", v)),
    };

    public static TileOrder ParseOrder(string v) => v.ToLowerInvariant() switch
    {
        "row" or "rows" or "rowmajor" => TileOrder.RowMajor,
        "column" or "columns" or "col" or "columnmajor" => TileOrder.ColumnMajor,
        "random" => TileOrder.Random,
        _ => throw new UsageException(CliText.T("UnknownOrder", v)),
    };

    public static T ParseEnum<T>(string v, string option) where T : struct, Enum =>
        Enum.TryParse<T>(v.Replace("-", "").Replace("_", ""), ignoreCase: true, out var r)
            ? r
            : throw new UsageException(CliText.T("InvalidValue", v, option, string.Join(", ", Enum.GetNames<T>())));
}
