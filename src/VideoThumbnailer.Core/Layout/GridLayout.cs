using VideoThumbnailer.Core.Settings;

namespace VideoThumbnailer.Core.Layout;

public readonly record struct TileRect(int X, int Y, int Width, int Height);

/// <summary>Pure geometry of a sheet. The header height is measured by the renderer and passed in.</summary>
public sealed class GridLayout
{
    /// <summary>Columns of the grid (cells of a special grid).</summary>
    public int Columns { get; }
    /// <summary>Rows of the grid (cells of a special grid).</summary>
    public int Rows { get; }
    /// <summary>Size of one cell; tiles of a special grid can span several cells.</summary>
    public int TileWidth { get; }
    public int TileHeight { get; }
    public int SheetWidth { get; }
    public int SheetHeight { get; }
    public int HeaderHeight { get; }
    public bool HeaderAtTop { get; }
    /// <summary>Display aspect ratio of the video the layout was made for.</summary>
    public double Aspect { get; }
    /// <summary>The special grid in use (already turned for portrait videos), null for a plain grid.</summary>
    public GridTemplate? Template { get; }
    /// <summary>Rectangle of the header band (empty when there is no header).</summary>
    public TileRect HeaderRect { get; }
    /// <summary>Tile rectangles in timeline order (index 0 = earliest frame).</summary>
    public IReadOnlyList<TileRect> Tiles { get; }

    private GridLayout(int cols, int rows, int tileW, int tileH, int sheetW, int sheetH, int headerH, bool top, double aspect,
                       GridTemplate? template, TileRect header, IReadOnlyList<TileRect> tiles)
    {
        Columns = cols; Rows = rows; TileWidth = tileW; TileHeight = tileH;
        SheetWidth = sheetW; SheetHeight = sheetH; HeaderHeight = headerH; HeaderAtTop = top; Aspect = aspect;
        Template = template; HeaderRect = header; Tiles = tiles;
    }

    /// <summary>
    /// Size in which to capture the frame of tile <paramref name="index"/>: its width and the matching height for the
    /// video's aspect ratio. Equals the tile for a plain grid; a big tile of a special grid is a few pixels off
    /// (the gaps it spans), the renderer crops that.
    /// </summary>
    public (int Width, int Height) FrameSize(int index)
    {
        var t = Tiles[index];
        return t.Width == TileWidth && t.Height == TileHeight ? (TileWidth, TileHeight) : (t.Width, ComputeTileHeight(t.Width, Aspect));
    }

    /// <summary>Effective columns/rows for a video with the given display aspect ratio.</summary>
    public static (int Columns, int Rows) EffectiveGrid(SheetSettings s, double aspect)
    {
        if (EffectiveTemplate(s, aspect) is { } t) return (t.Columns, t.Rows);
        if (s.PortraitSwapGrid && aspect < 1.0 && s.Rows > s.Columns)
            return (s.Rows, s.Columns);
        return (s.Columns, s.Rows);
    }

    /// <summary>
    /// The special grid for a video with this aspect ratio: turned like the plain grid for portrait videos, null when
    /// there is none or it is not valid.
    /// </summary>
    public static GridTemplate? EffectiveTemplate(SheetSettings s, double aspect)
    {
        if (s.Template is not { IsValid: true } t) return null;
        return s.PortraitSwapGrid && aspect < 1.0 && t.Rows > t.Columns ? t.Transposed() : t;
    }

    public static int ComputeTileWidth(SheetSettings s, int columns)
    {
        if (s.TileWidth is { } tw) return tw;
        return WidthFor(s.SheetWidth, s, columns);
    }

    private static int WidthFor(int sheetWidth, SheetSettings s, int columns)
    {
        var avail = sheetWidth - 2 * s.Margin - (columns - 1) * s.Gap;
        return Math.Max(16, avail / columns);
    }

    /// <summary>
    /// Width of one cell. With "fit to screen" and a known screen it comes from the screen (for width and height:
    /// the largest size at which the whole sheet fits), otherwise from the fixed sheet or tile width.
    /// </summary>
    public static int ComputeCellWidth(SheetSettings s, int columns, int rows, double aspect, int headerHeight, ScreenSize? screenSize = null)
    {
        if (s.FitToScreen == ScreenFit.Off || (screenSize ?? ScreenFitting.Current) is not { } screen)
            return ComputeTileWidth(s, columns);
        if (aspect <= 0) aspect = 16.0 / 9.0;
        var w = WidthFor(screen.Width, s, columns);
        if (s.FitToScreen == ScreenFit.WidthAndHeight)
        {
            var headerBlock = headerHeight > 0 ? headerHeight + s.Gap : 0;
            var maxH = (screen.Height - 2 * s.Margin - headerBlock - (rows - 1) * s.Gap) / rows;
            var byHeight = (int)Math.Floor(maxH * aspect);
            while (byHeight > 16 && ComputeTileHeight(byHeight, aspect) > maxH) byHeight--;
            // Heights are rounded to even numbers, so a slightly wider tile may still have the same height.
            while (byHeight < w && ComputeTileHeight(byHeight + 1, aspect) <= maxH) byHeight++;
            w = Math.Min(w, byHeight);
        }
        return Math.Max(16, w);
    }

    public static int ComputeTileHeight(int tileWidth, double aspect)
    {
        if (aspect <= 0) aspect = 16.0 / 9.0;
        var h = (int)Math.Round(tileWidth / aspect);
        return Math.Max(2, h + (h & 1)); // even height keeps scalers happy
    }

    /// <param name="screen">Screen for "fit to screen"; null = <see cref="ScreenFitting.Current"/>.</param>
    public static GridLayout Create(SheetSettings s, double aspect, int headerHeight, Random? random = null, ScreenSize? screen = null)
    {
        var template = EffectiveTemplate(s, aspect);
        var (cols, rows) = EffectiveGrid(s, aspect);
        var tileW = ComputeCellWidth(s, cols, rows, aspect, headerHeight, screen);
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

        // Cells (top left cell and size) in timeline order.
        List<TemplateTile> cells;
        if (template is not null)
        {
            cells = template.Ordered(s.Order).ToList();
        }
        else
        {
            var count = cols * rows;
            cells = new List<TemplateTile>(count);
            for (var i = 0; i < count; i++)
            {
                var cell = s.Order switch
                {
                    TileOrder.ColumnMajor => (i % rows) * cols + i / rows,
                    _ => i,
                };
                cells.Add(new TemplateTile(cell % cols, cell / cols));
            }
        }
        if (s.Order == TileOrder.Random)
        {
            var rnd = random ?? new Random(12345);
            var shuffled = cells.ToArray();
            rnd.Shuffle(shuffled);
            cells = [.. shuffled];
        }

        var tiles = new TileRect[cells.Count];
        for (var i = 0; i < cells.Count; i++)
        {
            var c = cells[i];
            tiles[i] = new TileRect(s.Margin + c.X * (tileW + s.Gap), gridTop + c.Y * (tileH + s.Gap),
                                    c.Size * tileW + (c.Size - 1) * s.Gap, c.Size * tileH + (c.Size - 1) * s.Gap);
        }
        return new GridLayout(cols, rows, tileW, tileH, sheetW, sheetH, headerHeight, top, aspect, template, header, tiles);
    }
}
