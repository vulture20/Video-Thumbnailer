using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VideoThumbnailer.Core.Layout;
using VideoThumbnailer.Core.Settings;
using VideoThumbnailer.Gui.Localization;

namespace VideoThumbnailer.Gui.ViewModels;

/// <summary>
/// Grid editor (#36): a raster of columns × rows cells; dragging over cells places a square tile (1 × 1, 2 × 2 …),
/// a right click or Del removes one, cells without a tile stay background (room for a logo or text).
/// </summary>
public sealed partial class GridEditorViewModel : ObservableObject
{
    public const int MaxColumns = 50;
    public const int MaxRows = 100;

    private readonly TemplateStore _store;

    [ObservableProperty] private decimal _columns;
    [ObservableProperty] private decimal _rows;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(IsExistingOwn))] [NotifyCanExecuteChangedFor(nameof(DeleteOwnCommand))] private string _name;
    [ObservableProperty] private string _description;
    /// <summary>Aspect ratio of one cell (that of the video): 16:9 by default.</summary>
    public double CellAspect { get; init; } = 16.0 / 9;

    public List<TemplateTile> Tiles { get; } = [];
    /// <summary>Raised whenever tiles or the raster changed (the canvas redraws).</summary>
    public event Action? Changed;

    public string InfoText => Loc.T("GridEditorInfo", Tiles.Count, (int)Columns, (int)Rows, EmptyCells);
    public int EmptyCells => (int)Columns * (int)Rows - Tiles.Sum(t => t.Size * t.Size);
    public bool IsValid => ToTemplate().IsValid;
    public bool IsExistingOwn => _store.Find(Name.Trim()) is not null;

    /// <summary>Set by the window: the result is to be saved as an own grid as well.</summary>
    public bool SaveAsOwn { get; set; }
    /// <summary>Own grids changed (saved or deleted) while the editor was open.</summary>
    public bool OwnGridsChanged { get; private set; }

    public GridEditorViewModel(GridTemplate? start, int plainColumns, int plainRows, TemplateStore? store = null)
    {
        _store = store ?? new TemplateStore();
        if (start is { IsValid: true })
        {
            _columns = start.Columns;
            _rows = start.Rows;
            _name = BuiltInTemplates.IsBuiltIn(start) ? Loc.T("Layout." + start.Name) : start.Name;
            _description = start.Description ?? "";
            Tiles.AddRange(start.Tiles.Select(t => new TemplateTile(t.X, t.Y, t.Size)));
        }
        else
        {
            _columns = Math.Clamp(plainColumns, 1, MaxColumns);
            _rows = Math.Clamp(plainRows, 1, MaxRows);
            _name = Loc.T("GridEditorNewName");
            _description = "";
            Fill();
        }
    }

    partial void OnColumnsChanged(decimal value) => Resized();
    partial void OnRowsChanged(decimal value) => Resized();

    private void Resized()
    {
        Tiles.RemoveAll(t => t.X + t.Size > (int)Columns || t.Y + t.Size > (int)Rows);
        Notify();
    }

    private void Notify()
    {
        OnPropertyChanged(nameof(InfoText));
        OnPropertyChanged(nameof(IsValid));
        Changed?.Invoke();
    }

    public TemplateTile? TileAt(int x, int y) => Tiles.FirstOrDefault(t => x >= t.X && x < t.X + t.Size && y >= t.Y && y < t.Y + t.Size);

    /// <summary>
    /// The square a drag from cell (x0, y0) to (x1, y1) covers: as big as the longer side of the drag, growing from the
    /// start cell towards the end cell, kept inside the raster.
    /// </summary>
    public (int X, int Y, int Size) Square(int x0, int y0, int x1, int y1)
    {
        int cols = (int)Columns, rows = (int)Rows;
        x0 = Math.Clamp(x0, 0, cols - 1); y0 = Math.Clamp(y0, 0, rows - 1);
        x1 = Math.Clamp(x1, 0, cols - 1); y1 = Math.Clamp(y1, 0, rows - 1);
        var size = Math.Max(Math.Abs(x1 - x0), Math.Abs(y1 - y0)) + 1;
        // limited by the room in the direction of the drag
        var roomX = x1 >= x0 ? cols - x0 : x0 + 1;
        var roomY = y1 >= y0 ? rows - y0 : y0 + 1;
        size = Math.Min(size, Math.Min(roomX, roomY));
        var x = x1 >= x0 ? x0 : x0 - size + 1;
        var y = y1 >= y0 ? y0 : y0 - size + 1;
        return (x, y, size);
    }

    /// <summary>Places the square of a drag; tiles it covers are replaced.</summary>
    public void Place(int x0, int y0, int x1, int y1)
    {
        var (x, y, size) = Square(x0, y0, x1, y1);
        Tiles.RemoveAll(t => t.X < x + size && x < t.X + t.Size && t.Y < y + size && y < t.Y + t.Size);
        Tiles.Add(new TemplateTile(x, y, size));
        Notify();
    }

    public bool RemoveAt(int x, int y)
    {
        if (TileAt(x, y) is not { } t) return false;
        Tiles.Remove(t);
        Notify();
        return true;
    }

    /// <summary>Fills all free cells with small tiles.</summary>
    [RelayCommand]
    public void Fill()
    {
        for (var y = 0; y < (int)Rows; y++)
            for (var x = 0; x < (int)Columns; x++)
                if (TileAt(x, y) is null) Tiles.Add(new TemplateTile(x, y));
        Notify();
    }

    [RelayCommand]
    public void Clear()
    {
        Tiles.Clear();
        Notify();
    }

    /// <summary>Number of each tile in timeline order (row by row), for the labels in the editor.</summary>
    public IReadOnlyDictionary<TemplateTile, int> Order() =>
        Tiles.OrderBy(t => t.Y).ThenBy(t => t.X).Select((t, i) => (t, i)).ToDictionary(p => p.t, p => p.i + 1);

    public GridTemplate ToTemplate() => new()
    {
        Name = string.IsNullOrWhiteSpace(Name) ? Loc.T("GridEditorNewName") : Name.Trim(),
        Description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim(),
        Columns = (int)Columns,
        Rows = (int)Rows,
        Tiles = Tiles.OrderBy(t => t.Y).ThenBy(t => t.X).Select(t => new TemplateTile(t.X, t.Y, t.Size)).ToList(),
    };

    /// <summary>Saves the grid as an own grid ("&lt;Name&gt;.vtgrid"); returns the error text or null.</summary>
    public string? SaveOwn()
    {
        var t = ToTemplate();
        if (t.Validate() is { Count: > 0 } problems) return string.Join(" ", problems);
        try
        {
            _store.Save(t);
            OwnGridsChanged = true;
            OnPropertyChanged(nameof(IsExistingOwn));
            DeleteOwnCommand.NotifyCanExecuteChanged();
            return null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return e.Message; }
    }

    [RelayCommand(CanExecute = nameof(IsExistingOwn))]
    private void DeleteOwn()
    {
        try
        {
            if (_store.Delete(Name.Trim())) OwnGridsChanged = true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { /* stays */ }
        OnPropertyChanged(nameof(IsExistingOwn));
        DeleteOwnCommand.NotifyCanExecuteChanged();
    }
}
