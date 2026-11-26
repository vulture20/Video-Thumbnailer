using VideoThumbnailer.Core.Layout;
using VideoThumbnailer.Core.Settings;

namespace VideoThumbnailer.Tests;

public class LayoutTests
{
    [Fact]
    public void Timestamps_are_evenly_spread_inside_skipped_range()
    {
        var t = TimestampPlanner.Plan(100, 4, 10, 10); // usable 10..90, slot 20
        Assert.Equal([20, 40, 60, 80], t);
    }

    [Fact]
    public void Timestamps_for_zero_duration_are_zero()
    {
        Assert.All(TimestampPlanner.Plan(0, 3, 2, 2), v => Assert.Equal(0, v));
    }

    [Fact]
    public void Tile_width_fills_sheet_width()
    {
        var s = new SheetSettings { Columns = 4, Rows = 2, SheetWidth = 1000, Gap = 10, Margin = 20 };
        var l = GridLayout.Create(s, 16.0 / 9, 0);
        // (1000 - 40 - 30) / 4 = 232
        Assert.Equal(232, l.TileWidth);
        Assert.Equal(2 * 20 + 4 * 232 + 3 * 10, l.SheetWidth);
        Assert.Equal(0, l.TileHeight % 2);
        Assert.Equal(8, l.Tiles.Count);
    }

    [Fact]
    public void Portrait_video_swaps_grid_when_enabled()
    {
        var s = new SheetSettings { Columns = 4, Rows = 5, PortraitSwapGrid = true };
        Assert.Equal((5, 4), GridLayout.EffectiveGrid(s, 9.0 / 16));
        s.PortraitSwapGrid = false;
        Assert.Equal((4, 5), GridLayout.EffectiveGrid(s, 9.0 / 16));
        Assert.Equal((4, 5), GridLayout.EffectiveGrid(new SheetSettings { Columns = 4, Rows = 5 }, 16.0 / 9));
    }

    [Fact]
    public void Column_major_order_fills_columns_first()
    {
        var s = new SheetSettings { Columns = 3, Rows = 2, Order = TileOrder.ColumnMajor, Margin = 0, Gap = 0, SheetWidth = 300 };
        var l = GridLayout.Create(s, 1, 0);
        Assert.Equal(l.Tiles[0].X, l.Tiles[1].X);  // second frame is below the first
        Assert.True(l.Tiles[1].Y > l.Tiles[0].Y);
        Assert.True(l.Tiles[2].X > l.Tiles[0].X);
    }

    [Fact]
    public void Random_order_is_a_permutation()
    {
        var s = new SheetSettings { Columns = 4, Rows = 4, Order = TileOrder.Random };
        var l = GridLayout.Create(s, 1.5, 0, new Random(1));
        Assert.Equal(16, l.Tiles.Distinct().Count());
    }

    [Fact]
    public void Header_at_bottom_is_below_grid()
    {
        var s = new SheetSettings { Columns = 2, Rows = 2, Header = new HeaderSettings { Position = HeaderPosition.BottomRight } };
        var l = GridLayout.Create(s, 16.0 / 9, 80);
        Assert.False(l.HeaderAtTop);
        Assert.True(l.HeaderRect.Y > l.Tiles.Max(t => t.Y + t.Height));
        Assert.Equal(l.SheetHeight, l.HeaderRect.Y + l.HeaderRect.Height + s.Margin);
    }
}
