using System.Globalization;
using System.Text;
using System.Xml;
using VideoThumbnailer.Core.Input;
using VideoThumbnailer.Core.Media;

namespace VideoThumbnailer.Core.Output;

/// <summary>Technical data of one video for the export; <see cref="Info"/> is null when ffprobe failed (<see cref="Error"/>).</summary>
public sealed record InfoRow(string Path, MediaInfo? Info, string? Error = null);

/// <summary>
/// Writes the technical data of many videos (the values behind the info header) into one CSV or XML file (#32).
/// Field names are the placeholders of the info header; numbers are plain values (bytes, seconds, bit/s) in
/// invariant culture, so spreadsheets and scripts read them in every language.
/// </summary>
public static class InfoExport
{
    public static IReadOnlyList<string> Fields { get; } =
    [
        "filename", "name", "ext", "folder", "path", "title", "container",
        "size", "duration", "bitrate",
        "width", "height", "resolution", "vcodec", "vprofile", "fps", "pixfmt", "hdr", "vbitrate", "rotation",
        "acodec", "samplerate", "channels", "channellayout", "abitrate", "alang", "audiotracks",
        "subtracks", "slang", "subtitles",
        "error",
    ];

    public enum Format { Csv, Xml }

    /// <summary>XML for ".xml", otherwise CSV.</summary>
    public static Format FormatFor(string file) =>
        string.Equals(System.IO.Path.GetExtension(file), ".xml", StringComparison.OrdinalIgnoreCase) ? Format.Xml : Format.Csv;

    /// <summary>Values of one video in the order of <see cref="Fields"/>; unknown values are empty.</summary>
    public static IReadOnlyList<string> Values(InfoRow row)
    {
        var inv = CultureInfo.InvariantCulture;
        var i = row.Info;
        var v = i?.Video;
        var a = i?.Audio;
        string N(double? d, string fmt = "0.###") => d is { } x && x > 0 ? x.ToString(fmt, inv) : "";
        string L(long? l) => l is > 0 ? l.Value.ToString(inv) : "";
        var p = row.Path;
        return
        [
            VideoSource.FileName(p), VideoSource.NameWithoutExtension(p), VideoSource.Extension(p), VideoSource.Folder(p),
            VideoSource.DisplayPath(p), i?.Title ?? "", i?.ContainerFormat ?? "",
            i is null ? "" : i.FileSize.ToString(inv), N(i?.Duration), L(i?.BitRate),
            v is null ? "" : v.DisplaySize.Width.ToString(inv), v is null ? "" : v.DisplaySize.Height.ToString(inv),
            v is null ? "" : $"{v.DisplaySize.Width}x{v.DisplaySize.Height}",
            v?.Codec ?? "", v?.Profile ?? "", N(v?.FrameRate), v?.PixelFormat ?? "", v is null ? "" : v.IsHdr ? "true" : "false",
            L(v?.BitRate), v is null ? "" : v.Rotation.ToString(inv),
            a?.Codec ?? "", a is { SampleRate: > 0 } ? a.SampleRate.ToString(inv) : "", a is { Channels: > 0 } ? a.Channels.ToString(inv) : "",
            a?.ChannelLayout ?? "", L(a?.BitRate), a?.Language ?? "", i is null ? "" : i.AudioStreams.Count.ToString(inv),
            i is null ? "" : i.SubtitleStreams.Count.ToString(inv),
            i is null ? "" : string.Join(",", i.SubtitleStreams.Select(t => t.Language).Where(l => !string.IsNullOrEmpty(l) && l != "und").Distinct(StringComparer.OrdinalIgnoreCase)),
            i is null ? "" : string.Join(";", i.SubtitleStreams.Select(SubtitleValue)),
            row.Error ?? "",
        ];
    }

    /// <summary>One subtitle track as "lang:FORMAT" plus ":forced", ":sdh", ":external" (language independent).</summary>
    private static string SubtitleValue(SubtitleStreamInfo t)
    {
        var s = (string.IsNullOrEmpty(t.Language) ? "und" : t.Language) + ":" + t.FormatName;
        if (t.Forced) s += ":forced";
        if (t.HearingImpaired) s += ":sdh";
        if (t.IsExternal) s += ":external";
        return s;
    }

    public static void Write(string file, IEnumerable<InfoRow> rows)
    {
        var dir = System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(file));
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        var tmp = file + ".part";
        using (var w = new StreamWriter(tmp, false, new UTF8Encoding(true)))
        {
            if (FormatFor(file) == Format.Xml) WriteXml(w, rows); else WriteCsv(w, rows);
        }
        File.Move(tmp, file, overwrite: true);
    }

    /// <summary>RFC 4180: comma separated, CRLF, fields with comma, quote or line break in quotes. UTF-8 with BOM so Excel reads umlauts.</summary>
    public static void WriteCsv(TextWriter w, IEnumerable<InfoRow> rows)
    {
        w.Write(string.Join(",", Fields) + "\r\n");
        foreach (var r in rows) w.Write(string.Join(",", Values(r).Select(Quote)) + "\r\n");

        static string Quote(string s) =>
            s.IndexOfAny([',', '"', '\r', '\n']) >= 0 ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;
    }

    /// <summary>&lt;videos&gt; with one &lt;video&gt; per file and one element per field (empty fields left out).</summary>
    public static void WriteXml(TextWriter w, IEnumerable<InfoRow> rows)
    {
        using var x = XmlWriter.Create(w, new XmlWriterSettings { Indent = true, Encoding = new UTF8Encoding(true) });
        x.WriteStartDocument();
        x.WriteStartElement("videos");
        foreach (var r in rows)
        {
            x.WriteStartElement("video");
            var values = Values(r);
            for (var k = 0; k < Fields.Count; k++)
                if (values[k].Length > 0) x.WriteElementString(Fields[k], values[k]);
            x.WriteEndElement();
        }
        x.WriteEndElement();
        x.WriteEndDocument();
    }

    /// <summary>Reads the data of all videos with ffprobe (at most <paramref name="parallel"/> at once), in the given order.</summary>
    public static async Task<IReadOnlyList<InfoRow>> ProbeAllAsync(string ffprobePath, IReadOnlyList<string> paths, int parallel, CancellationToken ct)
    {
        var rows = new InfoRow[paths.Count];
        await Parallel.ForEachAsync(Enumerable.Range(0, paths.Count),
            new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, parallel), CancellationToken = ct },
            async (k, token) =>
            {
                try { rows[k] = new InfoRow(paths[k], await FfprobeReader.ReadAsync(ffprobePath, paths[k], token).ConfigureAwait(false)); }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex) { rows[k] = new InfoRow(paths[k], null, ex.Message); }
            }).ConfigureAwait(false);
        return rows;
    }
}
