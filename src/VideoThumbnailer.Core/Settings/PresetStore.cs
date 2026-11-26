using System.Text.Json;

namespace VideoThumbnailer.Core.Settings;

/// <summary>Loads and saves presets (JSON files) from the user's preset folder plus the built-in ones.</summary>
public sealed class PresetStore
{
    public const string FileExtension = ".vtpreset.json";
    private readonly string _dir;

    public PresetStore(string? directory = null) => _dir = directory ?? AppPaths.PresetsDir;

    public string Directory => _dir;

    public IReadOnlyList<SheetSettings> LoadAll()
    {
        var result = new Dictionary<string, SheetSettings>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in BuiltInPresets.All) result[p.Name] = p;
        if (System.IO.Directory.Exists(_dir))
        {
            foreach (var file in System.IO.Directory.EnumerateFiles(_dir, "*" + FileExtension))
            {
                try
                {
                    var p = LoadFile(file);
                    result[p.Name] = p;
                }
                catch { /* broken preset files are skipped */ }
            }
        }
        return result.Values.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>Finds a preset by name or loads it from a file path.</summary>
    public SheetSettings? Find(string nameOrPath)
    {
        if (File.Exists(nameOrPath)) return LoadFile(nameOrPath);
        return LoadAll().FirstOrDefault(p => string.Equals(p.Name, nameOrPath, StringComparison.OrdinalIgnoreCase));
    }

    public static SheetSettings LoadFile(string path)
    {
        var json = File.ReadAllText(path);
        var p = JsonSerializer.Deserialize<SheetSettings>(json, SettingsJson.Options)
                ?? throw new InvalidDataException($"Preset file is empty: {path}");
        if (string.IsNullOrWhiteSpace(p.Name)) p.Name = Path.GetFileName(path).Replace(FileExtension, "");
        return p;
    }

    public string Save(SheetSettings preset)
    {
        System.IO.Directory.CreateDirectory(_dir);
        var path = Path.Combine(_dir, SafeFileName(preset.Name) + FileExtension);
        SaveFile(preset, path);
        return path;
    }

    public static void SaveFile(SheetSettings preset, string path)
    {
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(preset, SettingsJson.Options));
        File.Move(tmp, path, overwrite: true);
    }

    public bool Delete(string name)
    {
        var path = Path.Combine(_dir, SafeFileName(name) + FileExtension);
        if (!File.Exists(path)) return false;
        File.Delete(path);
        return true;
    }

    public static bool IsBuiltIn(string name) =>
        BuiltInPresets.All.Any(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));

    /// <summary>Suggested file name (with extension) for exporting a preset.</summary>
    public static string SafeFileNameForExport(string name) => SafeFileName(name) + FileExtension;

    internal static string SafeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars().Concat(['/', '\\', ':', '*', '?', '"', '<', '>', '|']).ToHashSet();
        var chars = name.Trim().Select(c => invalid.Contains(c) ? '_' : c).ToArray();
        var s = new string(chars);
        return string.IsNullOrWhiteSpace(s) ? "preset" : s;
    }
}
