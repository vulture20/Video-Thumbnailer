using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using SkiaSharp;
using VideoThumbnailer.Cli;
using VideoThumbnailer.Core.Batch;
using VideoThumbnailer.Core.Input;
using VideoThumbnailer.Core.Output;
using VideoThumbnailer.Core.Rendering;
using VideoThumbnailer.Core.Settings;

namespace VideoThumbnailer.Tests;

/// <summary>Issue #21: videos given as http(s) address, read by ffmpeg without downloading the whole file.</summary>
public class RemoteVideoTests
{
    private const string Url = "https://user:secret@media.example.org/films/My%20Holiday.mp4?token=abc#t=3";

    [Fact]
    public void Url_names_folder_and_shown_path_leave_out_credentials_and_query()
    {
        Assert.True(VideoSource.IsUrl(Url));
        Assert.True(VideoSource.IsUrl("HTTP://host/x.mkv"));
        Assert.False(VideoSource.IsUrl("/videos/a.mp4"));
        Assert.False(VideoSource.IsUrl(@"C:\videos\a.mp4"));
        Assert.False(VideoSource.IsUrl("ftp://host/a.mp4"));
        Assert.False(VideoSource.IsUrl("https://"));

        Assert.Equal("My Holiday.mp4", VideoSource.FileName(Url));
        Assert.Equal("My Holiday", VideoSource.NameWithoutExtension(Url));
        Assert.Equal("mp4", VideoSource.Extension(Url));
        Assert.Equal("https://media.example.org/films/My Holiday.mp4", VideoSource.DisplayPath(Url));
        Assert.Equal("https://media.example.org/films/", VideoSource.Folder(Url));
        Assert.Equal("media.example.org", VideoSource.FileName("https://media.example.org/"));
        Assert.Contains("-rw_timeout", VideoSource.InputOptions(Url));
        Assert.Empty(VideoSource.InputOptions("/videos/a.mp4"));
    }

    [Fact]
    public void Header_placeholders_and_output_path_of_a_url()
    {
        var info = TestUtil.SampleInfo(Url);
        Assert.Equal("My Holiday.mp4 | My Holiday | https://media.example.org/films/ | https://media.example.org/films/My Holiday.mp4",
            InfoTemplate.Expand("{filename} | {name} | {folder} | {path}", info, "en"));

        var s = new SheetSettings();
        var old = OutputPathResolver.RemoteFallbackFolder;
        try
        {
            OutputPathResolver.RemoteFallbackFolder = Path.GetFullPath("/fallback");
            // "Next to the video" does not exist for a URL: fallback folder, or the output folder when one is set.
            Assert.Equal(Path.Combine(Path.GetFullPath("/fallback"), "My Holiday.jpg"), OutputPathResolver.ResolveSheetPath(s, Url, null));
            s.Output.Location = OutputLocation.SubfolderNextToVideo;
            Assert.Equal(Path.Combine(Path.GetFullPath("/fallback"), "My Holiday.jpg"), OutputPathResolver.ResolveSheetPath(s, Url, null));
            s.Output.Location = OutputLocation.OutputFolder;
            s.Output.Folder = "/out";
            s.Output.KeepFolderStructure = true;
            s.Output.FileNamePattern = "{name}_{ext}";
            Assert.Equal(Path.Combine(Path.GetFullPath("/out"), "My Holiday_mp4.jpg"), OutputPathResolver.ResolveSheetPath(s, Url, null));
        }
        finally { OutputPathResolver.RemoteFallbackFolder = old; }
    }

    [Fact]
    public void Scanner_and_command_line_take_urls_as_they_are()
    {
        var found = VideoScanner.Scan([Url, Url, "https://host/b.webm"], recursive: true);
        Assert.Equal([Url, "https://host/b.webm"], found.Select(v => v.Path));
        Assert.All(found, v => Assert.Null(v.Root));
        Assert.Equal([Url], ArgParser.ParseGenerate(["-c", "2", Url]).Inputs);
    }

    [Fact]
    public async Task Creates_sheet_from_http_server_with_range_requests()
    {
        var t = PreviewSourceTests.Tools();
        var dir = TestUtil.TempDir();
        var file = Path.Combine(dir, "remote clip.mp4");
        // 20 s with a key frame every second, so seeking needs only small parts of the file.
        Assert.True(TestUtil.Run(t.FfmpegPath, "-v", "error", "-y", "-f", "lavfi", "-i", "testsrc2=size=640x360:rate=25:duration=20",
            "-g", "25", "-pix_fmt", "yuv420p", "-b:v", "2M", file));

        using var server = new RangeServer(file);
        var url = server.BaseUrl + "videos/remote%20clip.mp4?key=1";
        var outDir = Path.Combine(dir, "out");
        var s = new SheetSettings { Columns = 2, Rows = 2, SheetWidth = 640 };
        s.Header.Template = "{filename}\n{size:MB} · {duration}";
        s.Output.Location = OutputLocation.OutputFolder;
        s.Output.Folder = outDir;

        var job = new ThumbnailJob(url);
        var stats = await new BatchProcessor(t).RunAsync([job], s, new BatchOptions { MaxParallelProcesses = 2 }, TestContext.Current.CancellationToken);

        Assert.True(stats.Done == 1, job.Message);
        Assert.Equal(Path.Combine(outDir, "remote clip.jpg"), job.OutputPath);
        using var bmp = SKBitmap.Decode(job.OutputPath);
        Assert.True(bmp.Width > 600);
        // ffmpeg jumped into the file instead of reading it from the start.
        Assert.Contains(server.RangeStarts, start => start > 0);
    }

    [Fact]
    public async Task Unreachable_url_fails_with_a_message()
    {
        var t = PreviewSourceTests.Tools();
        int port;
        using (var l = new TcpListener(IPAddress.Loopback, 0)) { l.Start(); port = ((IPEndPoint)l.LocalEndpoint).Port; }
        var s = new SheetSettings();
        s.Output.Location = OutputLocation.OutputFolder;
        s.Output.Folder = TestUtil.TempDir();
        var job = new ThumbnailJob($"http://127.0.0.1:{port}/missing.mp4");
        var stats = await new BatchProcessor(t).RunAsync([job], s, new BatchOptions(), TestContext.Current.CancellationToken);
        Assert.Equal(1, stats.Failed);
        Assert.False(string.IsNullOrWhiteSpace(job.Message));
    }

    /// <summary>Minimal HTTP server for one file that answers range requests (206) like a normal web server.</summary>
    private sealed class RangeServer : IDisposable
    {
        private readonly HttpListener _listener = new();
        private readonly byte[] _data;
        public ConcurrentBag<long> RangeStarts { get; } = [];
        public string BaseUrl { get; }

        public RangeServer(string file)
        {
            _data = File.ReadAllBytes(file);
            int port;
            using (var l = new TcpListener(IPAddress.Loopback, 0)) { l.Start(); port = ((IPEndPoint)l.LocalEndpoint).Port; }
            BaseUrl = $"http://127.0.0.1:{port}/";
            _listener.Prefixes.Add(BaseUrl);
            _listener.Start();
            _ = Task.Run(LoopAsync);
        }

        private async Task LoopAsync()
        {
            while (_listener.IsListening)
            {
                HttpListenerContext ctx;
                try { ctx = await _listener.GetContextAsync(); }
                catch { return; }
                _ = Task.Run(() => Serve(ctx));
            }
        }

        private void Serve(HttpListenerContext ctx)
        {
            try
            {
                var res = ctx.Response;
                res.ContentType = "video/mp4";
                res.AddHeader("Accept-Ranges", "bytes");
                long start = 0, end = _data.Length - 1;
                var range = ctx.Request.Headers["Range"];
                if (range is not null && range.StartsWith("bytes=", StringComparison.Ordinal))
                {
                    var parts = range[6..].Split('-');
                    start = long.Parse(parts[0]);
                    if (parts.Length > 1 && parts[1].Length > 0) end = Math.Min(end, long.Parse(parts[1]));
                    RangeStarts.Add(start);
                    res.StatusCode = 206;
                    res.AddHeader("Content-Range", $"bytes {start}-{end}/{_data.Length}");
                }
                res.ContentLength64 = end - start + 1;
                if (ctx.Request.HttpMethod != "HEAD")
                    res.OutputStream.Write(_data, (int)start, (int)(end - start + 1));
                res.Close();
            }
            catch { /* client closed the connection after reading what it needed */ }
        }

        public void Dispose()
        {
            try { _listener.Stop(); _listener.Close(); } catch { /* ignore */ }
        }
    }
}
