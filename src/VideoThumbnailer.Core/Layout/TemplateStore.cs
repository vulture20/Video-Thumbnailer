using System.Text.Json;
using VideoThumbnailer.Core.Settings;

namespace VideoThumbnailer.Core.Layout;

/// <summary>
/// Own special grids (#36): one JSON file "&lt;Name&gt;.vtgrid" per grid in <see cref="AppPaths.GridsDir"/>. They are
/// offered next to the built-in grids; the grid in use is still stored in the preset, so presets keep working
/// on other computers.
/// </summary>
public sealed class TemplateStore(string? directory = null)
{
    public const string FileExtension = ".vtgrid";

    public string Directory { get; } = directory ?? AppPaths.GridsDir;

    public static bool IsTemplateFile(string path) => path.EndsWith(FileExtension, StringComparison.OrdinalIgnoreCase);

    /// <summary>All valid grids, by name; broken files are skipped.</summary>
    public IReadOnlyList<GridTemplate> LoadAll()
    {
        var list = new List<GridTemplate>();
        if (!System.IO.Directory.Exists(Directory)) return list;
        foreach (var file in System.IO.Directory.EnumerateFiles(Directory, "*" + FileExtension))
        {
            try
            {
                var t = GridTemplate.LoadFile(file);
                if (t.IsValid) list.Add(t);
            }
            catch (Exception e) when (e is JsonException or InvalidDataException or IOException or UnauthorizedAccessException) { /* skipped */ }
        }
        return list.OrderBy(t => t.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    public GridTemplate? Find(string name) =>
        LoadAll().FirstOrDefault(t => string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase));

    /// <summary>Saves (or replaces) a grid under its name; returns the file.</summary>
    public string Save(GridTemplate template)
    {
        System.IO.Directory.CreateDirectory(Directory);
        var path = PathFor(template.Name);
        SaveFile(template, path);
        return path;
    }

    public static void SaveFile(GridTemplate template, string path)
    {
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(template, SettingsJson.Options));
        File.Move(tmp, path, overwrite: true);
    }

    public bool Delete(string name)
    {
        var path = PathFor(name);
        if (!File.Exists(path)) return false;
        File.Delete(path);
        return true;
    }

    public string PathFor(string name) => Path.Combine(Directory, PresetStore.SafeFileName(name) + FileExtension);
}
