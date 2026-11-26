using System.Globalization;

namespace VideoThumbnailer.Core.Layout;

/// <summary>
/// A capture time chosen by hand for one tile, optionally with its own clip length (animated sheets).
/// Manual times belong to one video (queue entry or <c>--times</c>), never to a preset.
/// </summary>
public sealed record ManualTime(double Time, double? ClipSeconds = null)
{
    /// <summary>
    /// Parses a list like <c>"1:00, 5:30+2, -, 90.5"</c>: one entry per tile in timeline order, separated by commas.
    /// Each entry is seconds, mm:ss or hh:mm:ss (decimals allowed), optionally <c>+seconds</c> for the clip length.
    /// An empty entry, "-" or "auto" keeps the automatic time of that tile.
    /// </summary>
    public static ManualTime?[] ParseList(string text)
    {
        var parts = text.Split(',', ';');
        var list = new ManualTime?[parts.Length];
        for (var i = 0; i < parts.Length; i++)
        {
            var p = parts[i].Trim();
            if (p.Length == 0 || p == "-" || p.Equals("auto", StringComparison.OrdinalIgnoreCase)) continue;
            list[i] = Parse(p) ?? throw new FormatException(p);
        }
        // Trailing automatic entries carry no information.
        var last = Array.FindLastIndex(list, m => m is not null);
        return list[..(last + 1)];
    }

    /// <summary>One entry ("5:30" or "5:30+2"), null if it is not valid.</summary>
    public static ManualTime? Parse(string text)
    {
        var p = text.Trim();
        double? clip = null;
        var plus = p.IndexOf('+');
        if (plus >= 0)
        {
            if (!double.TryParse(p[(plus + 1)..].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var c) || c <= 0) return null;
            clip = c;
            p = p[..plus].Trim();
        }
        return TryParseTime(p, out var t) ? new ManualTime(t, clip) : null;
    }

    /// <summary>Seconds ("90.5"), mm:ss or hh:mm:ss with optional fraction; a comma also works as decimal separator.</summary>
    public static bool TryParseTime(string text, out double seconds)
    {
        seconds = 0;
        var parts = text.Trim().Replace(',', '.').Split(':');
        if (parts.Length is < 1 or > 3) return false;
        double total = 0;
        for (var i = 0; i < parts.Length; i++)
        {
            var last = i == parts.Length - 1;
            if (!double.TryParse(parts[i], last ? NumberStyles.Float : NumberStyles.None, CultureInfo.InvariantCulture, out var v) || v < 0)
                return false;
            if (i > 0 && v >= 60) return false; // minutes/seconds after the first field
            total = total * 60 + v;
        }
        if (double.IsNaN(total) || double.IsInfinity(total)) return false;
        seconds = total;
        return true;
    }

    /// <summary>"hh:mm:ss.fff" (milliseconds only when not zero), "+clip" for a clip length.</summary>
    public override string ToString()
    {
        var s = FormatTime(Time);
        return ClipSeconds is { } c ? s + "+" + c.ToString("0.###", CultureInfo.InvariantCulture) : s;
    }

    public static string FormatTime(double seconds)
    {
        var ts = TimeSpan.FromMilliseconds(Math.Round(Math.Max(0, seconds) * 1000));
        var core = $"{(int)ts.TotalHours:00}:{ts.Minutes:00}:{ts.Seconds:00}";
        return ts.Milliseconds == 0 ? core : core + "." + ts.Milliseconds.ToString("000", CultureInfo.InvariantCulture);
    }

    /// <summary>List form for <c>--times</c>, "-" for automatic tiles.</summary>
    public static string FormatList(IEnumerable<ManualTime?> times) => string.Join(",", times.Select(t => t?.ToString() ?? "-"));
}

/// <summary>Capture time of one tile: automatic or chosen by hand (then with an optional clip length).</summary>
public readonly record struct PlannedTime(double Time, bool Manual = false, double? ClipSeconds = null);
