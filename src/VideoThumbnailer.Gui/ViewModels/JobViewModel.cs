using CommunityToolkit.Mvvm.ComponentModel;
using VideoThumbnailer.Core.Batch;
using VideoThumbnailer.Core.Input;
using VideoThumbnailer.Gui.Localization;

namespace VideoThumbnailer.Gui.ViewModels;

/// <summary>UI mirror of a <see cref="ThumbnailJob"/>; updated on the UI thread.</summary>
public partial class JobViewModel : ObservableObject
{
    public ThumbnailJob Job { get; }
    public string FileName { get; }
    public string Folder { get; }
    public long FileSize { get; }
    /// <summary>Video on a web server (URL), read directly by ffmpeg.</summary>
    public bool IsRemote { get; }
    /// <summary>Address or path for the tooltip (URLs without credentials and query).</summary>
    public string ShownPath => VideoSource.DisplayPath(Job.InputPath);

    /// <summary>Some capture times of this video were chosen by hand.</summary>
    public bool HasManualTimes => Job.HasManualTimes;
    public int ManualTimeCount => Job.ManualTimes?.Count(t => t is not null) ?? 0;
    public string ManualTimesText => Loc.T("JobManualTimes", ManualTimeCount);

    /// <summary>Call after <see cref="ThumbnailJob.ManualTimes"/> changed.</summary>
    public void RefreshManualTimes()
    {
        OnPropertyChanged(nameof(HasManualTimes));
        OnPropertyChanged(nameof(ManualTimeCount));
        OnPropertyChanged(nameof(ManualTimesText));
    }

    [ObservableProperty] private JobStatus _status;
    [ObservableProperty] private double _progress;
    [ObservableProperty] private string? _message;
    [ObservableProperty] private string? _outputPath;

    public JobViewModel(ThumbnailJob job)
    {
        Job = job;
        FileName = VideoSource.FileName(job.InputPath);
        Folder = VideoSource.Folder(job.InputPath);
        IsRemote = VideoSource.IsUrl(job.InputPath);
        // Remote videos: the size is only known once ffprobe has read them.
        if (!IsRemote) try { FileSize = new FileInfo(job.InputPath).Length; } catch { FileSize = 0; }
        Refresh();
    }

    public string StatusText => Status switch
    {
        JobStatus.Pending => Loc.T("JobWaiting"),
        JobStatus.Running => $"{Progress:P0}",
        JobStatus.Done => Loc.T("JobDone"),
        JobStatus.Skipped => Loc.T("JobSkipped"),
        JobStatus.Failed => Loc.T("JobFailed"),
        JobStatus.Cancelled => Loc.T("JobCancelled"),
        _ => "",
    };

    public string Details => string.IsNullOrEmpty(Message) ? (OutputPath ?? Folder) : Message!;
    public bool IsFinished => Job.IsFinished;
    // Used as style classes for the colored status in the queue.
    public bool IsOk => Status == JobStatus.Done;
    public bool IsError => Status == JobStatus.Failed;
    public bool IsActive => Status == JobStatus.Running;
    public double ProgressPercent => Progress * 100;

    public void Refresh()
    {
        Status = Job.Status;
        Progress = Job.Progress;
        Message = Job.Message;
        OutputPath = Job.OutputPath;
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(Details));
        OnPropertyChanged(nameof(IsFinished));
        OnPropertyChanged(nameof(ProgressPercent));
        OnPropertyChanged(nameof(IsOk));
        OnPropertyChanged(nameof(IsError));
        OnPropertyChanged(nameof(IsActive));
        RefreshManualTimes();
    }
}
