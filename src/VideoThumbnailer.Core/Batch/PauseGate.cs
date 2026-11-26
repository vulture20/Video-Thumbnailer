namespace VideoThumbnailer.Core.Batch;

/// <summary>Async gate: while paused, <see cref="WaitAsync"/> blocks until resumed.</summary>
public sealed class PauseGate
{
    private volatile TaskCompletionSource _tcs = CompletedSource();

    public bool IsPaused { get; private set; }

    public void Pause()
    {
        lock (this)
        {
            if (IsPaused) return;
            IsPaused = true;
            _tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        }
    }

    public void Resume()
    {
        lock (this)
        {
            if (!IsPaused) return;
            IsPaused = false;
            _tcs.TrySetResult();
        }
    }

    public Task WaitAsync(CancellationToken ct) => _tcs.Task.WaitAsync(ct);

    private static TaskCompletionSource CompletedSource()
    {
        var t = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        t.SetResult();
        return t;
    }
}
