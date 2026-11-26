using System.Diagnostics;
using VideoThumbnailer.Core.Media;

namespace VideoThumbnailer.Tests;

internal static class TestUtil
{
    public static string TempDir()
    {
        var d = Path.Combine(Path.GetTempPath(), "vthumb-tests", Guid.NewGuid().ToString("N")[..10]);
        Directory.CreateDirectory(d);
        return d;
    }

    public static MediaInfo SampleInfo(string path = "/videos/My Movie.mkv", int w = 1920, int h = 1080, int rotation = 0) => new()
    {
        Path = path,
        FileSize = 1_610_612_736, // 1.5 GiB
        Duration = 5025.5,
        BitRate = 2_560_000,
        ContainerFormat = "matroska,webm",
        Video = new VideoStreamInfo
        {
            Index = 0, Codec = "hevc", Profile = "Main 10", Width = w, Height = h, FrameRate = 24000.0 / 1001,
            PixelFormat = "yuv420p10le", ColorTransfer = "smpte2084", Rotation = rotation,
        },
        AudioStreams = [new AudioStreamInfo { Index = 1, Codec = "eac3", SampleRate = 48000, Channels = 6, ChannelLayout = "5.1(side)", BitRate = 640_000, Language = "ger" }],
    };

    /// <summary>Runs a process and returns true on exit code 0.</summary>
    public static bool Run(string file, params string[] args)
    {
        try
        {
            var psi = new ProcessStartInfo(file) { RedirectStandardError = true, RedirectStandardOutput = true, UseShellExecute = false };
            foreach (var a in args) psi.ArgumentList.Add(a);
            using var p = Process.Start(psi)!;
            p.StandardOutput.ReadToEnd();
            p.StandardError.ReadToEnd();
            p.WaitForExit();
            return p.ExitCode == 0;
        }
        catch { return false; }
    }
}
