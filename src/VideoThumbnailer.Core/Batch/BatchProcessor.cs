using System.Diagnostics;
using VideoThumbnailer.Core.Ffmpeg;
using VideoThumbnailer.Core.Localization;
using VideoThumbnailer.Core.Settings;

namespace VideoThumbnailer.Core.Batch;

public sealed record BatchStats(int Total, int Done, int Skipped, int Failed, int Cancelled, TimeSpan Elapsed);

public sealed class BatchOptions
{
    /// <summary>Maximum number of ffmpeg/ffprobe processes at the same time.</summary>
    public int MaxParallelProcesses { get; set; } = DefaultParallelism;
    /// <summary>Maximum number of videos worked on at the same time.</summary>
    public int MaxParallelVideos { get; set; } = Math.Max(1, DefaultParallelism / 2);

    public static int DefaultParallelism => Math.Clamp(Environment.ProcessorCount, 1, 16);
}

/// <summary>Runs a list of jobs with bounded parallelism, pause/resume and cancellation.</summary>
public sealed class BatchProcessor(FfmpegTools tools)
{
    public PauseGate Pause { get; } = new();

    public async Task<BatchStats> RunAsync(IReadOnlyList<ThumbnailJob> jobs, SheetSettings settings, BatchOptions options, CancellationToken ct)
    {
        var s = settings.Clone();
        s.Normalize();
        var sw = Stopwatch.StartNew();
        using var processLimiter = new SemaphoreSlim(Math.Max(1, options.MaxParallelProcesses));
        var generator = new SheetGenerator(tools, processLimiter, Pause);
        var pending = jobs.Where(j => j.Status == JobStatus.Pending).ToList();

        await Parallel.ForEachAsync(pending,
            new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, options.MaxParallelVideos), CancellationToken = CancellationToken.None },
            async (job, _) =>
            {
                if (ct.IsCancellationRequested) { job.Update(JobStatus.Cancelled, message: CoreText.T("Cancelled")); return; }
                var jsw = Stopwatch.StartNew();
                job.Update(JobStatus.Running, 0);
                try
                {
                    var result = await generator.GenerateAsync(job.InputPath, job.InputRoot, s, new SyncProgress(job), ct).ConfigureAwait(false);
                    job.Update(result.Status, result.Status == JobStatus.Done ? 1 : null, result.Message ?? "", result.SheetPath ?? "", jsw.Elapsed);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    job.Update(JobStatus.Cancelled, message: CoreText.T("Cancelled"), elapsed: jsw.Elapsed);
                }
                catch (Exception ex)
                {
                    job.Update(JobStatus.Failed, message: ex.Message, elapsed: jsw.Elapsed);
                }
            }).ConfigureAwait(false);

        return new BatchStats(
            pending.Count,
            pending.Count(j => j.Status == JobStatus.Done),
            pending.Count(j => j.Status == JobStatus.Skipped),
            pending.Count(j => j.Status == JobStatus.Failed),
            pending.Count(j => j.Status == JobStatus.Cancelled),
            sw.Elapsed);
    }

    /// <summary>Reports progress synchronously (Progress&lt;T&gt; would post to a captured context).</summary>
    private sealed class SyncProgress(ThumbnailJob job) : IProgress<double>
    {
        public void Report(double value) => job.Update(progress: value);
    }
}
