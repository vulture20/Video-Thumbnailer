using System.Text.RegularExpressions;

namespace VideoThumbnailer.Core.Media;

/// <summary>
/// Finds subtitle files next to a video (#35): <c>Film.srt</c>, <c>Film.de.srt</c>, <c>Film.en.forced.ass</c>,
/// <c>Film.eng.sdh.vtt</c>, <c>Film.idx</c> (+ <c>.sub</c>), <c>Film.sup</c>. Between the name of the video and the
/// extension only a language code and the words <c>forced</c>, <c>sdh</c>, <c>hi</c>, <c>cc</c> are allowed, so
/// <c>Film.Teil2.srt</c> does not count for <c>Film.mkv</c>.
/// </summary>
public static partial class ExternalSubtitles
{
    public static IReadOnlyList<string> Extensions { get; } = [".srt", ".ass", ".ssa", ".vtt", ".sup", ".idx", ".sub"];

    [GeneratedRegex(@"^[a-z]{2,3}([-_][a-z]{2,4})?$", RegexOptions.IgnoreCase)]
    private static partial Regex LanguageCode();

    public static IReadOnlyList<SubtitleStreamInfo> Find(string videoPath)
    {
        if (Input.VideoSource.IsUrl(videoPath)) return [];
        try
        {
            var dir = Path.GetDirectoryName(Path.GetFullPath(videoPath));
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return [];
            var stem = Path.GetFileNameWithoutExtension(videoPath);
            var files = Directory.EnumerateFiles(dir, "*", new EnumerationOptions { RecurseSubdirectories = false, IgnoreInaccessible = true })
                .Where(f => Extensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
                .ToList();
            var result = new List<(string Name, SubtitleStreamInfo Info)>();
            foreach (var f in files)
            {
                var name = Path.GetFileName(f);
                if (Parse(stem, name) is not { } info) continue;
                var ext = Path.GetExtension(f).ToLowerInvariant();
                if (ext == ".sub")
                {
                    // VobSub: .idx and .sub belong together and count once (as .idx); a .sub alone is MicroDVD text
                    if (files.Any(o => string.Equals(o, Path.ChangeExtension(f, ".idx"), StringComparison.OrdinalIgnoreCase))) continue;
                    info = new SubtitleStreamInfo { Codec = "microdvd", Language = info.Language, Forced = info.Forced, HearingImpaired = info.HearingImpaired, ExternalPath = f };
                }
                else info = new SubtitleStreamInfo { Codec = ext.TrimStart('.'), Language = info.Language, Forced = info.Forced, HearingImpaired = info.HearingImpaired, ExternalPath = f };
                result.Add((name, info));
            }
            return result.OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase).Select(r => r.Info).ToList();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return [];
        }
    }

    /// <summary>Language and flags from a file name like <c>Film.de.forced.srt</c>; null when it does not belong to <paramref name="videoStem"/>.</summary>
    internal static SubtitleStreamInfo? Parse(string videoStem, string fileName)
    {
        var ext = Path.GetExtension(fileName);
        if (!Extensions.Contains(ext, StringComparer.OrdinalIgnoreCase)) return null;
        var stem = fileName[..^ext.Length];
        if (string.Equals(stem, videoStem, StringComparison.OrdinalIgnoreCase)) return new SubtitleStreamInfo { Codec = ext.TrimStart('.').ToLowerInvariant() };
        if (!stem.StartsWith(videoStem + ".", StringComparison.OrdinalIgnoreCase)) return null;

        string? lang = null;
        bool forced = false, hi = false;
        foreach (var token in stem[(videoStem.Length + 1)..].Split('.'))
        {
            var t = token.ToLowerInvariant();
            if (t == "forced") forced = true;
            else if (t is "sdh" or "cc" || (t == "hi" && lang is not null)) hi = true;
            else if (lang is null && LanguageCode().IsMatch(t)) lang = t.Replace('_', '-');
            else return null; // some other name, e.g. "Film.Teil2.srt" or a subtitle of "Film.x.mkv"
        }
        return new SubtitleStreamInfo { Codec = ext.TrimStart('.').ToLowerInvariant(), Language = lang, Forced = forced, HearingImpaired = hi };
    }
}
