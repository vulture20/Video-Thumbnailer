using CommunityToolkit.Mvvm.ComponentModel;
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
    [ObservableProperty] private decimal _columns;
    [ObservableProperty] private decimal _rows;
    [ObservableProperty] private decimal _sheetWidth;
    [ObservableProperty] private decimal _gap;
    [ObservableProperty] private decimal _margin;
    [ObservableProperty] private TileOrder _order;
    [ObservableProperty] private bool _portraitSwapGrid;
    [ObservableProperty] private decimal _skipStartPercent;
    [ObservableProperty] private decimal _skipEndPercent;
    [ObservableProperty] private string _backgroundColor = "";
    [ObservableProperty] private string? _fontFamily;

    // Timestamps
    [ObservableProperty] private bool _timestampEnabled;
    [ObservableProperty] private OverlayPosition _timestampPosition;
    [ObservableProperty] private decimal _timestampFontSize;
    [ObservableProperty] private string _timestampColor = "";
    [ObservableProperty] private decimal _timestampOpacity;
    [ObservableProperty] private decimal _timestampOutline;
    [ObservableProperty] private bool _timestampShadow;

    // Header
    [ObservableProperty] private bool _headerEnabled;
    [ObservableProperty] private HeaderPosition _headerPosition;
    [ObservableProperty] private string _headerTemplate = "";
    [ObservableProperty] private decimal _headerFontSize;
    [ObservableProperty] private decimal _headerTitleFontSize;

    // Output
    [ObservableProperty] private ImageFormat _format;
    [ObservableProperty] private decimal _quality;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(IsOutputFolder), nameof(IsSubfolder))] private OutputLocation _location;
    [ObservableProperty] private string? _outputFolder;
    [ObservableProperty] private bool _keepFolderStructure;
    [ObservableProperty] private string _subfolderName = "";
    [ObservableProperty] private string _fileNamePattern = "";
    [ObservableProperty] private OverwriteMode _overwrite;
    [ObservableProperty] private bool _saveFrames;

    public bool IsOutputFolder => Location == OutputLocation.OutputFolder;
    public bool IsSubfolder => Location == OutputLocation.SubfolderNextToVideo;
    public string PlaceholderHelp => Loc.T("Placeholders", string.Join(" ", Core.Rendering.InfoTemplate.Placeholders.Select(p => "{" + p + "}")));

    public void Load(SheetSettings s)
    {
        _source = s.Clone();
        Columns = s.Columns; Rows = s.Rows; SheetWidth = s.SheetWidth; Gap = s.Gap; Margin = s.Margin;
        Order = s.Order; PortraitSwapGrid = s.PortraitSwapGrid;
        SkipStartPercent = (decimal)s.SkipStartPercent; SkipEndPercent = (decimal)s.SkipEndPercent;
        BackgroundColor = s.BackgroundColor; FontFamily = s.FontFamily;
        SheetLanguage = Core.Localization.Lang.Normalize(s.TextLanguage) ?? "auto";

        TimestampEnabled = s.Timestamp.Enabled; TimestampPosition = s.Timestamp.Position;
        TimestampFontSize = (decimal)s.Timestamp.FontSize; TimestampColor = s.Timestamp.Color;
        TimestampOpacity = (decimal)s.Timestamp.Opacity; TimestampOutline = (decimal)s.Timestamp.OutlineWidth;
        TimestampShadow = s.Timestamp.Shadow;

        HeaderEnabled = s.Header.Enabled; HeaderPosition = s.Header.Position; HeaderTemplate = s.Header.EffectiveTemplate(EffectiveSheetLanguage);
        HeaderFontSize = (decimal)s.Header.FontSize; HeaderTitleFontSize = (decimal)s.Header.TitleFontSize;

        Format = s.Output.Format; Quality = s.Output.Quality; Location = s.Output.Location;
        OutputFolder = s.Output.Folder; KeepFolderStructure = s.Output.KeepFolderStructure;
        SubfolderName = s.Output.SubfolderName; FileNamePattern = s.Output.FileNamePattern;
        Overwrite = s.Output.Overwrite; SaveFrames = s.Output.SaveFrames;
    }

    /// <summary>Builds settings from the current values. Fields the GUI does not show are kept from the loaded preset.</summary>
    public SheetSettings ToSettings(string name)
    {
        var s = _source.Clone();
        s.Name = name;
        s.Columns = (int)Columns; s.Rows = (int)Rows; s.SheetWidth = (int)SheetWidth; s.TileWidth = null;
        s.Gap = (int)Gap; s.Margin = (int)Margin; s.Order = Order; s.PortraitSwapGrid = PortraitSwapGrid;
        s.SkipStartPercent = (double)SkipStartPercent; s.SkipEndPercent = (double)SkipEndPercent;
        s.BackgroundColor = BackgroundColor; s.FontFamily = string.IsNullOrWhiteSpace(FontFamily) ? null : FontFamily;
        s.TextLanguage = Core.Localization.Lang.Normalize(SheetLanguage);

        s.Timestamp.Enabled = TimestampEnabled; s.Timestamp.Position = TimestampPosition;
        s.Timestamp.FontSize = (float)TimestampFontSize; s.Timestamp.Color = TimestampColor;
        s.Timestamp.Opacity = (double)TimestampOpacity; s.Timestamp.OutlineWidth = (float)TimestampOutline;
        s.Timestamp.Shadow = TimestampShadow;

        s.Header.Enabled = HeaderEnabled; s.Header.Position = HeaderPosition; s.Header.Template = Core.Localization.CoreText.IsDefaultHeaderTemplate(HeaderTemplate) ? null : HeaderTemplate;
        s.Header.FontSize = (float)HeaderFontSize; s.Header.TitleFontSize = (float)HeaderTitleFontSize;

        s.Output.Format = Format; s.Output.Quality = (int)Quality; s.Output.Location = Location;
        s.Output.Folder = OutputFolder; s.Output.KeepFolderStructure = KeepFolderStructure;
        s.Output.SubfolderName = SubfolderName; s.Output.FileNamePattern = FileNamePattern;
        s.Output.Overwrite = Overwrite; s.Output.SaveFrames = SaveFrames;
        return s;
    }
}
