using System.Text.Json;
using VideoThumbnailer.Core.Localization;
using VideoThumbnailer.Core.Settings;

namespace VideoThumbnailer.Core.Layout;

/// <summary>
/// One tile of a <see cref="GridTemplate"/>: top left cell and size in cells. Tiles are always square in cells
/// (<see cref="Size"/> × <see cref="Size"/>), so every tile keeps the aspect ratio of the video.
/// </summary>
public sealed class TemplateTile
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Size { get; set; } = 1;

    public TemplateTile() { }
    public TemplateTile(int x, int y, int size = 1) { X = x; Y = y; Size = size; }
}

/// <summary>
/// Grid with tiles of different sizes ("special grid"): a raster of <see cref="Columns"/> × <see cref="Rows"/> cells
/// of the video's aspect ratio, covered by tiles of one or more cells. Stored as plain JSON in the preset
/// (<see cref="SheetSettings.Template"/>) or in a file of its own. Cells without a tile stay empty (background).
/// </summary>
public sealed class GridTemplate
{
    public const int MaxTiles = 1000;

    /// <summary>Name of the template; built-in ones are recognised by it (see <see cref="BuiltInTemplates"/>).</summary>
    public string Name { get; set; } = "Custom";
    public string? Description { get; set; }
    public int Columns { get; set; }
    public int Rows { get; set; }
    public List<TemplateTile> Tiles { get; set; } = [];

    /// <summary>Problems that make the template unusable (empty list = fine).</summary>
    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();
        if (Columns is < 1 or > 50 || Rows is < 1 or > 100)
            errors.Add(CoreText.T("TemplateSize", Name));
        if (Tiles.Count == 0)
            errors.Add(CoreText.T("TemplateEmpty", Name));
        if (Tiles.Count > MaxTiles)
            errors.Add(CoreText.T("TemplateTooMany", Name, MaxTiles));
        if (errors.Count > 0) return errors;

        var used = new bool[Columns, Rows];
        foreach (var t in Tiles)
        {
            if (t.Size < 1 || t.X < 0 || t.Y < 0 || t.X + t.Size > Columns || t.Y + t.Size > Rows)
            {
                errors.Add(CoreText.T("TemplateTileOutside", Name, t.X, t.Y, t.Size));
                continue;
            }
            for (var x = t.X; x < t.X + t.Size; x++)
                for (var y = t.Y; y < t.Y + t.Size; y++)
                {
                    if (used[x, y]) { errors.Add(CoreText.T("TemplateTileOverlap", Name, t.X, t.Y)); goto next; }
                    used[x, y] = true;
                }
            next:;
        }
        return errors;
    }

    public bool IsValid => Validate().Count == 0;

    /// <summary>Same template with columns and rows swapped (for portrait videos).</summary>
    public GridTemplate Transposed() => new()
    {
        Name = Name,
        Description = Description,
        Columns = Rows,
        Rows = Columns,
        Tiles = Tiles.Select(t => new TemplateTile(t.Y, t.X, t.Size)).ToList(),
    };

    /// <summary>
    /// Tiles in timeline order: by their top left cell in reading order (row by row, or column by column).
    /// A big tile therefore takes its turn where it starts.
    /// </summary>
    public IReadOnlyList<TemplateTile> Ordered(TileOrder order) => order == TileOrder.ColumnMajor
        ? Tiles.OrderBy(t => t.X).ThenBy(t => t.Y).ToList()
        : Tiles.OrderBy(t => t.Y).ThenBy(t => t.X).ToList();

    public GridTemplate Clone() => new()
    {
        Name = Name,
        Description = Description,
        Columns = Columns,
        Rows = Rows,
        Tiles = Tiles.Select(t => new TemplateTile(t.X, t.Y, t.Size)).ToList(),
    };

    /// <summary>Reads a template from a JSON file (only the template, or a whole preset that contains one).</summary>
    public static GridTemplate LoadFile(string path)
    {
        var json = File.ReadAllText(path);
        using var doc = JsonDocument.Parse(json, new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
        if (TryGet(doc.RootElement, "Template") is { ValueKind: JsonValueKind.Object } inner)
            return inner.Deserialize<GridTemplate>(SettingsJson.Options) ?? throw new InvalidDataException(path);
        var t = JsonSerializer.Deserialize<GridTemplate>(json, SettingsJson.Options) ?? throw new InvalidDataException(path);
        if (t.Name == "Custom") t.Name = Path.GetFileNameWithoutExtension(path);
        return t;
    }

    private static JsonElement? TryGet(JsonElement e, string name)
    {
        if (e.ValueKind != JsonValueKind.Object) return null;
        foreach (var p in e.EnumerateObject())
            if (string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)) return p.Value;
        return null;
    }
}

/// <summary>The special grids that come with the program.</summary>
public static class BuiltInTemplates
{
    private static List<TemplateTile> Smalls(int x0, int y0, int x1, int y1)
    {
        var list = new List<TemplateTile>();
        for (var y = y0; y < y1; y++)
            for (var x = x0; x < x1; x++)
                list.Add(new TemplateTile(x, y));
        return list;
    }

    public static IReadOnlyList<GridTemplate> All { get; } =
    [
        // One big picture in the middle of the first two rows, small ones left/right of it and below.
        new GridTemplate
        {
            Name = "BigTop", Columns = 4, Rows = 6,
            Tiles = [new(0, 0), new(1, 0, 2), new(3, 0), new(0, 1), new(3, 1), .. Smalls(0, 2, 4, 6)],
        },
        // One big picture in the middle, a ring of small ones around it.
        new GridTemplate
        {
            Name = "BigCenter", Columns = 4, Rows = 4,
            Tiles = [.. Smalls(0, 0, 4, 1), new(0, 1), new(1, 1, 2), new(3, 1), new(0, 2), new(3, 2), .. Smalls(0, 3, 4, 4)],
        },
        // Two big pictures side by side, small ones below.
        new GridTemplate
        {
            Name = "TwoBig", Columns = 4, Rows = 5,
            Tiles = [new(0, 0, 2), new(2, 0, 2), .. Smalls(0, 2, 4, 5)],
        },
        // The first picture big at the top left, the rest small around it.
        new GridTemplate
        {
            Name = "BigFirst", Columns = 5, Rows = 5,
            Tiles = [new(0, 0, 3), .. Smalls(3, 0, 5, 3), .. Smalls(0, 3, 5, 5)],
        },
    ];

    public static GridTemplate? Find(string name) =>
        All.FirstOrDefault(t => string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase))?.Clone();

    public static bool IsBuiltIn(GridTemplate? t) =>
        t is not null && All.Any(b => string.Equals(b.Name, t.Name, StringComparison.OrdinalIgnoreCase) && SameTiles(b, t));

    public static bool SameTiles(GridTemplate a, GridTemplate b) =>
        a.Columns == b.Columns && a.Rows == b.Rows && a.Tiles.Count == b.Tiles.Count
        && a.Tiles.OrderBy(t => t.Y).ThenBy(t => t.X).Zip(b.Tiles.OrderBy(t => t.Y).ThenBy(t => t.X))
                .All(p => p.First.X == p.Second.X && p.First.Y == p.Second.Y && p.First.Size == p.Second.Size);
}
