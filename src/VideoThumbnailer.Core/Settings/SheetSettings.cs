using System.Text.Json.Serialization;
using VideoThumbnailer.Core.Layout;
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
public enum AnimationFormat
{
    /// <summary>MP4 with H.264.</summary>
    Mp4,
    /// <summary>WebM with VP9.</summary>
    Webm,
    Gif,
    /// <summary>MP4 with H.265/HEVC (smaller, not played everywhere).</summary>
    H265,
    /// <summary>Animated WebP.</summary>
    Webp,
    /// <summary>Animated PNG (lossless, large).</summary>
    Apng,
    /// <summary>Ogg with Theora.</summary>
    Ogv,
}

/// <summary>Shape of the tile corners (#27).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<TileCorners>))]
public enum TileCorners { Square, Rounded, Cut }

/// <summary>Where the watermark goes (#29).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<WatermarkPosition>))]
public enum WatermarkPosition { TopLeft, TopRight, BottomLeft, BottomRight, Center, Tiled }

/// <summary>Sheet size from the screen (#25). The fixed <see cref="SheetSettings.SheetWidth"/> stays as fallback.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ScreenFit>))]
public enum ScreenFit
{
    /// <summary>Fixed width (<see cref="SheetSettings.SheetWidth"/> or <see cref="SheetSettings.TileWidth"/>).</summary>
    Off,
    /// <summary>As wide as the screen.</summary>
    Width,
    /// <summary>As large as fits on the screen without scrolling (width and height).</summary>
    WidthAndHeight,
}

[JsonConverter(typeof(JsonStringEnumConverter<BackgroundImageMode>))]
public enum BackgroundImageMode
{
    /// <summary>Covers the whole sheet, cut off at the sides (keeps the aspect ratio).</summary>
    Fill,
    /// <summary>Stretched to the size of the sheet (aspect ratio may change).</summary>
    Stretch,
    /// <summary>Repeated in original size from the top left.</summary>
    Tile,
    /// <summary>Original size in the middle.</summary>
    Center,
}

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
    /// <summary>
    /// Grid with tiles of different sizes; null = plain grid of <see cref="Columns"/> × <see cref="Rows"/> tiles.
    /// When set, columns and rows of the template are used instead.
    /// </summary>
    public GridTemplate? Template { get; set; }
    /// <summary>Total width of the sheet in pixels. Ignored when <see cref="TileWidth"/> is set.</summary>
    public int SheetWidth { get; set; } = 1920;
    /// <summary>
    /// Derive the size from the screen (<see cref="ScreenFitting.Current"/>). Without a known screen
    /// (command line without --screen, headless) the fixed width applies.
    /// </summary>
    public ScreenFit FitToScreen { get; set; } = ScreenFit.Off;
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
    /// <summary>Optional picture behind the tiles, drawn over <see cref="BackgroundColor"/>.</summary>
    public BackgroundImageSettings BackgroundImage { get; set; } = new();
    /// <summary>Font family for all text. Empty = bundled DejaVu Sans.</summary>
    public string? FontFamily { get; set; }
    /// <summary>Language of texts drawn into the sheet ("en", "de"); null = same as the application.</summary>
    public string? TextLanguage { get; set; }

    /// <summary>Language actually used for sheet texts.</summary>
    [JsonIgnore]
    public string EffectiveTextLanguage => Lang.Normalize(TextLanguage) ?? Lang.Current;

    /// <summary>Border, shadow, gray scale and corners of the tiles (#27).</summary>
    public TileStyleSettings TileStyle { get; set; } = new();
    /// <summary>Line around the whole sheet, drawn inside its edge (0 = none).</summary>
    public float SheetBorderWidth { get; set; }
    public string SheetBorderColor { get; set; } = "#FFFFFFFF";
    /// <summary>Picture or text over the sheet or every tile (#29).</summary>
    public WatermarkSettings Watermark { get; set; } = new();

    public TimestampSettings Timestamp { get; set; } = new();
    public HeaderSettings Header { get; set; } = new();
    public OutputSettings Output { get; set; } = new();
    public AnimationSettings Animation { get; set; } = new();
    /// <summary>Short clip of several parts of the video (#31), written next to the sheet or instead of it.</summary>
    public TrailerSettings Trailer { get; set; } = new();

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
        BackgroundImage ??= new();
        BackgroundImage.Opacity = Math.Clamp(BackgroundImage.Opacity, 0, 1);
        if (string.IsNullOrWhiteSpace(BackgroundImage.Path)) BackgroundImage.Path = null;
        TileStyle ??= new();
        TileStyle.BorderWidth = Math.Clamp(TileStyle.BorderWidth, 0, 100);
        TileStyle.ShadowOffset = Math.Clamp(TileStyle.ShadowOffset, 0, 100);
        TileStyle.ShadowBlur = Math.Clamp(TileStyle.ShadowBlur, 0, 100);
        TileStyle.CornerRadius = Math.Clamp(TileStyle.CornerRadius, 0, 500);
        SheetBorderWidth = Math.Clamp(SheetBorderWidth, 0, 500);
        Watermark ??= new();
        Watermark.Size = Math.Clamp(Watermark.Size, 0.01, 1);
        Watermark.Opacity = Math.Clamp(Watermark.Opacity, 0, 1);
        if (string.IsNullOrWhiteSpace(Watermark.ImagePath)) Watermark.ImagePath = null;
        if (string.IsNullOrEmpty(Watermark.Text)) Watermark.Text = null;
        if (string.IsNullOrEmpty(Header.Comment)) Header.Comment = null;
        if (Template is { } template)
        {
            var problems = template.Validate();
            if (problems.Count > 0) errors.AddRange(problems);
        }
        Animation.ClipSeconds = Math.Clamp(Animation.ClipSeconds, 0.5, 30);
        Animation.FramesPerSecond = Math.Clamp(Animation.FramesPerSecond, 1, 60);
        Trailer ??= new();
        Trailer.Normalize();
        Output.WebVttTileWidth = Math.Clamp(Output.WebVttTileWidth, 32, 1920);
        if (string.IsNullOrWhiteSpace(Output.FileNamePattern)) Output.FileNamePattern = "{name}";
        if (Output.Location == OutputLocation.OutputFolder && string.IsNullOrWhiteSpace(Output.Folder))
            errors.Add(CoreText.T("OutputFolderMissing"));
        if (Output.Location == OutputLocation.SubfolderNextToVideo && string.IsNullOrWhiteSpace(Output.SubfolderName))
            Output.SubfolderName = "thumbnails";
        return errors;
    }
}

public sealed class BackgroundImageSettings
{
    /// <summary>
    /// Image file (JPG, PNG, WebP, BMP, GIF). Relative paths in a preset file are resolved against the folder of
    /// that file when it is loaded, otherwise against the current folder. Null = no picture.
    /// </summary>
    public string? Path { get; set; }
    public BackgroundImageMode Mode { get; set; } = BackgroundImageMode.Fill;
    /// <summary>
    /// 0..1: how strongly the picture shows over the background color. Below 1 it is blended with the background
    /// color (darker with a dark color), so timestamps and the info header stay readable.
    /// </summary>
    public double Opacity { get; set; } = 0.5;

    [JsonIgnore]
    public bool Enabled => !string.IsNullOrWhiteSpace(Path);
}

public sealed class TileStyleSettings
{
    /// <summary>Line along the edge of every tile, drawn inside the tile (0 = none), so the layout stays the same.</summary>
    public float BorderWidth { get; set; }
    public string BorderColor { get; set; } = "#FFFFFFFF";
    /// <summary>Drop shadow behind every tile; it falls into the gap, so it needs a gap or margin of about its offset.</summary>
    public bool Shadow { get; set; }
    public string ShadowColor { get; set; } = "#B4000000";
    public float ShadowOffset { get; set; } = 4;
    public float ShadowBlur { get; set; } = 4;
    /// <summary>Tiles in shades of gray (the frames saved separately stay in color).</summary>
    public bool Grayscale { get; set; }
    public TileCorners Corners { get; set; } = TileCorners.Square;
    /// <summary>Size of rounded or cut corners in pixels.</summary>
    public float CornerRadius { get; set; } = 12;
}

public sealed class WatermarkSettings
{
    /// <summary>Text of the watermark; used when there is no picture.</summary>
    public string? Text { get; set; }
    /// <summary>Picture (PNG with transparency works best). Relative paths like the background picture. Wins over the text.</summary>
    public string? ImagePath { get; set; }
    public WatermarkPosition Position { get; set; } = WatermarkPosition.BottomRight;
    /// <summary>Width of the watermark as a share (0.01..1) of the sheet width, or of the tile width with <see cref="PerTile"/>.</summary>
    public double Size { get; set; } = 0.2;
    public double Opacity { get; set; } = 0.5;
    /// <summary>Color of the text.</summary>
    public string Color { get; set; } = "#FFFFFFFF";
    /// <summary>In every tile instead of once over the sheet.</summary>
    public bool PerTile { get; set; }

    [JsonIgnore]
    public bool HasImage => !string.IsNullOrWhiteSpace(ImagePath);
    [JsonIgnore]
    public bool Enabled => HasImage || !string.IsNullOrWhiteSpace(Text);
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
    /// <summary>
    /// Own text (#28), e.g. "Holiday 2026". Goes where the template has <c>{comment}</c>, otherwise below the last line.
    /// Several lines are allowed. Part of the header: not shown when the header is off.
    /// </summary>
    public string? Comment { get; set; }
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
    /// <summary>
    /// Additionally write a WebVTT file with a sprite image for the seek bar of web players (#32):
    /// "&lt;sheet name&gt;.thumbnails.vtt" and "&lt;sheet name&gt;.thumbnails.jpg", made from the same captured frames.
    /// </summary>
    public bool WebVtt { get; set; }
    /// <summary>Width of one picture in the WebVTT sprite (pixels).</summary>
    public int WebVttTileWidth { get; set; } = 160;
    /// <summary>
    /// Additionally write the JSON sidecar "&lt;sheet name&gt;.sheet.json" with the video, the sheet size and per tile
    /// its rectangle and capture time (#38), for the viewer (#39) and to restore times chosen by hand.
    /// </summary>
    public bool Sidecar { get; set; }
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
    /// <summary>
    /// Sound of one tile (the biggest, of equal ones the earliest) for MP4, H.265, WebM and OGV. GIF, WebP and APNG
    /// cannot hold sound; there it is ignored.
    /// </summary>
    public bool Audio { get; set; }

    /// <summary>Formats that can carry sound.</summary>
    public static bool SupportsAudio(AnimationFormat f) => f is AnimationFormat.Mp4 or AnimationFormat.H265 or AnimationFormat.Webm or AnimationFormat.Ogv;
    /// <summary>Formats where the quality setting has an effect.</summary>
    public static bool HasQuality(AnimationFormat f) => f is not (AnimationFormat.Gif or AnimationFormat.Apng);

    /// <summary>Number of frames of the animation.</summary>
    [JsonIgnore]
    public int FrameCount => Math.Max(1, (int)Math.Round(ClipSeconds * FramesPerSecond));
}

/// <summary>
/// Preview clip (#31): parts from several places of the video one after the other, like a trailer.
/// Written as "&lt;name&gt;.preview.&lt;ext&gt;" next to the sheet.
/// </summary>
public sealed class TrailerSettings
{
    /// <summary>Longest clip in seconds; longer settings get shorter parts.</summary>
    public const double MaxSeconds = 120;

    public bool Enabled { get; set; }
    /// <summary>Only the clip, no sheet.</summary>
    public bool Only { get; set; }
    public AnimationFormat Format { get; set; } = AnimationFormat.Mp4;
    /// <summary>Number of parts.</summary>
    public int Clips { get; set; } = 12;
    /// <summary>Length of each part in seconds.</summary>
    public double ClipSeconds { get; set; } = 2.5;
    /// <summary>Cross-fade between two parts in seconds; 0 = hard cut.</summary>
    public double CrossfadeSeconds { get; set; } = 0.5;
    /// <summary>Longer side of the picture in pixels (width for landscape, height for portrait videos).</summary>
    public int Size { get; set; } = 640;
    public int FramesPerSecond { get; set; } = 24;
    /// <summary>Time of the part in the corner, styled like the timestamps of the sheet.</summary>
    public bool Timestamps { get; set; } = true;
    /// <summary>Sound of the parts (MP4, H.265, WebM, OGV only).</summary>
    public bool Audio { get; set; }

    /// <summary>Clamps the values; parts get shorter so that the whole clip stays within <see cref="MaxSeconds"/>.</summary>
    public void Normalize()
    {
        Clips = Math.Clamp(Clips, 1, 60);
        ClipSeconds = Math.Clamp(ClipSeconds, 0.5, 30);
        if (Clips * ClipSeconds > MaxSeconds) ClipSeconds = Math.Max(0.5, Math.Floor(MaxSeconds / Clips * 10) / 10);
        CrossfadeSeconds = Math.Clamp(CrossfadeSeconds, 0, Math.Min(2, ClipSeconds / 2));
        Size = Math.Clamp(Size, 64, 3840);
        FramesPerSecond = Math.Clamp(FramesPerSecond, 1, 60);
    }

    /// <summary>Length of the whole clip in seconds (parts minus the cross-fades).</summary>
    [JsonIgnore]
    public double TotalSeconds => Clips * ClipSeconds - (Clips - 1) * CrossfadeSeconds;
}
