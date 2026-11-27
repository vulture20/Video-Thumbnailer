using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using VideoThumbnailer.Core;
using VideoThumbnailer.Core.Batch;
using VideoThumbnailer.Core.Ffmpeg;
using VideoThumbnailer.Core.Output;
using VideoThumbnailer.Core.Settings;
using VideoThumbnailer.Core.Viewer;
using VideoThumbnailer.Gui.ViewModels;
using VideoThumbnailer.Gui.Views;

namespace VideoThumbnailer.Tests;

/// <summary>Sheet viewer (#39): players, trash, viewer window.</summary>
public class ViewerTests
{
    private static T Named<T>(Window w, string name) where T : Control =>
        Avalonia.LogicalTree.LogicalExtensions.GetLogicalDescendants(w).OfType<T>().First(c => c.Name == name);

    [Fact]
    public void Player_command_lines()
    {
        Assert.Equal(["--start=90.5", "--", "/v/a b.mkv"], PlayerLauncher.Build(PlayerKind.Mpv, "mpv", "/v/a b.mkv", 90.5).Arguments);
        Assert.Equal(["--start-time=90.5", "/v/a.mkv"], PlayerLauncher.Build(PlayerKind.Vlc, "vlc", "/v/a.mkv", 90.5).Arguments);
        Assert.Equal(["C:\\a.mkv", "/start", "90500"], PlayerLauncher.Build(PlayerKind.MpcHc, "mpc-hc64.exe", "C:\\a.mkv", 90.5).Arguments);
        Assert.Equal(["C:\\a.mkv", "/seek=00:01:30.500"], PlayerLauncher.Build(PlayerKind.PotPlayer, "PotPlayerMini64.exe", "C:\\a.mkv", 90.5).Arguments);

        var custom = PlayerLauncher.FromTemplate("\"C:\\Program Files\\My Player\\p.exe\" --pos {ms} \"{file}\"", "C:\\v\\a b.mkv", 2.25)!;
        Assert.Equal("C:\\Program Files\\My Player\\p.exe", custom.FileName);
        Assert.Equal(["--pos", "2250", "C:\\v\\a b.mkv"], custom.Arguments);
        Assert.True(custom.SupportsTime);
        var noFile = PlayerLauncher.FromTemplate("player --at={time}", "/v/a.mkv", 3661)!;
        Assert.Equal(["--at=01:01:01.000", "/v/a.mkv"], noFile.Arguments);
        Assert.Null(PlayerLauncher.Resolve(PlayerKind.Custom, "  ", "/v/a.mkv", 1));
        Assert.False(PlayerLauncher.Resolve(PlayerKind.System, null, "/v/a.mkv", 1)!.SupportsTime);
    }

    [Fact]
    public void Home_trash_follows_the_freedesktop_spec()
    {
        var dir = TestUtil.TempDir();
        var trash = Path.Combine(dir, "Trash");
        var a = Path.Combine(dir, "Mein Film.mkv");
        File.WriteAllText(a, "1");
        var first = Trash.MoveToHomeTrash(a, trash);
        File.WriteAllText(a, "2");
        var second = Trash.MoveToHomeTrash(a, trash);
        Assert.False(File.Exists(a));
        Assert.Equal(["Mein Film.mkv", "Mein Film.2.mkv"], new[] { Path.GetFileName(first), Path.GetFileName(second) });
        var info = File.ReadAllText(Path.Combine(trash, "info", "Mein Film.mkv.trashinfo"));
        Assert.StartsWith("[Trash Info]\nPath=", info);
        Assert.Contains("Mein%20Film.mkv", info);
        Assert.Contains("DeletionDate=", info);
        Assert.Equal("/a/%C3%BC%20b.mkv", Trash.EscapePath("/a/ü b.mkv"));
    }

    [AvaloniaFact]
    public async Task Viewer_shows_the_sheet_plays_and_trashes()
    {
        var t = FfmpegLocator.FromPath();
        if (t is null) Assert.Skip("ffmpeg/ffprobe not on PATH");
        var home = TestUtil.TempDir();
        AppPaths.SetRootOverride(home);
        var trashed = new List<string>();
        var oldTrash = ViewerViewModel.MoveToTrash;
        ViewerViewModel.MoveToTrash = f => { trashed.Add(f); File.Delete(f); };
        try
        {
            var dir = TestUtil.TempDir();
            var video = Path.Combine(dir, "view.mp4");
            Assert.True(TestUtil.Run(t.FfmpegPath, "-v", "error", "-y", "-f", "lavfi", "-i", "testsrc2=size=320x180:rate=25:duration=12", "-pix_fmt", "yuv420p", video));
            var s = new SheetSettings { Columns = 3, Rows = 2, SheetWidth = 600 };
            s.Output.Sidecar = true;
            s.Output.WebVtt = true;
            var job = new ThumbnailJob(video);
            await new BatchProcessor(t).RunAsync([job], s, new BatchOptions(), TestContext.Current.CancellationToken);
            Assert.Equal(JobStatus.Done, job.Status);

            var app = new AppSettings { Player = PlayerKind.Custom };
            // opened through the sheet image: the sidecar next to it is found
            var viewer = await ViewerViewModel.OpenAsync(job.OutputPath!, app, t);
            Assert.Equal(Path.GetFullPath(video), viewer.VideoPath);
            Assert.NotNull(viewer.Image);
            Assert.Equal(viewer.Sidecar.Sheet.Width, viewer.Image!.PixelSize.Width);
            var tile = viewer.TileAt(0.5, 0.75)!;
            Assert.Equal(4, tile.Index);
            viewer.Hover(tile);
            Assert.Contains("5", viewer.HoverText);

            if (!OperatingSystem.IsWindows())
            {
                var output = Path.Combine(dir, "played.txt");
                viewer.PlayerCommand = $"/bin/sh -c \"echo {{seconds}} > '{output}'\"";
                Assert.Equal(viewer.PlayerCommand, app.PlayerCommand);
                viewer.Play(tile);
                for (var i = 0; i < 50 && !File.Exists(output); i++) await Task.Delay(100, TestContext.Current.CancellationToken);
                Assert.Equal(tile.Time, double.Parse(File.ReadAllText(output).Trim(), System.Globalization.CultureInfo.InvariantCulture), 3);
            }

            var window = new ViewerWindow(viewer);
            window.Show();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            Assert.True(Named<Button>(window, "ViewerTrash").IsEnabled);
            Assert.True(Named<TextBox>(window, "ViewerPlayerCommand").IsVisible);
            if (Environment.GetEnvironmentVariable("VTHUMB_FEATURE_SHOTS") is { Length: > 0 } shots)
            {
#pragma warning disable CS0618
                window.CaptureRenderedFrame()?.Save(Path.Combine(shots, "viewer.png"));
#pragma warning restore CS0618
            }

            Assert.Equal(4, viewer.SheetFiles().Count); // sheet, sidecar, .thumbnails.vtt/.jpg
            string? deleted = null;
            viewer.VideoDeleted += v => deleted = v;
            Assert.True(viewer.Delete(withSheet: true));
            Assert.Equal(Path.GetFullPath(video), deleted);
            Assert.Equal(5, trashed.Count);
            Assert.False(viewer.CanDelete);
            Assert.False(File.Exists(video));
            window.Close();

            await Assert.ThrowsAsync<InvalidDataException>(() => ViewerViewModel.OpenAsync(job.OutputPath!, app, t));
        }
        finally
        {
            ViewerViewModel.MoveToTrash = oldTrash;
            AppPaths.SetRootOverride(null);
        }
    }
}
