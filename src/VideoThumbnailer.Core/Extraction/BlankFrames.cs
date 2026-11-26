using SkiaSharp;

namespace VideoThumbnailer.Core.Extraction;

/// <summary>
/// Detects (nearly) black or single-colored frames (fades, cuts, title cards without text) and plans where to
/// look for a better frame nearby.
/// </summary>
public static class BlankFrames
{
    /// <summary>Below this standard deviation of the brightness (0–255) a frame counts as one color, whatever the color.</summary>
    public const double UniformStdDev = 6;
    /// <summary>A frame darker than this on average (0–255) …</summary>
    public const double DarkMean = 24;
    /// <summary>… and with less contrast than this counts as black.</summary>
    public const double DarkStdDev = 12;
    /// <summary>Distance between two tries when searching nearby.</summary>
    public const double SearchStep = 0.5;

    /// <summary>Mean and standard deviation of the brightness (Rec. 709 luma, 0–255) on a grid of at most 64 × 64 samples.</summary>
    public static (double Mean, double StdDev) Measure(SKBitmap bmp)
    {
        if (bmp.Width <= 0 || bmp.Height <= 0) return (0, 0);
        var nx = Math.Min(64, bmp.Width);
        var ny = Math.Min(64, bmp.Height);
        double sum = 0, sq = 0;
        for (var y = 0; y < ny; y++)
        {
            var py = (int)((y + 0.5) * bmp.Height / ny);
            for (var x = 0; x < nx; x++)
            {
                var c = bmp.GetPixel((int)((x + 0.5) * bmp.Width / nx), py);
                var l = 0.2126 * c.Red + 0.7152 * c.Green + 0.0722 * c.Blue;
                sum += l;
                sq += l * l;
            }
        }
        var n = (double)nx * ny;
        var mean = sum / n;
        return (mean, Math.Sqrt(Math.Max(0, sq / n - mean * mean)));
    }

    public static bool IsBlank(SKBitmap bmp)
    {
        var (mean, sd) = Measure(bmp);
        return sd < UniformStdDev || mean < DarkMean && sd < DarkStdDev;
    }

    public static bool IsBlank(byte[] encoded)
    {
        using var bmp = SKBitmap.Decode(encoded);
        return bmp is not null && IsBlank(bmp);
    }

    /// <summary>
    /// Times to try instead of <paramref name="time"/>, nearest first, alternating after/before (+0.5, −0.5, +1, −1 …)
    /// up to ±<paramref name="window"/> seconds, inside the video.
    /// </summary>
    public static IEnumerable<double> Candidates(double time, double window, double duration)
    {
        var steps = (int)Math.Floor(Math.Max(0, window) / SearchStep + 1e-9);
        var end = duration > 0 ? duration - 0.05 : double.MaxValue;
        for (var k = 1; k <= steps; k++)
        {
            var d = k * SearchStep;
            if (time + d <= end) yield return time + d;
            if (time - d >= 0) yield return time - d;
        }
    }
}
