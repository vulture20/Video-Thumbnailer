using System.Globalization;

namespace VideoThumbnailer.Core.Util;

public static class Formatting
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>Formats seconds as h:mm:ss (or mm:ss for &lt; 1h when <paramref name="compact"/> is set).</summary>
    public static string Duration(double seconds, bool compact = false, bool millis = false)
    {
        if (double.IsNaN(seconds) || seconds < 0) seconds = 0;
        var ts = TimeSpan.FromSeconds(seconds);
        var h = (int)ts.TotalHours;
        var core = compact && h == 0
            ? $"{ts.Minutes:00}:{ts.Seconds:00}"
            : $"{h:00}:{ts.Minutes:00}:{ts.Seconds:00}";
        return millis ? core + "." + ts.Milliseconds.ToString("000", Inv) : core;
    }

    public static string Size(long bytes, string? unit = null)
    {
        double b = bytes;
        string U(double v, string u) => v.ToString(v >= 100 ? "0" : "0.00", Inv) + " " + u;
        switch (unit?.ToUpperInvariant())
        {
            case "B": case "BYTES": return bytes.ToString("N0", Inv) + " B";
            case "KB": return U(b / 1024, "KB");
            case "MB": return U(b / (1024 * 1024), "MB");
            case "GB": return U(b / (1024d * 1024 * 1024), "GB");
            case "TB": return U(b / (1024d * 1024 * 1024 * 1024), "TB");
        }
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        var i = 0;
        while (b >= 1024 && i < units.Length - 1) { b /= 1024; i++; }
        return i == 0 ? bytes + " B" : U(b, units[i]);
    }

    public static string Bitrate(long? bitsPerSecond)
    {
        if (bitsPerSecond is null or <= 0) return "n/a";
        var v = bitsPerSecond.Value;
        return v >= 1_000_000
            ? (v / 1_000_000d).ToString("0.00", Inv) + " Mbit/s"
            : (v / 1000d).ToString("0", Inv) + " kbit/s";
    }

    public static string Number(double v, string format = "0.###") => v.ToString(format, Inv);
}
