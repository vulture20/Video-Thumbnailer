using VideoThumbnailer.Core.Layout;
using VideoThumbnailer.Core.Rendering;
using VideoThumbnailer.Core.Settings;

namespace VideoThumbnailer.Tests;

public class SheetPreviewTests
{
    [Fact]
    public void Preview_has_the_geometry_of_the_real_sheet()
    {
        var s = BuiltInPresets.Default.Clone();
        var info = SheetPreview.SampleInfo("/videos/Lofoten.mp4");
        using var r = SheetPreview.Render(s, info, maxWidth: 10_000);
        var layout = GridLayout.Create(s, 16.0 / 9.0, SheetRenderer.MeasureHeader(s, info));
        Assert.Equal(layout.SheetWidth, r.SheetWidth);
        Assert.Equal(layout.SheetHeight, r.SheetHeight);
        Assert.Equal(20, r.Tiles);
        Assert.Equal(r.SheetWidth, r.Bitmap.Width);
    }

    [Fact]
    public void Preview_is_scaled_down_but_reports_full_size_and_follows_header_switch()
    {
        var s = BuiltInPresets.Default.Clone();
        s.SheetWidth = 3000;
        var info = SheetPreview.SampleInfo("x.mkv");
        using var with = SheetPreview.Render(s, info, maxWidth: 600);
        Assert.Equal(600, with.Bitmap.Width);
        Assert.InRange(with.SheetWidth, 2990, 3000); // tile widths are whole pixels
        s.Header.Enabled = false;
        using var without = SheetPreview.Render(s, info, maxWidth: 600);
        Assert.True(without.SheetHeight < with.SheetHeight);
    }
}
