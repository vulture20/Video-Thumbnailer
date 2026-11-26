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

    [Theory]
    [InlineData("--bogus")]
    [InlineData("--columns")]
    [InlineData("--columns", "x")]
    [InlineData("--format", "bmp")]
    [InlineData("--timestamp-pos", "middle")]
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
