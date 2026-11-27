using System.Text;
using System.Text.RegularExpressions;
using VideoThumbnailer.Cli;
using VideoThumbnailer.Gui.Input;

namespace VideoThumbnailer.Tests;

/// <summary>
/// Keeps the option and shortcut tables in README.md and README.en.md in step with the code (#53). The tables sit between
/// <c>&lt;!-- cli-options:start --&gt;</c>/<c>end</c> and <c>&lt;!-- shortcuts:start --&gt;</c>/<c>end</c>.
/// After changing options or shortcuts: <c>VTHUMB_UPDATE_README=1 dotnet test --filter ReadmeTests</c> rewrites them.
/// </summary>
public partial class ReadmeTests
{
    /// <summary>German and English name of each action in <see cref="Shortcuts.All"/>, in README order.</summary>
    private static readonly (string Action, string De, string En)[] ShortcutTexts =
    [
        ("AddFiles", "Dateien hinzufügen", "Add files"),
        ("AddFolder", "Ordner hinzufügen", "Add a folder"),
        ("AddUrl", "Webadresse (URL) hinzufügen", "Add a web address (URL)"),
        ("Start", "Starten", "Start"),
        ("Pause", "Pause / Fortsetzen", "Pause / resume"),
        ("Cancel", "Abbrechen (während eines Laufs)", "Cancel (while running)"),
        ("SelectAll", "Alle Einträge der Warteschlange auswählen", "Select all queue entries"),
        ("RemoveSelected", "Ausgewählte Einträge aus der Warteschlange entfernen", "Remove the selected entries from the queue"),
        ("RequeueSelected", "Ausgewählte fertige Einträge erneut einreihen", "Re-queue the selected finished entries"),
        ("ToggleMode", "Zwischen „Einfach“ und „Erweitert“ umschalten", "Switch between \"Simple\" and \"Advanced\""),
        ("ToggleLivePreview", "Live-Vorschau ein/aus", "Live preview on/off"),
        ("ToggleQueue", "Warteschlange ein-/ausklappen", "Fold/unfold the queue"),
    ];

    /// <summary>Keys outside the main window (handled in their own windows, not in <see cref="Shortcuts"/>).</summary>
    private static readonly (string WhereDe, string WhereEn, string WinDe, string WinEn, string Mac, string De, string En)[] OtherKeys =
    [
        ("Viewer", "Viewer", "Esc", "Esc", "⎋", "schließen", "close"),
        ("Viewer", "Viewer", "Entf", "Del", "⌫", "Video (auf Wunsch mit Sheet) in den Papierkorb verschieben", "move the video (optionally with its sheet) to the trash"),
        ("Raster-Editor", "Grid editor", "Entf / Rücktaste", "Del / Backspace", "⌫", "Kachel unter dem Mauszeiger entfernen (ebenso Rechtsklick)", "remove the tile under the mouse (right click too)"),
    ];

    public static string ShortcutMarkdown(string lang)
    {
        var de = lang == "de";
        var sb = new StringBuilder(de ? "| Windows / Linux | macOS | Aktion |\n|---|---|---|\n" : "| Windows / Linux | macOS | Action |\n|---|---|---|\n");
        var or = de ? " oder " : " or ";
        foreach (var (action, textDe, textEn) in ShortcutTexts)
        {
            var keys = Shortcuts.All[action];
            var win = string.Join(or, keys.Select(k => $"<kbd>{Shortcuts.Display(k, lang, mac: false)}</kbd>"));
            var mac = string.Join(or, keys.Select(k => $"<kbd>{Shortcuts.Display(k, lang, mac: true)}</kbd>"));
            sb.Append($"| {win} | {mac} | {(de ? textDe : textEn)} |\n");
        }
        foreach (var k in OtherKeys)
            sb.Append($"| <kbd>{(de ? k.WinDe : k.WinEn)}</kbd> | <kbd>{k.Mac}</kbd> | {(de ? k.WhereDe : k.WhereEn)}: {(de ? k.De : k.En)} |\n");
        return sb.ToString();
    }

    [Fact]
    public void Every_shortcut_has_a_readme_text()
    {
        Assert.Equal(Shortcuts.All.Keys.Order(), ShortcutTexts.Select(s => s.Action).Order());
    }

    [Fact]
    public void Option_catalog_matches_the_parser()
    {
        var source = File.ReadAllText(Path.Combine(RepoRoot(), "src", "VideoThumbnailer.Cli", "ArgParser.cs"));
        var parsed = CaseLabel().Matches(source).Select(m => m.Groups[1].Value).Where(n => n != "--").ToHashSet();
        var catalog = CliOptions.AllNames.ToList();
        Assert.Equal(catalog.Count, catalog.Distinct().Count());
        Assert.Empty(parsed.Except(catalog));   // an option without documentation
        Assert.Empty(catalog.Except(parsed));   // documentation of an option that does not exist
    }

    [Fact]
    public void Every_option_is_in_both_help_texts()
    {
        foreach (var name in CliOptions.AllNames.Where(n => n.StartsWith("--", StringComparison.Ordinal)))
        {
            Assert.True(Regex.IsMatch(HelpEnglish, Regex.Escape(name) + @"(?![\w-])"), $"{name} missing in the English help");
            Assert.True(Regex.IsMatch(HelpGerman, Regex.Escape(name) + @"(?![\w-])"), $"{name} missing in the German help");
        }
    }

    private static string HelpEnglish => HelpText.English;
    private static string HelpGerman => HelpText.German;

    [Fact]
    public void Every_example_parses_and_uses_its_option()
    {
        foreach (var o in CliOptions.Groups.SelectMany(g => g.Options))
        {
            var args = SplitCommandLine(o.Example);
            Assert.Equal("vthumb", args[0]);
            Assert.Contains(args, a => o.Names.Contains(a));
            if (o.FileArgument) continue;
            var ex = Record.Exception(() => ArgParser.ParseGenerate(args.Skip(1).ToList()));
            Assert.True(ex is null, $"{o.Example}: {ex?.Message}");
        }
    }

    [Theory]
    [InlineData("README.md", "de")]
    [InlineData("README.en.md", "en")]
    public void Readme_tables_are_up_to_date(string file, string lang)
    {
        var path = Path.Combine(RepoRoot(), file);
        var readme = File.ReadAllText(path).ReplaceLineEndings("\n");
        var updated = Replace(Replace(readme, "cli-options", CliOptions.Markdown(lang)), "shortcuts", ShortcutMarkdown(lang));
        if (Environment.GetEnvironmentVariable("VTHUMB_UPDATE_README") is { Length: > 0 })
        {
            if (updated != readme) File.WriteAllText(path, updated);
            return;
        }
        Assert.True(updated == readme,
            $"{file}: tables are out of date: run VTHUMB_UPDATE_README=1 dotnet test --filter ReadmeTests");
    }

    [Fact]
    public void Help_lists_every_option_with_its_value()
    {
        foreach (var o in CliOptions.Groups.SelectMany(g => g.Options))
        {
            Assert.Contains(CliOptions.HelpNames(o, "en"), HelpText.English);
            Assert.Contains(CliOptions.HelpNames(o, "de"), HelpText.German);
        }
        Assert.All(HelpText.English.Split('\n'), l => Assert.True(l.Length <= 112, l));
        Assert.All(HelpText.German.Split('\n'), l => Assert.True(l.Length <= 112, l));
    }

    private static string Replace(string text, string marker, string content)
    {
        var start = $"<!-- {marker}:start -->";
        var end = $"<!-- {marker}:end -->";
        var a = text.IndexOf(start, StringComparison.Ordinal);
        var b = text.IndexOf(end, StringComparison.Ordinal);
        Assert.True(a >= 0 && b > a, $"README.md: markers {start} … {end} missing");
        return text[..(a + start.Length)] + "\n" + content + text[b..];
    }

    internal static string RepoRoot()
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d is not null; d = d.Parent)
            if (File.Exists(Path.Combine(d.FullName, "VideoThumbnailer.slnx"))) return d.FullName;
        throw new InvalidOperationException("repository root not found");
    }

    /// <summary>Splits like a POSIX shell for the simple cases in the examples (double quotes, backslash kept).</summary>
    internal static List<string> SplitCommandLine(string line)
    {
        var result = new List<string>();
        var cur = new StringBuilder();
        bool quoted = false, any = false;
        foreach (var c in line)
        {
            if (c == '"') { quoted = !quoted; any = true; }
            else if (c == ' ' && !quoted) { if (any) result.Add(cur.ToString()); cur.Clear(); any = false; }
            else { cur.Append(c); any = true; }
        }
        if (any) result.Add(cur.ToString());
        return result;
    }

    [GeneratedRegex("case \"(-[^\"]*)\"")]
    private static partial Regex CaseLabel();
}
