namespace VideoThumbnailer.Core.Batch;

public enum JobStatus { Pending, Running, Done, Skipped, Failed, Cancelled }

/// <summary>One video in the queue. <see cref="Changed"/> fires from worker threads.</summary>
public sealed class ThumbnailJob(string inputPath, string? inputRoot = null)
{
    public Guid Id { get; } = Guid.NewGuid();
    public string InputPath { get; } = inputPath;
    public string? InputRoot { get; } = inputRoot;
    public JobStatus Status { get; private set; } = JobStatus.Pending;
    public double Progress { get; private set; }
    public string? Message { get; private set; }
    public string? OutputPath { get; private set; }
    public TimeSpan Elapsed { get; private set; }

    public event Action<ThumbnailJob>? Changed;

    public bool IsFinished => Status is JobStatus.Done or JobStatus.Skipped or JobStatus.Failed or JobStatus.Cancelled;

    internal void Update(JobStatus? status = null, double? progress = null, string? message = null, string? output = null, TimeSpan? elapsed = null)
    {
        if (status is { } s) Status = s;
        if (progress is { } p) Progress = Math.Clamp(p, 0, 1);
        if (message is not null) Message = message;
        if (output is not null) OutputPath = output;
        if (elapsed is { } e) Elapsed = e;
        Changed?.Invoke(this);
    }

    /// <summary>Puts a finished job back to pending so it can run again.</summary>
    public void Reset()
    {
        Status = JobStatus.Pending; Progress = 0; Message = null; OutputPath = null; Elapsed = TimeSpan.Zero;
        Changed?.Invoke(this);
    }
}
