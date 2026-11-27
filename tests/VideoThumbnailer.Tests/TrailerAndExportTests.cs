using System.Xml.Linq;
using SkiaSharp;
using VideoThumbnailer.Core.Batch;
using VideoThumbnailer.Core.Ffmpeg;
using VideoThumbnailer.Core.Input;
using VideoThumbnailer.Core.Output;
using VideoThumbnailer.Core.Rendering;
using VideoThumbnailer.Core.Settings;

namespace VideoThumbnailer.Tests;

/// <summary>Preview clip (#31), info export and WebVTT (#32).</summary>
public class TrailerAndExportTests
{
    private static FfmpegTools Tools()
    {
        var t = FfmpegLocator.FromPath();
        if (t is null) Assert.Skip("ffmpeg/ffprobe not on PATH");
        return t;
    }

    private static string MakeVideo(FfmpegTools t, string dir, string name, int seconds, bool audio = false)
    {
        var path = Path.Combine(dir, name);
        var args = new List<string> { "-v", "error", "-y", "-f", "lavfi", "-i", $"testsrc2=size=320x180:rate=25:duration={seconds}" };
        if (audio) args.AddRange(["-f", "lavfi", "-i", $"sine=frequency=440:duration={seconds}", "-c:a", "aac", "-shortest"]);
        args.AddRange(["-pix_fmt", "yuv420p", path]);
        Assert.True(TestUtil.Run(t.FfmpegPath, args.ToArray()), "creating test video failed");
        return path;
    }

    private static async Task<double> DurationAsync(FfmpegTools t, string file) =>
        (await Core.Media.FfprobeReader.ReadAsync(t.FfprobePath, file, TestContext.Current.CancellationToken)).Duration;

    [Fact]
    public void Trailer_settings_stay_within_two_minutes()
    {
        var tr = new TrailerSettings { Clips = 40, ClipSeconds = 10, CrossfadeSeconds = 9 };
        tr.Normalize();
        Assert.True(tr.Clips * tr.ClipSeconds <= TrailerSettings.MaxSeconds);
        Assert.Equal(3, tr.ClipSeconds);
        Assert.True(tr.CrossfadeSeconds <= tr.ClipSeconds / 2);
    }

    [Fact]
    public void Trailer_frame_size_keeps_the_longer_side_and_is_even()
    {
        Assert.Equal((640, 360), TrailerGenerator.FrameSize(16.0 / 9, 640));
        Assert.Equal((360, 640), TrailerGenerator.FrameSize(9.0 / 16, 640));
        var (w, h) = TrailerGenerator.FrameSize(2.39, 641);
        Assert.True(w % 2 == 0 && h % 2 == 0);
    }

    [Fact]
    public void Trailer_sequence_blends_parts()
    {
        // Two parts of 10 frames with a fade of 3: 17 frames, the 3 blended ones show both parts.
        var seq = TrailerGenerator.Sequence([10, 10], [3]);
        Assert.Equal(17, seq.Count);
        Assert.Equal((0, 7, 0), (seq[7].Part, seq[7].Frame, seq[7].NextFrame));
        Assert.Equal((1, 3, -1), (seq[10].Part, seq[10].Frame, seq[10].NextFrame));
        Assert.All(seq.Where(x => x.NextFrame >= 0), x => Assert.InRange(x.Mix, 0.01f, 0.99f));
        // Hard cut.
        Assert.Equal(20, TrailerGenerator.Sequence([10, 10], [0]).Count);
        Assert.Equal(2, TrailerGenerator.FadeFrames(1, 25, 10, 4)); // never more than half a part
    }

    [Fact]
    public void Audio_of_several_parts_is_joined_with_crossfades()
    {
        var hard = AnimationEncoder.AudioJoinFilter(3, null);
        Assert.Contains("[s1][s2][s3]concat=n=3:v=0:a=1[aout]", hard);
        var soft = AnimationEncoder.AudioJoinFilter(3, [0.5, 0.25]);
        Assert.Contains("[s1][s2]acrossfade=d=0.5:c1=tri:c2=tri[x2]", soft);
        Assert.Contains("[x2][s3]acrossfade=d=0.25:c1=tri:c2=tri[aout]", soft);
    }

    [Fact]
    public void Preview_clips_are_skipped_in_folder_scans()
    {
        Assert.True(VideoScanner.IsAnimatedSheet("/v/movie.preview.mp4"));
        Assert.True(VideoScanner.IsAnimatedSheet("/v/movie.preview (2).webm"));
        Assert.False(VideoScanner.IsAnimatedSheet("/v/preview.mp4"));
        var s = new SheetSettings();
        s.Trailer.Format = AnimationFormat.Gif;
        Assert.EndsWith(Path.Combine("v", "movie.preview.gif"), OutputPathResolver.ResolveTrailerPath(s, "/v/movie.mkv", null));
    }

    [Fact]
    public async Task Writes_preview_clip_next_to_the_sheet()
    {
        var t = Tools();
        var dir = TestUtil.TempDir();
        var video = MakeVideo(t, dir, "movie.mp4", 30, audio: true);
        var s = new SheetSettings { Columns = 2, Rows = 2, SheetWidth = 400 };
        s.Trailer.Enabled = true;
        s.Trailer.Clips = 4;
        s.Trailer.ClipSeconds = 2;
        s.Trailer.CrossfadeSeconds = 0.4;
        s.Trailer.FramesPerSecond = 10;
        s.Trailer.Size = 320;
        s.Trailer.Audio = true;

        var job = new ThumbnailJob(video);
        await new BatchProcessor(t).RunAsync([job], s, new BatchOptions(), TestContext.Current.CancellationToken);
        Assert.Equal(JobStatus.Done, job.Status);
        Assert.EndsWith("movie.jpg", job.OutputPath);
        var clip = Path.Combine(dir, "movie.preview.mp4");
        Assert.True(File.Exists(clip), job.Message);
        Assert.Contains("movie.preview.mp4", job.Message);
        var info = await Core.Media.FfprobeReader.ReadAsync(t.FfprobePath, clip, TestContext.Current.CancellationToken);
        Assert.Equal((320, 180), (info.Video!.Width, info.Video.Height));
        Assert.NotNull(info.Audio);
        // 4 parts × 2 s minus 3 fades of 0.4 s = 6.8 s
        Assert.InRange(info.Duration, 6.4, 7.3);

        // The folder scan finds the video, not the clip.
        Assert.Single(VideoScanner.Scan([dir], recursive: false));
    }

    [Fact]
    public async Task Only_the_preview_clip_as_gif()
    {
        var t = Tools();
        var dir = TestUtil.TempDir();
        var video = MakeVideo(t, dir, "short.mp4", 6);
        var s = new SheetSettings();
        s.Trailer.Enabled = true;
        s.Trailer.Only = true;
        s.Trailer.Format = AnimationFormat.Gif;
        s.Trailer.Clips = 5;          // only 3 parts of 2 s fit into 6 s
        s.Trailer.ClipSeconds = 2;
        s.Trailer.CrossfadeSeconds = 0;
        s.Trailer.FramesPerSecond = 5;
        s.Trailer.Size = 160;

        var job = new ThumbnailJob(video);
        await new BatchProcessor(t).RunAsync([job], s, new BatchOptions(), TestContext.Current.CancellationToken);
        Assert.Equal(JobStatus.Done, job.Status);
        Assert.EndsWith("short.preview.gif", job.OutputPath);
        Assert.False(File.Exists(Path.Combine(dir, "short.jpg")));
        using var codec = SKCodec.Create(job.OutputPath);
        Assert.Equal(160, codec.Info.Width);
        Assert.InRange(codec.FrameCount, 25, 31); // 3 × 2 s × 5 fps
    }

    [Fact]
    public void Vtt_cues_cover_the_whole_video()
    {
        var ranges = WebVttWriter.CueRanges([10, 30, 50], 60);
        Assert.Equal([(0d, 20d), (20d, 40d), (40d, 60d)], ranges);
        var vtt = WebVttWriter.BuildVtt("my movie.thumbnails.jpg", [10, 30, 50], 60, 2, 160, 90);
        Assert.StartsWith("WEBVTT\n", vtt);
        Assert.Contains("00:00:20.000 --> 00:00:40.000\nmy%20movie.thumbnails.jpg#xywh=160,0,160,90", vtt);
        Assert.Contains("#xywh=0,90,160,90", vtt);
        Assert.Equal(("/v/a.thumbnails.vtt", "/v/a.thumbnails.jpg"),
            WebVttWriter.PathsFor("/v/a.sheet.mp4") is var (v, sp) ? (v.Replace('\\', '/'), sp.Replace('\\', '/')) : default);
    }

    [Fact]
    public async Task Writes_webvtt_with_sprite()
    {
        var t = Tools();
        var dir = TestUtil.TempDir();
        var video = MakeVideo(t, dir, "web.mp4", 20);
        var s = new SheetSettings { Columns = 3, Rows = 3, SheetWidth = 600 };
        s.Output.WebVtt = true;
        s.Output.WebVttTileWidth = 100;

        var job = new ThumbnailJob(video);
        await new BatchProcessor(t).RunAsync([job], s, new BatchOptions(), TestContext.Current.CancellationToken);
        Assert.Equal(JobStatus.Done, job.Status);
        var vtt = File.ReadAllText(Path.Combine(dir, "web.thumbnails.vtt"));
        Assert.Equal(9, vtt.Split("#xywh=").Length - 1);
        using var sprite = SKBitmap.Decode(Path.Combine(dir, "web.thumbnails.jpg"));
        Assert.Equal((300, 3 * 56), (sprite.Width, sprite.Height));
    }

    [Fact]
    public void Exports_info_as_csv_and_xml()
    {
        var info = TestUtil.SampleInfo("/videos/My, \"Movie\".mkv");
        InfoRow[] rows = [new(info.Path, info), new("/videos/broken.mp4", null, "ffprobe failed")];
        var csv = new StringWriter();
        InfoExport.WriteCsv(csv, rows);
        var lines = csv.ToString().Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(3, lines.Length);
        Assert.StartsWith("filename,name,ext,folder,path,", lines[0]);
        Assert.StartsWith("\"My, \"\"Movie\"\".mkv\",", lines[1]);
        Assert.Contains(",1920,1080,1920x1080,", lines[1]);
        Assert.EndsWith(",ffprobe failed", lines[2]);

        var file = Path.Combine(TestUtil.TempDir(), "infos.xml");
        InfoExport.Write(file, rows);
        var doc = XDocument.Load(file);
        var videos = doc.Root!.Elements("video").ToList();
        Assert.Equal(2, videos.Count);
        Assert.Equal("1920", videos[0].Element("width")!.Value);
        Assert.Equal("ffprobe failed", videos[1].Element("error")!.Value);
        Assert.Null(videos[1].Element("width"));
    }
}
