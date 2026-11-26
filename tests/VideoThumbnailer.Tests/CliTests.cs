using VideoThumbnailer.Cli;
using VideoThumbnailer.Core.Settings;

namespace VideoThumbnailer.Tests;

public class CliTests
{
    [Fact]
    public void Parses_options_and_inputs()
    {
        var o = ArgParser.ParseGenerate(["-p", "Compact", "--columns=6", "--rows", "3", "-f", "webp", "-o", "/out", "--keep-structure",
                                         "--order", "column", "--no-header", "-j", "3", "--yes", "a.mp4", "folder"]);
        Assert.Equal("Compact", o.Preset);
        Assert.Equal(3, o.Jobs);
        Assert.True(o.Yes);
        Assert.Equal(["a.mp4", "folder"], o.Inputs);

        var s = new SheetSettings();
        foreach (var apply in o.Overrides) apply(s);
        Assert.Equal(6, s.Columns);
        Assert.Equal(3, s.Rows);
        Assert.Equal(ImageFormat.Webp, s.Output.Format);
        Assert.Equal(OutputLocation.OutputFolder, s.Output.Location);
        Assert.Equal("/out", s.Output.Folder);
        Assert.True(s.Output.KeepFolderStructure);
        Assert.Equal(TileOrder.ColumnMajor, s.Order);
        Assert.False(s.Header.Enabled);
    }

    /// <summary>
    /// Every setting of a preset can be changed on the command line. Maps each JSON path of <see cref="SheetSettings"/>
    /// to arguments that change it away from the default. A new setting without an option makes this test fail.
    /// </summary>
    private static readonly Dictionary<string, string[]> OptionFor = new()
    {
        ["description"] = ["--description", "d"],
        ["columns"] = ["-c", "7"], ["rows"] = ["--rows", "2"], ["sheetWidth"] = ["-w", "1000"], ["tileWidth"] = ["--tile-width", "300"],
        ["gap"] = ["--gap", "1"], ["margin"] = ["--margin", "1"], ["portraitSwapGrid"] = ["--no-portrait-swap"],
        ["order"] = ["--order", "random"], ["skipStartPercent"] = ["--skip-start", "5"], ["skipEndPercent"] = ["--skip-end", "5"],
        ["skipBlackFrames"] = ["--no-skip-black"], ["blackFrameSearchSeconds"] = ["--black-search", "5"],
        ["backgroundColor"] = ["--background", "#000000"], ["fontFamily"] = ["--font", "Arial"], ["textLanguage"] = ["--sheet-lang", "de"],
        ["timestamp.enabled"] = ["--no-timestamps"], ["timestamp.position"] = ["--timestamp-pos", "TopLeft"],
        ["timestamp.fontSize"] = ["--timestamp-size", "20"], ["timestamp.bold"] = ["--no-timestamp-bold"],
        ["timestamp.color"] = ["--timestamp-color", "#FFFF0000"], ["timestamp.opacity"] = ["--timestamp-opacity", "50"],
        ["timestamp.outlineColor"] = ["--outline-color", "#FF00FF00"], ["timestamp.outlineWidth"] = ["--outline-width", "1.5"],
        ["timestamp.shadow"] = ["--no-shadow"], ["timestamp.shadowColor"] = ["--shadow-color", "#80FFFFFF"],
        ["timestamp.shadowOffset"] = ["--shadow-offset", "4"], ["timestamp.padding"] = ["--timestamp-padding", "2"],
        ["timestamp.showMilliseconds"] = ["--milliseconds"],
        ["header.enabled"] = ["--no-header"], ["header.position"] = ["--header-pos", "BottomRight"],
        ["header.template"] = ["--header-template", "{filename}"], ["header.fontSize"] = ["--header-size", "12"],
        ["header.titleFontSize"] = ["--title-size", "30"], ["header.color"] = ["--header-color", "#FF808080"],
        ["header.titleColor"] = ["--title-color", "#FFFFFF00"], ["header.lineSpacing"] = ["--line-spacing", "1.5"],
        ["output.format"] = ["-f", "png"], ["output.quality"] = ["-q", "50"], ["output.location"] = ["--subfolder", "x"],
        ["output.folder"] = ["-o", "/out"], ["output.keepFolderStructure"] = ["--keep-structure"],
        ["output.subfolderName"] = ["--subfolder", "sheets"], ["output.fileNamePattern"] = ["--pattern", "{name}_s"],
        ["output.overwrite"] = ["--overwrite", "skip"], ["output.saveFrames"] = ["--frames"],
        ["output.framesFolderPattern"] = ["--frames-folder", "f"],
        ["animation.enabled"] = ["-a", "gif"], ["animation.format"] = ["-a", "webm"],
        ["animation.clipSeconds"] = ["--clip-seconds", "5"], ["animation.framesPerSecond"] = ["--fps", "20"],
    };

    // Not settings of the sheet: the preset's file format version and its name (set with --save-preset).
    private static readonly string[] NotOptions = ["schemaVersion", "name"];

    private static IEnumerable<string> JsonPaths(System.Text.Json.Nodes.JsonObject o, string prefix = "") =>
        o.SelectMany(kv => kv.Value is System.Text.Json.Nodes.JsonObject child
            ? JsonPaths(child, prefix + kv.Key + ".")
            : [prefix + kv.Key]);

    private static System.Text.Json.Nodes.JsonObject ToJson(SheetSettings s)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(s, new System.Text.Json.JsonSerializerOptions(SettingsJson.Options)
            { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.Never });
        return System.Text.Json.Nodes.JsonNode.Parse(json)!.AsObject();
    }

    private static string? ValueAt(System.Text.Json.Nodes.JsonObject o, string path)
    {
        System.Text.Json.Nodes.JsonNode? n = o;
        foreach (var part in path.Split('.')) n = n?[part];
        return n?.ToJsonString();
    }

    [Fact]
    public void Every_preset_setting_has_a_command_line_option()
    {
        var paths = JsonPaths(ToJson(new SheetSettings())).Except(NotOptions).Order().ToList();
        Assert.Equal(paths, OptionFor.Keys.Order().ToList());
    }

    [Fact]
    public void Each_option_changes_its_setting()
    {
        var defaults = ToJson(new SheetSettings());
        foreach (var (path, args) in OptionFor)
        {
            var s = new SheetSettings();
            foreach (var apply in ArgParser.ParseGenerate(args).Overrides) apply(s);
            Assert.True(ValueAt(defaults, path) != ValueAt(ToJson(s), path), $"{string.Join(' ', args)} does not change {path}");
        }
    }

    [Fact]
    public void Parses_timestamp_and_header_styles()
    {
        var o = ArgParser.ParseGenerate(["--timestamp-color", "#FFFF0000", "--timestamp-opacity", "0.4", "--outline-width", "0",
                                         "--shadow-color=#40000000", "--milliseconds", "--title-color", "#00FF00", "--line-spacing", "1.1"]);
        var s = new SheetSettings();
        s.Timestamp.Shadow = false;
        foreach (var apply in o.Overrides) apply(s);
        Assert.Equal("#FFFF0000", s.Timestamp.Color);
        Assert.Equal(0.4, s.Timestamp.Opacity);
        Assert.Equal(0, s.Timestamp.OutlineWidth);
        Assert.True(s.Timestamp.Shadow); // a shadow color switches the shadow on
        Assert.Equal("#40000000", s.Timestamp.ShadowColor);
        Assert.True(s.Timestamp.ShowMilliseconds);
        Assert.Equal("#00FF00", s.Header.TitleColor);
        Assert.Equal(1.1f, s.Header.LineSpacing);
    }

    [Theory]
    [InlineData("--timestamp-color", "red-ish")]
    [InlineData("--background", "#12")]
    [InlineData("--shadow-offset", "x")]
    public void Rejects_bad_colors_and_numbers(params string[] args) =>
        Assert.Throws<UsageException>(() => ArgParser.ParseGenerate(args));

    [Theory]
    [InlineData("--bogus")]
    [InlineData("--columns")]
    [InlineData("--columns", "x")]
    [InlineData("--format", "bmp")]
    [InlineData("--timestamp-pos", "middle")]
    [InlineData("--times", "1:00,abc")]
    [InlineData("--times", "1:75")]
    [InlineData("--times", "5+0")]
    public void Rejects_bad_arguments(params string[] args)
    {
        Assert.Throws<UsageException>(() => ArgParser.ParseGenerate(args));
    }

    [Fact]
    public void Enum_values_accept_kebab_case()
    {
        Assert.Equal(OverlayPosition.BottomLeft, ArgParser.ParseEnum<OverlayPosition>("bottom-left", "--x"));
    }
}
