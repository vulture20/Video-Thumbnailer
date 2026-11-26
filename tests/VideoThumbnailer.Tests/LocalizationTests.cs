using System.Text.RegularExpressions;
using VideoThumbnailer.Gui.Localization;

namespace VideoThumbnailer.Tests;

public partial class LocalizationTests
{
    private static string RepoFile(params string[] parts)
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "VideoThumbnailer.slnx"))) dir = Path.GetDirectoryName(dir);
        Assert.NotNull(dir);
        return Path.Combine([dir!, .. parts]);
    }

    [Fact]
    public void Every_entry_has_both_languages_with_matching_placeholders()
    {
        foreach (var (key, e) in Strings.Table)
        {
            Assert.False(string.IsNullOrWhiteSpace(e.En), key);
            Assert.False(string.IsNullOrWhiteSpace(e.De), key);
            Assert.Equal(Args().Matches(e.En).Select(m => m.Value).Distinct().Order(), Args().Matches(e.De).Select(m => m.Value).Distinct().Order());
        }
    }

    [Fact]
    public void All_keys_used_in_xaml_and_code_exist()
    {
        var guiDir = RepoFile("src", "VideoThumbnailer.Gui");
        var used = new HashSet<string>();
        foreach (var f in Directory.EnumerateFiles(guiDir, "*.axaml", SearchOption.AllDirectories))
            foreach (Match m in XamlKey().Matches(File.ReadAllText(f))) used.Add(m.Groups[1].Value);
        foreach (var f in Directory.EnumerateFiles(guiDir, "*.cs", SearchOption.AllDirectories).Where(f => !f.Contains("obj")))
            foreach (Match m in CodeKey().Matches(File.ReadAllText(f))) used.Add(m.Groups[1].Value);
        Assert.True(used.Count > 50, $"only {used.Count} keys found");
        Assert.All(used, k => Assert.True(Strings.Table.ContainsKey(k), $"missing key {k}"));
    }

    [Fact]
    public void Enum_choices_have_translations()
    {
        foreach (var key in Strings.Table.Keys.Where(k => k.Contains('.')))
            Assert.DoesNotContain("⟦", Loc.Get(key, "de"));
        foreach (var prefix in new[] { "Order", "Pos", "Format", "Location", "Overwrite", "Lang" })
            Assert.Contains(Strings.Table.Keys, k => k.StartsWith(prefix + "."));
        Assert.Equal("Zeilenweise", Loc.Get("Order.RowMajor", "de"));
        Assert.Equal("⟦Missing⟧", Loc.Get("Missing", "en"));
    }

    [GeneratedRegex(@"\{\d+\}")] private static partial Regex Args();
    [GeneratedRegex(@"\{l:T (\w+)\}")] private static partial Regex XamlKey();
    [GeneratedRegex(@"Loc\.T\(""(\w+)""")] private static partial Regex CodeKey();
}
