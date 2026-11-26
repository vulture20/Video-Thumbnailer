using System.Globalization;

namespace VideoThumbnailer.Core.Layout;

/// <summary>Usable area of a screen in physical pixels (without task bar or dock).</summary>
public readonly record struct ScreenSize(int Width, int Height)
{
    public override string ToString() => $"{Width}x{Height}";

    /// <summary>Parses "1920x1080" (also "1920×1080" or "1920,1080").</summary>
    public static bool TryParse(string? text, out ScreenSize size)
    {
        size = default;
        if (string.IsNullOrWhiteSpace(text)) return false;
        var parts = text.Trim().Split(['x', 'X', '×', ','], StringSplitOptions.TrimEntries);
        if (parts.Length != 2
            || !int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var w)
            || !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var h)
            || w < 100 || h < 100 || w > 32000 || h > 32000)
            return false;
        size = new ScreenSize(w, h);
        return true;
    }
}

/// <summary>
/// Screen that "fit to screen" sheets are sized for. The GUI sets the screen its window is on, the command line
/// the value of --screen (or the one the GUI saw last). Null = unknown: the fixed width of the settings applies.
/// </summary>
public static class ScreenFitting
{
    private static readonly Lock Gate = new();
    private static ScreenSize? _current;

    public static ScreenSize? Current
    {
        get { lock (Gate) return _current; }
        set { lock (Gate) _current = value; }
    }
}
