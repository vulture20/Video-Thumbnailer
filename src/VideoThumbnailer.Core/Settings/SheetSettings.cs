using System.Text.Json.Serialization;
using VideoThumbnailer.Core.Localization;

namespace VideoThumbnailer.Core.Settings;

[JsonConverter(typeof(JsonStringEnumConverter<TileOrder>))]
public enum TileOrder { RowMajor, ColumnMajor, Random }

[JsonConverter(typeof(JsonStringEnumConverter<OverlayPosition>))]
public enum OverlayPosition { TopLeft, TopRight, BottomLeft, BottomRight, Center }

[JsonConverter(typeof(JsonStringEnumConverter<HeaderPosition>))]
public enum HeaderPosition { TopLeft, TopRight, BottomLeft, BottomRight }

[JsonConverter(typeof(JsonStringEnumConverter<ImageFormat>))]
public enum ImageFormat { Jpeg, Png, Webp }

[JsonConverter(typeof(JsonStringEnumConverter<AnimationFormat>))]
public enum AnimationFormat { Mp4, Webm, Gif }

[JsonConverter(typeof(JsonStringEnumConverter<OutputLocation>))]
public enum OutputLocation
{
    /// <summary>All sheets go to <see cref="OutputSettings.Folder"/>.</summary>
    OutputFolder,
    /// <summary>Sheet is saved in the same folder as the video.</summary>
    NextToVideo,
    /// <summary>Sheet is saved in a subfolder next to the video ("one level down").</summary>
    SubfolderNextToVideo,
}

[JsonConverter(typeof(JsonStringEnumConverter<OverwriteMode>))]
public enum OverwriteMode { Skip, Overwrite, Rename }

/// <summary>
/// All settings that describe how a sheet looks and where it goes. A preset is a named
/// <see cref="SheetSettings"/> serialized as JSON.
/// </summary>
public sealed class SheetSettings
{
    public int SchemaVersion { get; set; } = 1;
    public string Name { get; set; } = "Custom";
    public string? Description { get; set; }

    public int Columns { get; set; } = 4;
    public int Rows { get; set; } = 5;
    /// <summary>Total width of the sheet in pixels. Ignored when <see cref="TileWidth"/> is set.</summary>
    public int SheetWidth { get; set; } = 1920;
    /// <summary>Optional fixed tile width; the sheet width is derived from it.</summary>
    public int? TileWidth { get; set; }
    public int Gap { get; set; } = 6;
    public int Margin { get; set; } = 10;
    /// <summary>For portrait videos (height &gt; width) swap columns and rows to keep the sheet compact.</summary>
    public bool PortraitSwapGrid { get; set; } = true;
    public TileOrder Order { get; set; } = TileOrder.RowMajor;
    /// <summary>Percentage of the duration skipped at the start (intros, black frames).</summary>
    public double SkipStartPercent { get; set; } = 2;
    /// <summary>Percentage of the duration skipped at the end (credits).</summary>
    public double SkipEndPercent { get; set; } = 2;
    /// <summary>Replace (nearly) black or single-colored frames (fades, cuts) by a good frame close by.</summary>
    public bool SkipBlackFrames { get; set; } = true;
    /// <summary>How far before and after the planned time (seconds) to look for a better frame.</summary>
    public double BlackFrameSearchSeconds { get; set; } = 2;
    public string BackgroundColor { get; set; } = "#FF1E1E1E";
    /// <summary>Font family for all text. Empty = bundled DejaVu Sans.</summary>
    public string? FontFamily { get; set; }
    /// <summary>Language of texts drawn into the sheet ("en", "de"); null = same as the application.</summary>
    public string? TextLanguage { get; set; }

    /// <summary>Language actually used for sheet texts.</summary>
    [JsonIgnore]
    public string EffectiveTextLanguage => Lang.Normalize(TextLanguage) ?? Lang.Current;

    public TimestampSettings Timestamp { get; set; } = new();
    public HeaderSettings Header { get; set; } = new();
    public OutputSettings Output { get; set; } = new();
    public AnimationSettings Animation { get; set; } = new();

    public SheetSettings Clone()
    {
        var json = System.Text.Json.JsonSerializer.Serialize(this, SettingsJson.Options);
        return System.Text.Json.JsonSerializer.Deserialize<SheetSettings>(json, SettingsJson.Options)!;
    }

    /// <summary>Clamps values into sane ranges. Returns a list of problems that could not be fixed.</summary>
    public IReadOnlyList<string> Normalize()
    {
        var errors = new List<string>();
        Columns = Math.Clamp(Columns, 1, 50);
        Rows = Math.Clamp(Rows, 1, 100);
        SheetWidth = Math.Clamp(SheetWidth, 100, 32000);
        if (TileWidth is { } tw) TileWidth = Math.Clamp(tw, 16, 8000);
        Gap = Math.Clamp(Gap, 0, 500);
        Margin = Math.Clamp(Margin, 0, 500);
        SkipStartPercent = Math.Clamp(SkipStartPercent, 0, 45);
        SkipEndPercent = Math.Clamp(SkipEndPercent, 0, 45);
        BlackFrameSearchSeconds = Math.Clamp(BlackFrameSearchSeconds, 0.5, 30);
        Timestamp.FontSize = Math.Clamp(Timestamp.FontSize, 4, 400);
        Timestamp.Opacity = Math.Clamp(Timestamp.Opacity, 0, 1);
        Header.FontSize = Math.Clamp(Header.FontSize, 4, 400);
        Header.TitleFontSize = Math.Clamp(Header.TitleFontSize, 4, 400);
        Output.Quality = Math.Clamp(Output.Quality, 1, 100);
        Animation.ClipSeconds = Math.Clamp(Animation.ClipSeconds, 0.5, 30);
        Animation.FramesPerSecond = Math.Clamp(Animation.FramesPerSecond, 1, 60);
        if (string.IsNullOrWhiteSpace(Output.FileNamePattern)) Output.FileNamePattern = "{name}";
        if (Output.Location == OutputLocation.OutputFolder && string.IsNullOrWhiteSpace(Output.Folder))
            errors.Add(CoreText.T("OutputFolderMissing"));
        if (Output.Location == OutputLocation.SubfolderNextToVideo && string.IsNullOrWhiteSpace(Output.SubfolderName))
            Output.SubfolderName = "thumbnails";
        return errors;
    }
}

public sealed class TimestampSettings
{
    public bool Enabled { get; set; } = true;
    public OverlayPosition Position { get; set; } = OverlayPosition.BottomRight;
    public float FontSize { get; set; } = 16;
    public bool Bold { get; set; } = true;
    public string Color { get; set; } = "#FFFFFFFF";
    /// <summary>0..1, multiplied with the alpha of all timestamp colors.</summary>
    public double Opacity { get; set; } = 1.0;
    public string OutlineColor { get; set; } = "#FF000000";
    public float OutlineWidth { get; set; } = 3;
    public bool Shadow { get; set; } = true;
    public string ShadowColor { get; set; } = "#AA000000";
    public float ShadowOffset { get; set; } = 2;
    public float Padding { get; set; } = 6;
    /// <summary>Show milliseconds (hh:mm:ss.fff).</summary>
    public bool ShowMilliseconds { get; set; }
}

public sealed class HeaderSettings
{
    public bool Enabled { get; set; } = true;
    public HeaderPosition Position { get; set; } = HeaderPosition.TopLeft;
    /// <summary>
    /// Lines of the info header with placeholders in braces (see README). The first line is rendered
    /// with <see cref="TitleFontSize"/> in bold. Null/empty or a built-in default = default template in the sheet language.
    /// </summary>
    public string? Template { get; set; }

    /// <summary>The template to render for the given language.</summary>
    public string EffectiveTemplate(string lang) =>
        CoreText.IsDefaultHeaderTemplate(Template) ? CoreText.DefaultHeaderTemplate(lang) : Template!;
    public float FontSize { get; set; } = 16;
    public float TitleFontSize { get; set; } = 22;
    public string Color { get; set; } = "#FFE6E6E6";
    public string TitleColor { get; set; } = "#FFFFFFFF";
    public float LineSpacing { get; set; } = 1.25f;
}

public sealed class OutputSettings
{
    public ImageFormat Format { get; set; } = ImageFormat.Jpeg;
    /// <summary>1..100, used for JPEG and WebP.</summary>
    public int Quality { get; set; } = 90;
    public OutputLocation Location { get; set; } = OutputLocation.NextToVideo;
    public string? Folder { get; set; }
    /// <summary>With <see cref="OutputLocation.OutputFolder"/>: recreate the folder structure below the added input folder.</summary>
    public bool KeepFolderStructure { get; set; }
    public string SubfolderName { get; set; } = "thumbnails";
    /// <summary>File name without extension. Placeholders: {name} {ext} {preset} {cols} {rows} {date} {time}.</summary>
    public string FileNamePattern { get; set; } = "{name}";
    public OverwriteMode Overwrite { get; set; } = OverwriteMode.Overwrite;
    /// <summary>Additionally save every captured frame as its own image.</summary>
    public bool SaveFrames { get; set; }
    /// <summary>Folder (relative to the sheet) for single frames. Same placeholders as the file name.</summary>
    public string FramesFolderPattern { get; set; } = "{name}_frames";
}

/// <summary>
/// Animated sheet: every tile plays a short clip starting at its timestamp. When enabled, the sheet is
/// written as video/GIF instead of a still image (<see cref="OutputSettings.Format"/> is not used then).
/// </summary>
public sealed class AnimationSettings
{
    public bool Enabled { get; set; }
    public AnimationFormat Format { get; set; } = AnimationFormat.Mp4;
    /// <summary>Length of each tile's clip in seconds (= length of the animation).</summary>
    public double ClipSeconds { get; set; } = 3;
    public int FramesPerSecond { get; set; } = 10;

    /// <summary>Number of frames of the animation.</summary>
    [JsonIgnore]
    public int FrameCount => Math.Max(1, (int)Math.Round(ClipSeconds * FramesPerSecond));
}
