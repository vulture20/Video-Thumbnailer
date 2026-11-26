using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace VideoThumbnailer.Gui.Controls;

/// <summary>Tiny columns × rows symbol for the preset tiles (at most 8 × 6 cells are drawn).</summary>
public sealed class GridGlyph : Control
{
    public static readonly StyledProperty<int> ColumnsProperty = AvaloniaProperty.Register<GridGlyph, int>(nameof(Columns), 4);
    public static readonly StyledProperty<int> RowsProperty = AvaloniaProperty.Register<GridGlyph, int>(nameof(Rows), 5);
    public static readonly StyledProperty<IBrush?> BrushProperty = AvaloniaProperty.Register<GridGlyph, IBrush?>(nameof(Brush));

    static GridGlyph() => AffectsRender<GridGlyph>(ColumnsProperty, RowsProperty, BrushProperty);

    public int Columns { get => GetValue(ColumnsProperty); set => SetValue(ColumnsProperty, value); }
    public int Rows { get => GetValue(RowsProperty); set => SetValue(RowsProperty, value); }
    public IBrush? Brush { get => GetValue(BrushProperty); set => SetValue(BrushProperty, value); }

    protected override Size MeasureOverride(Size availableSize) => new(40, 30);

    public override void Render(DrawingContext context)
    {
        var brush = Brush ?? Brushes.Gray;
        var cols = Math.Clamp(Columns, 1, 8);
        var rows = Math.Clamp(Rows, 1, 6);
        const double gap = 1.5;
        var w = (Bounds.Width - (cols - 1) * gap) / cols;
        var h = (Bounds.Height - (rows - 1) * gap) / rows;
        for (var r = 0; r < rows; r++)
            for (var c = 0; c < cols; c++)
                context.FillRectangle(brush, new Rect(c * (w + gap), r * (h + gap), w, h), 0.8f);
    }
}
