using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using VideoThumbnailer.Core.Layout;
using VideoThumbnailer.Gui.Localization;
using VideoThumbnailer.Core.Settings;

namespace VideoThumbnailer.Gui.ViewModels;

/// <summary>Editable copy of <see cref="SheetSettings"/>. Numeric values are decimal for NumericUpDown.</summary>
public partial class SettingsViewModel : ObservableObject
{
    private SheetSettings _source = BuiltInPresets.Default;

    public static Choice[] Orders { get; } = Choice.ForEnum<TileOrder>("Order");
    public static Choice[] TimestampPositions { get; } = Choice.ForEnum<OverlayPosition>("Pos");
    public static Choice[] HeaderPositions { get; } = Choice.ForEnum<HeaderPosition>("Pos");
    public static Choice[] Formats { get; } = Choice.ForEnum<ImageFormat>("Format");
    public static Choice[] Locations { get; } = Choice.ForEnum<OutputLocation>("Location");
    public static Choice[] OverwriteModes { get; } = Choice.ForEnum<OverwriteMode>("Overwrite");
    public static Choice[] ScreenFits { get; } = Choice.ForEnum<ScreenFit>("Fit");
    public static Choice[] BackgroundModes { get; } = Choice.ForEnum<BackgroundImageMode>("BgMode");
    public static Choice[] CornerStyles { get; } = Choice.ForEnum<TileCorners>("Corners");
    public static Choice[] WatermarkPositions { get; } = Choice.ForEnum<WatermarkPosition>("WmPos");
    public static Choice[] SheetLanguages { get; } = [new("auto", "SheetLang.auto"), new("en", "SheetLang.en"), new("de", "SheetLang.de")];

    public SettingsViewModel() => Loc.Instance.LanguageChanged += () =>
    {
        OnPropertyChanged(nameof(PlaceholderHelp));
        RefreshDefaultTemplate();
    };

    [ObservableProperty] private string _sheetLanguage = "auto";

    partial void OnSheetLanguageChanged(string value) => RefreshDefaultTemplate();

    private string EffectiveSheetLanguage => Core.Localization.Lang.Normalize(SheetLanguage) ?? Core.Localization.Lang.Current;

    /// <summary>While the header shows a built-in default template, keep it in the language of the sheet.</summary>
    private void RefreshDefaultTemplate()
    {
        if (Core.Localization.CoreText.IsDefaultHeaderTemplate(HeaderTemplate))
            HeaderTemplate = Core.Localization.CoreText.DefaultHeaderTemplate(EffectiveSheetLanguage);
    }

    // Layout
    /// <summary>Plain grid, the built-in special grids and (if any) the special grid of the loaded preset.</summary>
    public ObservableCollection<LayoutChoice> Layouts { get; } = [LayoutChoice.Plain(), .. LayoutChoice.BuiltIns()];
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(IsPlainGrid))] private LayoutChoice? _selectedLayout;
    /// <summary>Columns and rows only apply to the plain grid.</summary>
    public bool IsPlainGrid => SelectedLayout?.Template is null;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(IsFixedWidth))] private ScreenFit _fitToScreen;
    /// <summary>The sheet width only applies when the size does not come from the screen.</summary>
    public bool IsFixedWidth => FitToScreen == ScreenFit.Off;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(HasBackgroundImage))] private string? _backgroundImagePath;
    [ObservableProperty] private BackgroundImageMode _backgroundImageMode;
    [ObservableProperty] private decimal _backgroundImageOpacity;
    public bool HasBackgroundImage => !string.IsNullOrWhiteSpace(BackgroundImagePath);
    [ObservableProperty] private decimal _columns;
    [ObservableProperty] private decimal _rows;
    [ObservableProperty] private decimal _sheetWidth;
    [ObservableProperty] private decimal _gap;
    [ObservableProperty] private decimal _margin;
    [ObservableProperty] private TileOrder _order;
    [ObservableProperty] private bool _portraitSwapGrid;
    [ObservableProperty] private decimal _skipStartPercent;
    [ObservableProperty] private decimal _skipEndPercent;
    [ObservableProperty] private bool _skipBlackFrames;
    [ObservableProperty] private decimal _blackFrameSearchSeconds;
    [ObservableProperty] private string _backgroundColor = "";
    [ObservableProperty] private string? _fontFamily;

    // Tile effects (#27)
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(HasTileBorder))] private decimal _tileBorderWidth;
    [ObservableProperty] private string _tileBorderColor = "";
    [ObservableProperty] private bool _tileShadow;
    [ObservableProperty] private string _tileShadowColor = "";
    [ObservableProperty] private bool _tileGrayscale;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(HasCornerSize))] private TileCorners _tileCorners;
    [ObservableProperty] private decimal _tileCornerRadius;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(HasSheetBorder))] private decimal _sheetBorderWidth;
    [ObservableProperty] private string _sheetBorderColor = "";
    /// <summary>The corner size only applies to rounded or cut corners.</summary>
    public bool HasCornerSize => TileCorners != TileCorners.Square;
    public bool HasTileBorder => TileBorderWidth > 0;
    public bool HasSheetBorder => SheetBorderWidth > 0;

    // Watermark (#29)
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(HasWatermark), nameof(HasWatermarkText))] private string? _watermarkText;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(HasWatermark), nameof(HasWatermarkImage), nameof(IsWatermarkText), nameof(HasWatermarkText))] private string? _watermarkImagePath;
    [ObservableProperty] private WatermarkPosition _watermarkPosition;
    [ObservableProperty] private decimal _watermarkSize;
    [ObservableProperty] private decimal _watermarkOpacity;
    [ObservableProperty] private string _watermarkColor = "";
    [ObservableProperty] private bool _watermarkPerTile;
    public bool HasWatermarkImage => !string.IsNullOrWhiteSpace(WatermarkImagePath);
    public bool HasWatermark => HasWatermarkImage || !string.IsNullOrWhiteSpace(WatermarkText);
    /// <summary>A picture wins over the text, so text and text color are only editable without a picture.</summary>
    public bool IsWatermarkText => !HasWatermarkImage;
    /// <summary>The text color applies to a text watermark only.</summary>
    public bool HasWatermarkText => IsWatermarkText && !string.IsNullOrWhiteSpace(WatermarkText);

    // Timestamps
    [ObservableProperty] private bool _timestampEnabled;
    [ObservableProperty] private OverlayPosition _timestampPosition;
    [ObservableProperty] private decimal _timestampFontSize;
    [ObservableProperty] private string _timestampColor = "";
    [ObservableProperty] private decimal _timestampOpacity;
    [ObservableProperty] private decimal _timestampOutline;
    [ObservableProperty] private bool _timestampShadow;
    [ObservableProperty] private string _timestampOutlineColor = "";
    [ObservableProperty] private string _timestampShadowColor = "";
    [ObservableProperty] private bool _timestampMilliseconds;

    // Header
    [ObservableProperty] private bool _headerEnabled;
    [ObservableProperty] private HeaderPosition _headerPosition;
    [ObservableProperty] private string _headerTemplate = "";
    [ObservableProperty] private decimal _headerFontSize;
    [ObservableProperty] private decimal _headerTitleFontSize;
    [ObservableProperty] private string _headerColor = "";
    [ObservableProperty] private string _headerTitleColor = "";
    /// <summary>Own text in the header (#28).</summary>
    [ObservableProperty] private string? _headerComment;

    // Output
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(IsJpeg), nameof(IsPng), nameof(IsWebp), nameof(HasQuality))] private ImageFormat _format;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(QualityValue))] private decimal _quality;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(IsOutputFolder), nameof(IsSubfolder))] private OutputLocation _location;
    [ObservableProperty] private string? _outputFolder;
    [ObservableProperty] private bool _keepFolderStructure;
    [ObservableProperty] private string _subfolderName = "";
    [ObservableProperty] private string _fileNamePattern = "";
    [ObservableProperty] private OverwriteMode _overwrite;
    [ObservableProperty] private bool _saveFrames;

    // Animated sheet
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(IsStill), nameof(HasQuality))] private bool _animated;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(IsMp4), nameof(IsWebm), nameof(IsGif), nameof(IsH265), nameof(IsWebpAnimation),
                                                   nameof(IsApng), nameof(IsOgv), nameof(HasQuality), nameof(CanHaveAudio))] private AnimationFormat _animationFormat;
    [ObservableProperty] private bool _animationAudio;
    [ObservableProperty] private decimal _clipSeconds;
    [ObservableProperty] private decimal _animationFps;

    public bool IsStill => !Animated;
    public bool IsMp4 { get => AnimationFormat == AnimationFormat.Mp4; set { if (value) AnimationFormat = AnimationFormat.Mp4; } }
    public bool IsWebm { get => AnimationFormat == AnimationFormat.Webm; set { if (value) AnimationFormat = AnimationFormat.Webm; } }
    public bool IsGif { get => AnimationFormat == AnimationFormat.Gif; set { if (value) AnimationFormat = AnimationFormat.Gif; } }
    public bool IsH265 { get => AnimationFormat == AnimationFormat.H265; set { if (value) AnimationFormat = AnimationFormat.H265; } }
    public bool IsWebpAnimation { get => AnimationFormat == AnimationFormat.Webp; set { if (value) AnimationFormat = AnimationFormat.Webp; } }
    public bool IsApng { get => AnimationFormat == AnimationFormat.Apng; set { if (value) AnimationFormat = AnimationFormat.Apng; } }
    public bool IsOgv { get => AnimationFormat == AnimationFormat.Ogv; set { if (value) AnimationFormat = AnimationFormat.Ogv; } }
    /// <summary>GIF, WebP and APNG cannot hold sound.</summary>
    public bool CanHaveAudio => AnimationSettings.SupportsAudio(AnimationFormat);

    // Format buttons and quality slider in the output section.
    public bool IsJpeg { get => Format == ImageFormat.Jpeg; set { if (value) Format = ImageFormat.Jpeg; } }
    public bool IsPng { get => Format == ImageFormat.Png; set { if (value) Format = ImageFormat.Png; } }
    public bool IsWebp { get => Format == ImageFormat.Webp; set { if (value) Format = ImageFormat.Webp; } }
    /// <summary>PNG/APNG are lossless and GIF has a fixed palette; the quality slider applies to all other formats.</summary>
    public bool HasQuality => Animated ? AnimationSettings.HasQuality(AnimationFormat) : Format != ImageFormat.Png;
    public double QualityValue { get => (double)Quality; set => Quality = Math.Clamp((decimal)Math.Round(value), 1, 100); }

    public bool IsOutputFolder => Location == OutputLocation.OutputFolder;
    public bool IsSubfolder => Location == OutputLocation.SubfolderNextToVideo;
    public string PlaceholderHelp => Loc.T("Placeholders", string.Join(" ", Core.Rendering.InfoTemplate.Placeholders.Select(p => "{" + p + "}")));

    public void Load(SheetSettings s)
    {
        _source = s.Clone();
        Columns = s.Columns; Rows = s.Rows; SheetWidth = s.SheetWidth; Gap = s.Gap; Margin = s.Margin;
        SelectedLayout = ChoiceFor(s.Template);
        FitToScreen = s.FitToScreen;
        BackgroundImagePath = s.BackgroundImage?.Path;
        BackgroundImageMode = s.BackgroundImage?.Mode ?? BackgroundImageMode.Fill;
        BackgroundImageOpacity = (decimal)(s.BackgroundImage?.Opacity ?? 0.5);
        Order = s.Order; PortraitSwapGrid = s.PortraitSwapGrid;
        SkipStartPercent = (decimal)s.SkipStartPercent; SkipEndPercent = (decimal)s.SkipEndPercent;
        SkipBlackFrames = s.SkipBlackFrames; BlackFrameSearchSeconds = (decimal)s.BlackFrameSearchSeconds;
        BackgroundColor = s.BackgroundColor; FontFamily = s.FontFamily;
        SheetLanguage = Core.Localization.Lang.Normalize(s.TextLanguage) ?? "auto";

        var ts = s.TileStyle ?? new TileStyleSettings();
        TileBorderWidth = (decimal)ts.BorderWidth; TileBorderColor = ts.BorderColor;
        TileShadow = ts.Shadow; TileShadowColor = ts.ShadowColor; TileGrayscale = ts.Grayscale;
        TileCorners = ts.Corners; TileCornerRadius = (decimal)ts.CornerRadius;
        SheetBorderWidth = (decimal)s.SheetBorderWidth; SheetBorderColor = s.SheetBorderColor;
        var wm = s.Watermark ?? new WatermarkSettings();
        WatermarkText = wm.Text; WatermarkImagePath = wm.ImagePath; WatermarkPosition = wm.Position;
        WatermarkSize = (decimal)wm.Size; WatermarkOpacity = (decimal)wm.Opacity; WatermarkColor = wm.Color; WatermarkPerTile = wm.PerTile;

        TimestampEnabled = s.Timestamp.Enabled; TimestampPosition = s.Timestamp.Position;
        TimestampFontSize = (decimal)s.Timestamp.FontSize; TimestampColor = s.Timestamp.Color;
        TimestampOpacity = (decimal)s.Timestamp.Opacity; TimestampOutline = (decimal)s.Timestamp.OutlineWidth;
        TimestampShadow = s.Timestamp.Shadow;
        TimestampOutlineColor = s.Timestamp.OutlineColor; TimestampShadowColor = s.Timestamp.ShadowColor;
        TimestampMilliseconds = s.Timestamp.ShowMilliseconds;

        HeaderEnabled = s.Header.Enabled; HeaderPosition = s.Header.Position; HeaderTemplate = s.Header.EffectiveTemplate(EffectiveSheetLanguage);
        HeaderFontSize = (decimal)s.Header.FontSize; HeaderTitleFontSize = (decimal)s.Header.TitleFontSize;
        HeaderColor = s.Header.Color; HeaderTitleColor = s.Header.TitleColor; HeaderComment = s.Header.Comment;

        Format = s.Output.Format; Quality = s.Output.Quality; Location = s.Output.Location;
        OutputFolder = s.Output.Folder; KeepFolderStructure = s.Output.KeepFolderStructure;
        SubfolderName = s.Output.SubfolderName; FileNamePattern = s.Output.FileNamePattern;
        Overwrite = s.Output.Overwrite; SaveFrames = s.Output.SaveFrames;

        Animated = s.Animation.Enabled; AnimationFormat = s.Animation.Format; AnimationAudio = s.Animation.Audio;
        ClipSeconds = (decimal)s.Animation.ClipSeconds; AnimationFps = s.Animation.FramesPerSecond;
    }

    /// <summary>Entry of the layout picker for a template; a template that is not built in gets its own entry.</summary>
    private LayoutChoice ChoiceFor(GridTemplate? t)
    {
        // Only the special grid of the preset currently loaded is offered besides the built-in ones.
        foreach (var old in Layouts.Where(l => !l.IsBuiltIn).ToList()) Layouts.Remove(old);
        if (t is null) return Layouts[0];
        var builtIn = Layouts.FirstOrDefault(l => l.Template is { } b && l.IsBuiltIn
                                                  && string.Equals(b.Name, t.Name, StringComparison.OrdinalIgnoreCase) && BuiltInTemplates.SameTiles(b, t));
        if (builtIn is not null) return builtIn;
        var custom = LayoutChoice.Custom(t);
        Layouts.Add(custom);
        return custom;
    }

    /// <summary>Builds settings from the current values. Fields the GUI does not show are kept from the loaded preset.</summary>
    public SheetSettings ToSettings(string name)
    {
        var s = _source.Clone();
        s.Name = name;
        s.Columns = (int)Columns; s.Rows = (int)Rows; s.SheetWidth = (int)SheetWidth; s.TileWidth = null;
        s.Template = SelectedLayout?.Template?.Clone();
        s.FitToScreen = FitToScreen;
        s.BackgroundImage = new BackgroundImageSettings
        {
            Path = string.IsNullOrWhiteSpace(BackgroundImagePath) ? null : BackgroundImagePath.Trim(),
            Mode = BackgroundImageMode,
            Opacity = (double)BackgroundImageOpacity,
        };
        s.Gap = (int)Gap; s.Margin = (int)Margin; s.Order = Order; s.PortraitSwapGrid = PortraitSwapGrid;
        s.SkipStartPercent = (double)SkipStartPercent; s.SkipEndPercent = (double)SkipEndPercent;
        s.SkipBlackFrames = SkipBlackFrames; s.BlackFrameSearchSeconds = (double)BlackFrameSearchSeconds;
        s.BackgroundColor = BackgroundColor; s.FontFamily = string.IsNullOrWhiteSpace(FontFamily) ? null : FontFamily;
        s.TextLanguage = Core.Localization.Lang.Normalize(SheetLanguage);

        // Shadow offset and blur are not shown and stay as in the preset.
        s.TileStyle.BorderWidth = (float)TileBorderWidth; s.TileStyle.BorderColor = TileBorderColor;
        s.TileStyle.Shadow = TileShadow; s.TileStyle.ShadowColor = TileShadowColor; s.TileStyle.Grayscale = TileGrayscale;
        s.TileStyle.Corners = TileCorners; s.TileStyle.CornerRadius = (float)TileCornerRadius;
        s.SheetBorderWidth = (float)SheetBorderWidth; s.SheetBorderColor = SheetBorderColor;
        s.Watermark.Text = string.IsNullOrWhiteSpace(WatermarkText) ? null : WatermarkText;
        s.Watermark.ImagePath = string.IsNullOrWhiteSpace(WatermarkImagePath) ? null : WatermarkImagePath.Trim();
        s.Watermark.Position = WatermarkPosition; s.Watermark.Size = (double)WatermarkSize; s.Watermark.Opacity = (double)WatermarkOpacity;
        s.Watermark.Color = WatermarkColor; s.Watermark.PerTile = WatermarkPerTile;

        s.Timestamp.Enabled = TimestampEnabled; s.Timestamp.Position = TimestampPosition;
        s.Timestamp.FontSize = (float)TimestampFontSize; s.Timestamp.Color = TimestampColor;
        s.Timestamp.Opacity = (double)TimestampOpacity; s.Timestamp.OutlineWidth = (float)TimestampOutline;
        s.Timestamp.Shadow = TimestampShadow;
        s.Timestamp.OutlineColor = TimestampOutlineColor; s.Timestamp.ShadowColor = TimestampShadowColor;
        s.Timestamp.ShowMilliseconds = TimestampMilliseconds;

        s.Header.Enabled = HeaderEnabled; s.Header.Position = HeaderPosition; s.Header.Template = Core.Localization.CoreText.IsDefaultHeaderTemplate(HeaderTemplate) ? null : HeaderTemplate;
        s.Header.FontSize = (float)HeaderFontSize; s.Header.TitleFontSize = (float)HeaderTitleFontSize;
        s.Header.Color = HeaderColor; s.Header.TitleColor = HeaderTitleColor;
        s.Header.Comment = string.IsNullOrWhiteSpace(HeaderComment) ? null : HeaderComment.Replace("\r\n", "\n");

        s.Output.Format = Format; s.Output.Quality = (int)Quality; s.Output.Location = Location;
        s.Output.Folder = OutputFolder; s.Output.KeepFolderStructure = KeepFolderStructure;
        s.Output.SubfolderName = SubfolderName; s.Output.FileNamePattern = FileNamePattern;
        s.Output.Overwrite = Overwrite; s.Output.SaveFrames = SaveFrames;

        s.Animation.Enabled = Animated; s.Animation.Format = AnimationFormat; s.Animation.Audio = AnimationAudio;
        s.Animation.ClipSeconds = (double)ClipSeconds; s.Animation.FramesPerSecond = (int)AnimationFps;
        return s;
    }
}
