using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace VideoThumbnailer.Gui.Controls;

/// <summary>
/// Tiny columns × rows symbol for the preset and layout tiles (at most 8 × 6 cells are drawn). With a
/// <see cref="Template"/> its tiles are drawn instead, big ones spanning their cells.
/// </summary>
public sealed class GridGlyph : Control
{
    public static readonly StyledProperty<int> ColumnsProperty = AvaloniaProperty.Register<GridGlyph, int>(nameof(Columns), 4);
    public static readonly StyledProperty<int> RowsProperty = AvaloniaProperty.Register<GridGlyph, int>(nameof(Rows), 5);
    public static readonly StyledProperty<IBrush?> BrushProperty = AvaloniaProperty.Register<GridGlyph, IBrush?>(nameof(Brush));
    public static readonly StyledProperty<Core.Layout.GridTemplate?> TemplateProperty =
        AvaloniaProperty.Register<GridGlyph, Core.Layout.GridTemplate?>(nameof(Template));

    static GridGlyph() => AffectsRender<GridGlyph>(ColumnsProperty, RowsProperty, BrushProperty, TemplateProperty);

    public Core.Layout.GridTemplate? Template { get => GetValue(TemplateProperty); set => SetValue(TemplateProperty, value); }

    public int Columns { get => GetValue(ColumnsProperty); set => SetValue(ColumnsProperty, value); }
    public int Rows { get => GetValue(RowsProperty); set => SetValue(RowsProperty, value); }
    public IBrush? Brush { get => GetValue(BrushProperty); set => SetValue(BrushProperty, value); }

    protected override Size MeasureOverride(Size availableSize) => new(40, 30);

    public override void Render(DrawingContext context)
    {
        var brush = Brush ?? Brushes.Gray;
        if (Template is { IsValid: true } t)
        {
            RenderTemplate(context, brush, t);
            return;
        }
        var cols = Math.Clamp(Columns, 1, 8);
        var rows = Math.Clamp(Rows, 1, 6);
        const double gap = 1.5;
        var w = (Bounds.Width - (cols - 1) * gap) / cols;
        var h = (Bounds.Height - (rows - 1) * gap) / rows;
        for (var r = 0; r < rows; r++)
            for (var c = 0; c < cols; c++)
                context.FillRectangle(brush, new Rect(c * (w + gap), r * (h + gap), w, h), 0.8f);
    }

    private void RenderTemplate(DrawingContext context, IBrush brush, Core.Layout.GridTemplate t)
    {
        var gap = t.Columns > 6 || t.Rows > 6 ? 1.0 : 1.5;
        var w = (Bounds.Width - (t.Columns - 1) * gap) / t.Columns;
        var h = (Bounds.Height - (t.Rows - 1) * gap) / t.Rows;
        foreach (var tile in t.Tiles)
        {
            var rect = new Rect(tile.X * (w + gap), tile.Y * (h + gap), tile.Size * w + (tile.Size - 1) * gap, tile.Size * h + (tile.Size - 1) * gap);
            context.FillRectangle(brush, rect, 0.8f);
        }
    }
}
