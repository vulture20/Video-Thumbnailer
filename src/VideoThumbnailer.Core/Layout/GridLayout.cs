using VideoThumbnailer.Core.Settings;

namespace VideoThumbnailer.Core.Layout;

public readonly record struct TileRect(int X, int Y, int Width, int Height);

/// <summary>Pure geometry of a sheet. The header height is measured by the renderer and passed in.</summary>
public sealed class GridLayout
{
    public int Columns { get; }
    public int Rows { get; }
    public int TileWidth { get; }
    public int TileHeight { get; }
    public int SheetWidth { get; }
    public int SheetHeight { get; }
    public int HeaderHeight { get; }
    public bool HeaderAtTop { get; }
    /// <summary>Rectangle of the header band (empty when there is no header).</summary>
    public TileRect HeaderRect { get; }
    /// <summary>Tile rectangles in timeline order (index 0 = earliest frame).</summary>
    public IReadOnlyList<TileRect> Tiles { get; }

    private GridLayout(int cols, int rows, int tileW, int tileH, int sheetW, int sheetH, int headerH, bool top, TileRect header, IReadOnlyList<TileRect> tiles)
    {
        Columns = cols; Rows = rows; TileWidth = tileW; TileHeight = tileH;
        SheetWidth = sheetW; SheetHeight = sheetH; HeaderHeight = headerH; HeaderAtTop = top;
        HeaderRect = header; Tiles = tiles;
    }

    /// <summary>Effective columns/rows for a video with the given display aspect ratio.</summary>
    public static (int Columns, int Rows) EffectiveGrid(SheetSettings s, double aspect)
    {
        if (s.PortraitSwapGrid && aspect < 1.0 && s.Rows > s.Columns)
            return (s.Rows, s.Columns);
        return (s.Columns, s.Rows);
    }

    public static int ComputeTileWidth(SheetSettings s, int columns)
    {
        if (s.TileWidth is { } tw) return tw;
        var avail = s.SheetWidth - 2 * s.Margin - (columns - 1) * s.Gap;
        return Math.Max(16, avail / columns);
    }

    public static int ComputeTileHeight(int tileWidth, double aspect)
    {
        if (aspect <= 0) aspect = 16.0 / 9.0;
        var h = (int)Math.Round(tileWidth / aspect);
        return Math.Max(2, h + (h & 1)); // even height keeps scalers happy
    }

    public static GridLayout Create(SheetSettings s, double aspect, int headerHeight, Random? random = null)
    {
        var (cols, rows) = EffectiveGrid(s, aspect);
        var tileW = ComputeTileWidth(s, cols);
        var tileH = ComputeTileHeight(tileW, aspect);
        var gridW = cols * tileW + (cols - 1) * s.Gap;
        var gridH = rows * tileH + (rows - 1) * s.Gap;
        var sheetW = gridW + 2 * s.Margin;
        var headerBlock = headerHeight > 0 ? headerHeight + s.Gap : 0;
        var sheetH = gridH + 2 * s.Margin + headerBlock;

        var top = s.Header.Position is HeaderPosition.TopLeft or HeaderPosition.TopRight;
        var gridTop = s.Margin + (top ? headerBlock : 0);
        var header = headerHeight > 0
            ? new TileRect(s.Margin, top ? s.Margin : gridTop + gridH + s.Gap, gridW, headerHeight)
            : default;

        // Cells in reading order, then map timeline index -> cell according to the order setting.
        var count = cols * rows;
        var cellIndex = new int[count];
        for (var i = 0; i < count; i++)
        {
            cellIndex[i] = s.Order switch
            {
                TileOrder.ColumnMajor => (i % rows) * cols + i / rows,
                _ => i,
            };
        }
        if (s.Order == TileOrder.Random)
        {
            var rnd = random ?? new Random(12345);
            rnd.Shuffle(cellIndex);
        }

        var tiles = new TileRect[count];
        for (var i = 0; i < count; i++)
        {
            var cell = cellIndex[i];
            var c = cell % cols;
            var r = cell / cols;
            tiles[i] = new TileRect(s.Margin + c * (tileW + s.Gap), gridTop + r * (tileH + s.Gap), tileW, tileH);
        }
        return new GridLayout(cols, rows, tileW, tileH, sheetW, sheetH, headerHeight, top, header, tiles);
    }
}
