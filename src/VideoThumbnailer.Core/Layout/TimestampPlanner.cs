namespace VideoThumbnailer.Core.Layout;

public static class TimestampPlanner
{
    /// <summary>
    /// Evenly distributes <paramref name="count"/> capture points over the usable part of the video
    /// (after skipping the configured start/end percentages). Each point sits in the middle of its slot.
    /// </summary>
    public static double[] Plan(double duration, int count, double skipStartPercent, double skipEndPercent)
    {
        if (count <= 0) return [];
        if (duration <= 0) return Enumerable.Repeat(0d, count).ToArray();
        var start = duration * Math.Clamp(skipStartPercent, 0, 45) / 100.0;
        var end = duration * (1 - Math.Clamp(skipEndPercent, 0, 45) / 100.0);
        if (end <= start) { start = 0; end = duration; }
        var span = end - start;
        var result = new double[count];
        for (var i = 0; i < count; i++)
            result[i] = start + (i + 0.5) * span / count;
        return result;
    }

    /// <summary>
    /// Automatic plan with manual times on top: tile <c>i</c> (timeline order) takes <paramref name="manual"/>[i]
    /// when it is set, clamped into the video. Entries beyond the number of tiles are ignored.
    /// </summary>
    public static PlannedTime[] Plan(double duration, int count, double skipStartPercent, double skipEndPercent, IReadOnlyList<ManualTime?>? manual)
    {
        var auto = Plan(duration, count, skipStartPercent, skipEndPercent);
        var result = new PlannedTime[auto.Length];
        for (var i = 0; i < auto.Length; i++)
        {
            result[i] = manual is not null && i < manual.Count && manual[i] is { } m
                ? new PlannedTime(duration > 0 ? Math.Clamp(m.Time, 0, duration) : Math.Max(0, m.Time), true, m.ClipSeconds)
                : new PlannedTime(auto[i]);
        }
        return result;
    }
}
