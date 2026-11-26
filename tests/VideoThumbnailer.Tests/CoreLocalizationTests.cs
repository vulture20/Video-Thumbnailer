using System.Text.RegularExpressions;
using VideoThumbnailer.Cli;
using VideoThumbnailer.Core.Localization;
using VideoThumbnailer.Core.Settings;

namespace VideoThumbnailer.Tests;

public partial class CoreLocalizationTests
{
    [Theory]
    [InlineData("de", "de")]
    [InlineData("DE-at", "de")]
    [InlineData("deutsch", "de")]
    [InlineData("en_US.UTF-8", "en")]
    [InlineData("auto", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void Normalize_maps_language_names(string? input, string? expected) =>
        Assert.Equal(expected, Lang.Normalize(input));

    [Fact]
    public void Normalize_returns_null_for_unknown_languages() =>
        Assert.Null(Lang.Normalize("xx"));

    [Fact]
    public void Cli_lang_option_is_applied_and_removed()
    {
        var before = Lang.Current;
        try
        {
            Assert.Equal(["presets", "list"], CliApp.ApplyLanguage(["--lang", "de", "presets", "list"]));
            Assert.Equal(Lang.German, Lang.Current);
            Assert.Equal(["--help"], CliApp.ApplyLanguage(["--lang=en", "--help"]));
            Assert.Equal(Lang.English, Lang.Current);
            Assert.Throws<UsageException>(() => CliApp.ApplyLanguage(["--lang", "xx"]));
        }
        finally { Lang.Current = before; }
    }

    [Fact]
    public void Cli_sheet_lang_sets_text_language()
    {
        var o = ArgParser.ParseGenerate(["--sheet-lang", "de", "a.mp4"]);
        var s = new SheetSettings();
        foreach (var apply in o.Overrides) apply(s);
        Assert.Equal("de", s.EffectiveTextLanguage);
        Assert.Throws<UsageException>(() => ArgParser.ParseGenerate(["--sheet-lang", "fr", "a.mp4"]));
    }

    [Fact]
    public void Core_and_cli_tables_are_complete()
    {
        foreach (var key in CoreText.Keys) Check(key, CoreText.Entry(key));
        foreach (var (key, e) in CliText.Table) Check(key, e);

        static void Check(string key, (string En, string De) e)
        {
            Assert.False(string.IsNullOrWhiteSpace(e.En), key);
            Assert.False(string.IsNullOrWhiteSpace(e.De), key);
            Assert.Equal(Args().Matches(e.En).Select(m => m.Value).Distinct().Order(), Args().Matches(e.De).Select(m => m.Value).Distinct().Order());
        }
    }

    [Fact]
    public void Default_header_follows_sheet_language()
    {
        var h = new HeaderSettings();
        Assert.Contains("Größe:", h.EffectiveTemplate("de"));
        Assert.Contains("Size:", h.EffectiveTemplate("en"));
        Assert.True(CoreText.IsDefaultHeaderTemplate(CoreText.DefaultHeaderTemplate("en").Replace("\n", "\r\n")));
        Assert.False(CoreText.IsDefaultHeaderTemplate("{filename}"));
        h.Template = "{filename}";
        Assert.Equal("{filename}", h.EffectiveTemplate("de"));
    }

    [Fact]
    public void Built_in_preset_descriptions_are_translated()
    {
        var std = BuiltInPresets.Default;
        Assert.StartsWith("4 × 5 Kacheln", BuiltInPresets.Describe(std, "de"));
        Assert.StartsWith("4 × 5 tiles", BuiltInPresets.Describe(std, "en"));
        std.Description = "mine";
        Assert.Equal("mine", BuiltInPresets.Describe(std, "de"));
    }

    [Fact]
    public void Help_text_exists_in_both_languages()
    {
        Assert.Contains("--lang", HelpText.English);
        Assert.Contains("--lang", HelpText.German);
        Assert.Contains("--sheet-lang", HelpText.German);
    }

    [GeneratedRegex(@"\{\d+\}")] private static partial Regex Args();
}
