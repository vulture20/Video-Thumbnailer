using VideoThumbnailer.Core.Media;
using VideoThumbnailer.Core.Rendering;

namespace VideoThumbnailer.Tests;

public class MediaTests
{
    private const string ProbeJson = """
    {
      "streams": [
        { "index": 0, "codec_type": "video", "codec_name": "mjpeg", "width": 300, "height": 300, "disposition": { "attached_pic": 1 } },
        { "index": 1, "codec_type": "video", "codec_name": "h264", "profile": "High", "width": 1920, "height": 1080,
          "sample_aspect_ratio": "1:1", "avg_frame_rate": "30000/1001", "pix_fmt": "yuv420p", "bit_rate": "4000000",
          "side_data_list": [ { "side_data_type": "Display Matrix", "rotation": -90 } ] },
        { "index": 2, "codec_type": "audio", "codec_name": "aac", "sample_rate": "48000", "channels": 2, "channel_layout": "stereo",
          "bit_rate": "128000", "tags": { "language": "eng" } }
      ],
      "format": { "format_name": "mov,mp4,m4a,3gp,3g2,mj2", "duration": "125.250000", "size": "65000000", "bit_rate": "4150000",
                  "tags": { "title": "Holiday" } }
    }
    """;

    [Fact]
    public void Parses_ffprobe_json_and_skips_cover_art()
    {
        var info = FfprobeReader.Parse(ProbeJson, "/x/clip.mp4");
        Assert.NotNull(info.Video);
        Assert.Equal(1, info.Video!.Index);
        Assert.Equal("h264", info.Video.Codec);
        Assert.Equal(29.97, info.Video.FrameRate, 2);
        Assert.Equal(-90, info.Video.Rotation);
        Assert.Equal((1080, 1920), info.Video.DisplaySize);
        Assert.Equal(125.25, info.Duration, 3);
        Assert.Equal(65000000, info.FileSize);
        Assert.Equal("Holiday", info.Title);
        Assert.Equal("eng", info.Audio!.Language);
    }

    [Fact]
    public void Anamorphic_video_uses_sample_aspect()
    {
        var v = new VideoStreamInfo { Width = 720, Height = 576, SampleAspect = 64.0 / 45 };
        Assert.Equal((1024, 576), v.DisplaySize);
    }

    [Fact]
    public void Ratio_parsing()
    {
        Assert.Equal(25, FfprobeReader.Ratio("25/1"));
        Assert.Equal(16.0 / 9, FfprobeReader.Ratio("16:9"));
        Assert.Null(FfprobeReader.Ratio("0/0"));
    }

    [Fact]
    public void Info_template_expands_placeholders()
    {
        var info = TestUtil.SampleInfo();
        var text = InfoTemplate.Expand("{filename}|{size:GB}|{duration}|{resolution}|{fps}|{vcodec}{hdr}|{audio}|{unknown}", info, "en");
        Assert.Equal("My Movie.mkv|1.50 GB|01:23:45|1920×1080|23.976|hevc · HDR|eac3 · 48000 Hz · 5.1(side) · 640 kbit/s · ger|{unknown}", text);
    }

    [Fact]
    public void Info_template_uses_german_number_format()
    {
        var info = TestUtil.SampleInfo();
        Assert.Equal("1,50 GB|23,976", InfoTemplate.Expand("{size:GB}|{fps}", info, "de"));
    }

    [Fact]
    public void Info_template_without_audio()
    {
        var info = new MediaInfo { Path = "a.mp4", Duration = 1, Video = new VideoStreamInfo { Codec = "vp9", Width = 2, Height = 2 } };
        Assert.Equal("none / n/a", InfoTemplate.Expand("{audio} / {acodec}", info, "en"));
        Assert.Equal("keine / k. A.", InfoTemplate.Expand("{audio} / {acodec}", info, "de"));
    }
}
