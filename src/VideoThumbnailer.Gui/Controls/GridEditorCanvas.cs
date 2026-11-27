using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using VideoThumbnailer.Gui.ViewModels;

namespace VideoThumbnailer.Gui.Controls;

/// <summary>
/// Drawing surface of the grid editor (#36): cells in the video's aspect ratio, tiles with their number in timeline
/// order. Left drag places a square tile, right click removes one; the cell under the mouse can also be removed
/// with Del.
/// </summary>
public sealed class GridEditorCanvas : Control
{
    private const double Gap = 3;
    private readonly GridEditorViewModel _editor;
    private (int X, int Y)? _dragStart;
    private (int X, int Y)? _hover;

    public GridEditorCanvas(GridEditorViewModel editor)
    {
        _editor = editor;
        editor.Changed += InvalidateVisual;
        Focusable = true;
        Cursor = new Cursor(StandardCursorType.Cross);
        ClipToBounds = true;
    }

    /// <summary>Size of one cell so the whole raster fits.</summary>
    private (double W, double H, double X0, double Y0) Cell()
    {
        int cols = (int)_editor.Columns, rows = (int)_editor.Rows;
        var w = (Bounds.Width - (cols - 1) * Gap) / cols;
        var h = w / _editor.CellAspect;
        if (h * rows + (rows - 1) * Gap > Bounds.Height)
        {
            h = (Bounds.Height - (rows - 1) * Gap) / rows;
            w = h * _editor.CellAspect;
        }
        var totalW = w * cols + (cols - 1) * Gap;
        var totalH = h * rows + (rows - 1) * Gap;
        return (Math.Max(1, w), Math.Max(1, h), (Bounds.Width - totalW) / 2, (Bounds.Height - totalH) / 2);
    }

    private Rect RectOf(int x, int y, int size)
    {
        var (w, h, x0, y0) = Cell();
        return new Rect(x0 + x * (w + Gap), y0 + y * (h + Gap), size * w + (size - 1) * Gap, size * h + (size - 1) * Gap);
    }

    /// <summary>Cell under a point (clamped to the raster).</summary>
    public (int X, int Y) CellAt(Point p)
    {
        var (w, h, x0, y0) = Cell();
        var x = (int)Math.Floor((p.X - x0) / (w + Gap));
        var y = (int)Math.Floor((p.Y - y0) / (h + Gap));
        return (Math.Clamp(x, 0, (int)_editor.Columns - 1), Math.Clamp(y, 0, (int)_editor.Rows - 1));
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        Focus();
        var props = e.GetCurrentPoint(this).Properties;
        var cell = CellAt(e.GetPosition(this));
        if (props.IsRightButtonPressed) { _editor.RemoveAt(cell.X, cell.Y); e.Handled = true; return; }
        if (!props.IsLeftButtonPressed) return;
        _dragStart = cell;
        _hover = cell;
        e.Pointer.Capture(this);
        e.Handled = true;
        InvalidateVisual();
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var cell = CellAt(e.GetPosition(this));
        if (_hover == cell) return;
        _hover = cell;
        InvalidateVisual();
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_dragStart is not { } start) return;
        var end = CellAt(e.GetPosition(this));
        _dragStart = null;
        e.Pointer.Capture(null);
        _editor.Place(start.X, start.Y, end.X, end.Y);
        e.Handled = true;
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        if (_dragStart is null) { _hover = null; InvalidateVisual(); }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key is Key.Delete or Key.Back && _hover is { } h && _editor.RemoveAt(h.X, h.Y)) e.Handled = true;
    }

    private IBrush? Res(string key) => this.TryFindResource(key, ActualThemeVariant, out var v) ? v as IBrush : null;

    public override void Render(DrawingContext context)
    {
        var accent = Res("BrandBlueBrush") as ISolidColorBrush ?? new SolidColorBrush(Color.FromRgb(0x25, 0x85, 0xD0));
        var line = Res("GlyphBrush") ?? Brushes.Gray;
        var text = Res("GoBrush") ?? Brushes.Orange;
        var emptyPen = new Pen(line, 1, new DashStyle([3, 3], 0));
        for (var y = 0; y < (int)_editor.Rows; y++)
            for (var x = 0; x < (int)_editor.Columns; x++)
                context.DrawRectangle(null, emptyPen, RectOf(x, y, 1).Deflate(0.5), 3, 3);

        var order = _editor.Order();
        var fill = new SolidColorBrush(accent.Color);
        foreach (var tile in _editor.Tiles)
        {
            var r = RectOf(tile.X, tile.Y, tile.Size);
            context.DrawRectangle(fill, null, r, 3, 3);
            var label = new FormattedText(order[tile].ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                                          Typeface.Default, Math.Clamp(r.Height / 3, 9, 28), Brushes.White);
            context.DrawText(label, new Point(r.X + (r.Width - label.Width) / 2, r.Y + (r.Height - label.Height) / 2));
        }

        // the square a drag would place, or the cell under the mouse
        if (_dragStart is { } s && _hover is { } h)
        {
            var (x, y, size) = _editor.Square(s.X, s.Y, h.X, h.Y);
            context.DrawRectangle(null, new Pen(text, 2), RectOf(x, y, size).Inflate(1), 3, 3);
        }
        else if (_hover is { } hv)
        {
            context.DrawRectangle(null, new Pen(text, 1), RectOf(hv.X, hv.Y, 1).Inflate(0.5), 3, 3);
        }
    }
}
