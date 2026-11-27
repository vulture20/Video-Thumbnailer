using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.VisualTree;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using VideoThumbnailer.Core;
using VideoThumbnailer.Core.Batch;
using VideoThumbnailer.Core.Ffmpeg;
using VideoThumbnailer.Core.Layout;
using VideoThumbnailer.Core.Settings;
using VideoThumbnailer.Gui.Input;
using VideoThumbnailer.Gui.Localization;
using VideoThumbnailer.Gui.ViewModels;

namespace VideoThumbnailer.Tests;

/// <summary>Putting finished queue entries back to "waiting" (re-queue).</summary>
public class RequeueTests
{
    [Fact]
    public void Requeue_only_works_on_finished_jobs_and_keeps_manual_times()
    {
        var job = new ThumbnailJob("/videos/a.mp4") { ManualTimes = [new ManualTime(12.5)] };
        Assert.False(job.Requeue()); // waiting
        job.Update(JobStatus.Running, 0.5);
        Assert.False(job.Requeue()); // running
        Assert.False(job.ReplaceExisting);

        job.Update(JobStatus.Done, 1, "ok", "/videos/a.jpg", TimeSpan.FromSeconds(3));
        var changed = 0;
        job.Changed += _ => changed++;
        Assert.True(job.Requeue());
        Assert.Equal(1, changed);
        Assert.Equal(JobStatus.Pending, job.Status);
        Assert.Equal(0, job.Progress);
        Assert.Null(job.OutputPath);
        Assert.Null(job.Message);
        Assert.True(job.ReplaceExisting);
        Assert.True(job.HasManualTimes);
        Assert.Equal(12.5, job.ManualTimes![0]!.Time);
    }

    [Fact]
    public async Task Requeued_job_replaces_its_sheet_even_when_existing_ones_are_skipped()
    {
        var t = FfmpegLocator.FromPath();
        if (t is null) Assert.Skip("ffmpeg/ffprobe not on PATH");
        var dir = TestUtil.TempDir();
        var video = Path.Combine(dir, "clip.mp4");
        Assert.True(TestUtil.Run(t.FfmpegPath, "-v", "error", "-y", "-f", "lavfi", "-i", "testsrc2=size=320x240:rate=25:duration=4", "-pix_fmt", "yuv420p", video));
        var s = new SheetSettings { Columns = 2, Rows = 2, SheetWidth = 400 };
        s.Output.Overwrite = OverwriteMode.Skip;
        var ct = TestContext.Current.CancellationToken;

        var job = new ThumbnailJob(video);
        await new BatchProcessor(t).RunAsync([job], s, new BatchOptions(), ct);
        Assert.Equal(JobStatus.Done, job.Status);
        var sheet = job.OutputPath!;
        File.SetLastWriteTimeUtc(sheet, new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        // A fresh entry for the same video is skipped ...
        var other = new ThumbnailJob(video);
        await new BatchProcessor(t).RunAsync([other], s, new BatchOptions(), ct);
        Assert.Equal(JobStatus.Skipped, other.Status);

        // ... a re-queued one makes the sheet again, with the settings of this run.
        s.Columns = 3;
        Assert.True(job.Requeue());
        var stats = await new BatchProcessor(t).RunAsync([job], s, new BatchOptions(), ct);
        Assert.Equal(1, stats.Done);
        Assert.Equal(JobStatus.Done, job.Status);
        Assert.Equal(sheet, job.OutputPath);
        Assert.True(File.GetLastWriteTimeUtc(sheet).Year > 2000);

        // "Rename" still never touches the old file.
        s.Output.Overwrite = OverwriteMode.Rename;
        Assert.True(job.Requeue());
        await new BatchProcessor(t).RunAsync([job], s, new BatchOptions(), ct);
        Assert.Equal(JobStatus.Done, job.Status);
        Assert.NotEqual(sheet, job.OutputPath);
    }

    [Fact]
    public void Ctrl_R_is_free_and_shown_in_tooltips()
    {
        var gestures = Shortcuts.All.SelectMany(a => a.Value.Select(k => (k.Key, k.Command, k.Shift))).ToList();
        Assert.Equal(gestures.Count, gestures.Distinct().Count());
        var mac = Shortcuts.IsMac;
        try
        {
            Shortcuts.IsMac = false;
            Assert.Equal("Strg+R", Shortcuts.Display("RequeueSelected", "de"));
            Shortcuts.IsMac = true;
            Assert.Equal("⌘R", Shortcuts.Display("RequeueSelected", "de"));
        }
        finally { Shortcuts.IsMac = mac; }
    }

    private static List<JobViewModel> Queue(MainViewModel vm, params string[] names)
    {
        var dir = TestUtil.TempDir();
        foreach (var n in names) File.WriteAllBytes(Path.Combine(dir, n), [0]);
        vm.AddPaths([dir]);
        Dispatcher.UIThread.RunJobs();
        return names.Select(n => vm.Jobs.Single(j => j.FileName == n)).ToList();
    }

    private static void Finish(JobViewModel j, JobStatus status)
    {
        j.Job.Update(status, 1, status.ToString(), status == JobStatus.Done ? j.Job.InputPath + ".jpg" : null);
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void Queue_commands_put_finished_entries_back_in_place()
    {
        var home = TestUtil.TempDir();
        AppPaths.SetRootOverride(home);
        try
        {
            new AppSettings { FfmpegDownloadAsked = true, CheckFfmpegUpdates = false, Language = "German" }.Save();
            var (window, vm, _) = GuiShortcutTests.Open();
            var jobs = Queue(vm, "a.mp4", "b.mp4", "c.mp4", "d.mp4");
            var (a, b, c, d) = (jobs[0], jobs[1], jobs[2], jobs[3]);
            Assert.False(vm.RequeueAllFinishedCommand.CanExecute(null));
            Assert.False(vm.RetryFailedCommand.CanExecute(null));
            Assert.False(vm.RequeueJobCommand.CanExecute(a));

            Finish(a, JobStatus.Done);
            Finish(b, JobStatus.Failed);
            Finish(c, JobStatus.Skipped);
            Assert.True(vm.RequeueJobCommand.CanExecute(a));
            Assert.False(vm.RequeueJobCommand.CanExecute(d)); // still waiting
            Assert.True(vm.RetryFailedCommand.CanExecute(null));

            // The ↻ button: one entry, stays at its place.
            vm.RequeueJobCommand.Execute(a);
            Assert.Equal(JobStatus.Pending, a.Status);
            Assert.Equal("Wartet", a.StatusText);
            Assert.Same(a, vm.Jobs[0]);
            Assert.True(a.Job.ReplaceExisting);
            Assert.Contains("1", vm.StatusText);

            // "Retry failed" leaves done and skipped ones alone.
            vm.RetryFailedCommand.Execute(null);
            Assert.Equal(JobStatus.Pending, b.Status);
            Assert.Equal(JobStatus.Skipped, c.Status);
            Assert.False(vm.RetryFailedCommand.CanExecute(null));

            // "All finished": skipped ones too.
            vm.RequeueAllFinishedCommand.Execute(null);
            Assert.Equal(JobStatus.Pending, c.Status);
            Assert.False(vm.RequeueAllFinishedCommand.CanExecute(null));
            Assert.Equal(["a.mp4", "b.mp4", "c.mp4", "d.mp4"], vm.Jobs.Select(j => j.FileName));

            // Ctrl/Cmd+R on the selection; only the finished ones of it change.
            Finish(a, JobStatus.Done);
            Finish(b, JobStatus.Done);
            var list = window.FindControl<ListBox>("QueueList")!;
            Assert.False(vm.RequeueSelectedCommand.CanExecute(null));
            list.SelectedItems!.Add(a);
            list.SelectedItems.Add(d);
            Assert.True(vm.RequeueSelectedCommand.CanExecute(null));
            GuiShortcutTests.Press(window, Key.R, OperatingSystem.IsMacOS() ? RawInputModifiers.Meta : RawInputModifiers.Control);
            Assert.Equal(JobStatus.Pending, a.Status);
            Assert.Equal(JobStatus.Done, b.Status);
            Assert.Equal(JobStatus.Pending, d.Status);
            Assert.False(vm.RequeueSelectedCommand.CanExecute(null));

            if (Environment.GetEnvironmentVariable("VTHUMB_FEATURE_SHOTS") is { Length: > 0 } shots)
            {
                Finish(c, JobStatus.Failed);
                list.SelectedItems.Clear();
                window.UpdateLayout();
                var item = list.ContainerFromItem(b)!;
                var p = item.TranslatePoint(new Avalonia.Point(40, 12), window)!.Value;
                window.MouseMove(p, RawInputModifiers.None);
                Dispatcher.UIThread.RunJobs();
#pragma warning disable CS0618
                window.CaptureRenderedFrame()?.Save(Path.Combine(shots, "requeue-hover.png"));
                list.SelectedItems.Add(b);
                list.SelectedItems.Add(c);
                list.ContextMenu!.Open(list);
                Dispatcher.UIThread.RunJobs();
                window.CaptureRenderedFrame()?.Save(Path.Combine(shots, "requeue-menu.png"));
                list.ContextMenu.Close();
#pragma warning restore CS0618
                list.SelectedItems.Clear();
                window.MouseMove(new Avalonia.Point(700, 400), RawInputModifiers.None);
                Finish(c, JobStatus.Skipped);
            }

            // The tooltips of the list and the menu entry name the key.
            Assert.Contains("Strg/Cmd+R", ToolTip.GetTip(list) as string);
            Assert.Contains(OperatingSystem.IsMacOS() ? "⌘R" : "Strg+R", ToolTip.GetTip(window.FindControl<MenuItem>("RequeueSelectedItem")!) as string);
            window.Close();
        }
        finally { AppPaths.SetRootOverride(null); }
    }

    [AvaloniaFact]
    public void Nothing_is_requeued_while_a_run_is_going()
    {
        var home = TestUtil.TempDir();
        AppPaths.SetRootOverride(home);
        try
        {
            new AppSettings { FfmpegDownloadAsked = true, CheckFfmpegUpdates = false, Language = "English" }.Save();
            var (window, vm, _) = GuiShortcutTests.Open();
            var a = Queue(vm, "a.mp4")[0];
            Finish(a, JobStatus.Done);
            vm.IsRunning = true;
            Assert.False(vm.RequeueJobCommand.CanExecute(a));
            Assert.False(vm.RequeueAllFinishedCommand.CanExecute(null));
            Assert.Equal(0, vm.Requeue([a]));
            Assert.Equal(JobStatus.Done, a.Status);
            vm.IsRunning = false;
            Assert.True(vm.RequeueJobCommand.CanExecute(a));
            Assert.Equal(Loc.T("StatusRequeued", 1), (vm.Requeue([a]), vm.StatusText).StatusText);
            window.Close();
        }
        finally { AppPaths.SetRootOverride(null); }
    }
}
