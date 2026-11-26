namespace VideoThumbnailer.Core.Settings;

public static class BuiltInPresets
{
    public static IReadOnlyList<SheetSettings> All { get; } =
    [
        new SheetSettings
        {
            Name = "Standard",
            Description = "4 × 5 tiles, 1920 px wide, JPG, info header and timestamps",
        },
        new SheetSettings
        {
            Name = "Compact",
            Description = "3 × 3 tiles, 1280 px wide, small header",
            Columns = 3, Rows = 3, SheetWidth = 1280, Gap = 4, Margin = 8,
            Header = new HeaderSettings { FontSize = 13, TitleFontSize = 17, Template = "{filename}\n{size} · {duration} · {resolution} · {vcodec}" },
            Timestamp = new TimestampSettings { FontSize = 14 },
        },
        new SheetSettings
        {
            Name = "Detailed",
            Description = "6 × 8 tiles, 3000 px wide, PNG",
            Columns = 6, Rows = 8, SheetWidth = 3000, Gap = 8, Margin = 14,
            Header = new HeaderSettings { FontSize = 22, TitleFontSize = 30 },
            Timestamp = new TimestampSettings { FontSize = 20 },
            Output = new OutputSettings { Format = ImageFormat.Png },
        },
        new SheetSettings
        {
            Name = "Web",
            Description = "5 × 4 tiles, 1600 px wide, WebP, light background",
            Columns = 5, Rows = 4, SheetWidth = 1600, Gap = 4, Margin = 8,
            BackgroundColor = "#FFF4F4F4",
            Header = new HeaderSettings { Color = "#FF303030", TitleColor = "#FF000000", FontSize = 14, TitleFontSize = 19 },
            Timestamp = new TimestampSettings { FontSize = 14 },
            Output = new OutputSettings { Format = ImageFormat.Webp, Quality = 82 },
        },
    ];

    public static SheetSettings Default => All[0].Clone();
}
