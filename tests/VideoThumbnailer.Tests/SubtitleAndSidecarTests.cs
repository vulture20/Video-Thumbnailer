using VideoThumbnailer.Core.Batch;
using VideoThumbnailer.Core.Ffmpeg;
using VideoThumbnailer.Core.Layout;
using VideoThumbnailer.Core.Media;
using VideoThumbnailer.Core.Output;
using VideoThumbnailer.Core.Rendering;
using VideoThumbnailer.Core.Settings;

namespace VideoThumbnailer.Tests;

/// <summary>Subtitle info (#35) and the JSON sidecar (#38).</summary>
public class SubtitleAndSidecarTests
{
    private static FfmpegTools Tools()
    {
        var t = FfmpegLocator.FromPath();
        if (t is null) Assert.Skip("ffmpeg/ffprobe not on PATH");
        return t;
    }

    private static string MakeVideo(FfmpegTools t, string dir, string name, int seconds, params string[] extra)
    {
        var path = Path.Combine(dir, name);
        var args = new List<string> { "-v", "error", "-y", "-f", "lavfi", "-i", $"testsrc2=size=320x180:rate=25:duration={seconds}" };
        args.AddRange(extra);
        args.AddRange(["-pix_fmt", "yuv420p", path]);
        Assert.True(TestUtil.Run(t.FfmpegPath, args.ToArray()), "creating test video failed");
        return path;
    }

    [Theory]
    [InlineData("Film.srt", "srt", null, false, false)]
    [InlineData("Film.de.srt", "srt", "de", false, false)]
    [InlineData("film.EN.forced.ass", "ass", "en", true, false)]
    [InlineData("Film.eng.sdh.vtt", "vtt", "eng", false, true)]
    [InlineData("Film.pt-BR.hi.srt", "srt", "pt-br", false, true)]
    [InlineData("Film.idx", "idx", null, false, false)]
    public void Recognises_subtitle_file_names(string file, string codec, string? lang, bool forced, bool hi)
    {
        var s = ExternalSubtitles.Parse("Film", file);
        Assert.NotNull(s);
        Assert.Equal((codec, lang, forced, hi), (s.Codec, s.Language, s.Forced, s.HearingImpaired));
    }

    [Theory]
    [InlineData("Film.Teil2.srt")]
    [InlineData("Filmabend.srt")]
    [InlineData("Film.de.txt")]
    [InlineData("Other.srt")]
    public void Ignores_files_of_other_videos(string file) => Assert.Null(ExternalSubtitles.Parse("Film", file));

    [Fact]
    public void Finds_subtitle_files_next_to_the_video()
    {
        var dir = TestUtil.TempDir();
        var video = Path.Combine(dir, "Film.mkv");
        foreach (var f in new[] { "Film.mkv", "Film.de.srt", "Film.en.forced.ass", "Film.idx", "Film.sub", "Film.Teil2.srt", "Other.srt" })
            File.WriteAllText(Path.Combine(dir, f), "");
        var subs = ExternalSubtitles.Find(video);
        Assert.Equal(["SRT de", "ASS en", "VobSub "], subs.Select(s => s.FormatName + " " + s.Language).ToArray());
        Assert.All(subs, s => Assert.True(s.IsExternal));
        Assert.Empty(ExternalSubtitles.Find("https://example.com/video.mp4"));
    }

    [Fact]
    public void Subtitle_placeholders()
    {
        var info = TestUtil.SampleInfo().WithSubtitles(
        [
            new SubtitleStreamInfo { Index = 2, Codec = "subrip", Language = "ger" },
            new SubtitleStreamInfo { Index = 3, Codec = "hdmv_pgs_subtitle", Language = "eng", Forced = true },
            new SubtitleStreamInfo { Codec = "srt", Language = "eng", HearingImpaired = true, ExternalPath = "/videos/My Movie.eng.sdh.srt" },
        ]);
        Assert.Equal("3: ger SRT, eng PGS forced, eng SRT SDH external", InfoTemplate.Expand("{subtitles}", info, "en"));
        Assert.Equal("3: ger SRT, eng PGS erzwungen, eng SRT SDH extern", InfoTemplate.Expand("{subtitles}", info, "de"));
        Assert.Equal("ger, eng | 3", InfoTemplate.Expand("{slang} | {subtracks}", info, "en"));

        var none = TestUtil.SampleInfo();
        Assert.Equal("keine | keine | 0", InfoTemplate.Expand("{subtitles} | {slang} | {subtracks}", none, "de"));

        var values = InfoExport.Values(new InfoRow(info.Path, info));
        string F(string name) => values[InfoExport.Fields.ToList().IndexOf(name)];
        Assert.Equal(("3", "ger,eng", "ger:SRT;eng:PGS:forced;eng:SRT:sdh:external"), (F("subtracks"), F("slang"), F("subtitles")));
    }

    [Fact]
    public async Task Reads_subtitle_streams_and_files()
    {
        var t = Tools();
        var dir = TestUtil.TempDir();
        var srt = Path.Combine(dir, "in.srt");
        File.WriteAllText(srt, "1\n00:00:00,500 --> 00:00:02,000\nHallo\n");
        var video = MakeVideo(t, dir, "subs.mkv", 3, "-i", srt, "-map", "0:v", "-map", "1:s", "-c:s", "srt", "-metadata:s:s:0", "language=ger");
        File.Copy(srt, Path.Combine(dir, "subs.en.srt"));
        var info = await FfprobeReader.ReadAsync(t.FfprobePath, video, TestContext.Current.CancellationToken);
        Assert.Equal(2, info.SubtitleStreams.Count);
        Assert.Equal(("subrip", "ger", false), (info.SubtitleStreams[0].Codec, info.SubtitleStreams[0].Language, info.SubtitleStreams[0].IsExternal));
        Assert.Equal(("srt", "en", true), (info.SubtitleStreams[1].Codec, info.SubtitleStreams[1].Language, info.SubtitleStreams[1].IsExternal));
    }

    [Fact]
    public void Sidecar_paths_and_round_trip()
    {
        Assert.Equal("/v/a.vtsheet", SheetSidecar.PathFor("/v/a.jpg").Replace('\\', '/'));
        Assert.Equal("/v/a.vtsheet", SheetSidecar.PathFor("/v/a.sheet.mp4").Replace('\\', '/'));
        Assert.Equal("/v/a (2).vtsheet", SheetSidecar.PathFor("/v/a (2).png").Replace('\\', '/'));

        var dir = TestUtil.TempDir();
        var video = Path.Combine(dir, "videos", "clip.mp4");
        Directory.CreateDirectory(Path.GetDirectoryName(video)!);
        File.WriteAllText(video, "");
        var sheet = Path.Combine(dir, "sheets", "clip.jpg");
        var s = new SheetSettings { Columns = 2, Rows = 2, SheetWidth = 400 };
        var layout = GridLayout.Create(s, 16.0 / 9, 0, new Random(1));
        var car = SheetSidecar.Create(video, sheet, 60, (1920, 1080), layout, [5, 20, 35, 50], [false, true, false, true], null);
        var path = SheetSidecar.PathFor(sheet);
        car.Write(path);

        var back = SheetSidecar.Read(path);
        Assert.Equal("../videos/clip.mp4", back.Video.Path);
        Assert.Equal(Path.GetFullPath(video), back.ResolveVideo(path));
        Assert.Equal((400, layout.SheetHeight, 4), (back.Sheet.Width, back.Sheet.Height, back.Tiles.Count));
        Assert.Equal(layout.Tiles[1], new TileRect(back.Tiles[1].X, back.Tiles[1].Y, back.Tiles[1].Width, back.Tiles[1].Height));
        Assert.Equal([null, new ManualTime(20), null, new ManualTime(50)], back.ManualTimes());
        Assert.Equal(2, back.TileAt(layout.Tiles[2].X + 1, layout.Tiles[2].Y + 1)!.Index);
        Assert.DoesNotContain("\"manual\": false", File.ReadAllText(path));

        // moved away: the absolute path still finds the video; neither found: null
        var moved = Path.Combine(TestUtil.TempDir(), "clip.vtsheet");
        File.Copy(path, moved);
        Assert.Equal(Path.GetFullPath(video), SheetSidecar.Read(moved).ResolveVideo(moved));
        File.Delete(video);
        Assert.Null(SheetSidecar.Read(moved).ResolveVideo(moved));

        Assert.Throws<InvalidDataException>(() => SheetSidecar.Parse("{\"format\":\"other\"}"));
        Assert.Throws<InvalidDataException>(() => SheetSidecar.Parse("{\"format\":\"video-thumbnailer-sheet\",\"version\":99}"));
    }

    [Fact]
    public async Task Writes_sidecar_with_the_sheet()
    {
        var t = Tools();
        var dir = TestUtil.TempDir();
        var video = MakeVideo(t, dir, "side.mp4", 20);
        var s = new SheetSettings { Columns = 3, Rows = 2, SheetWidth = 600 };
        s.Output.Sidecar = true;
        var job = new ThumbnailJob(video) { ManualTimes = [null, new ManualTime(7.5)] };
        await new BatchProcessor(t).RunAsync([job], s, new BatchOptions(), TestContext.Current.CancellationToken);
        Assert.Equal(JobStatus.Done, job.Status);

        var car = SheetSidecar.Read(Path.Combine(dir, "side.vtsheet"));
        Assert.Equal(("side.mp4", "side.jpg", 6, false), (car.Video.Path, car.Sheet.File, car.Tiles.Count, car.Sheet.Animated));
        Assert.Equal(7.5, car.Tiles[1].Time);
        Assert.True(car.Tiles[1].Manual);
        Assert.Equal((320, 180), (car.Video.Width, car.Video.Height));
    }
}
