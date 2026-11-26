using CommunityToolkit.Mvvm.ComponentModel;
using VideoThumbnailer.Core.Batch;
using VideoThumbnailer.Gui.Localization;

namespace VideoThumbnailer.Gui.ViewModels;

/// <summary>UI mirror of a <see cref="ThumbnailJob"/>; updated on the UI thread.</summary>
public partial class JobViewModel : ObservableObject
{
    public ThumbnailJob Job { get; }
    public string FileName { get; }
    public string Folder { get; }
    public long FileSize { get; }

    [ObservableProperty] private JobStatus _status;
    [ObservableProperty] private double _progress;
    [ObservableProperty] private string? _message;
    [ObservableProperty] private string? _outputPath;

    public JobViewModel(ThumbnailJob job)
    {
        Job = job;
        FileName = Path.GetFileName(job.InputPath);
        Folder = Path.GetDirectoryName(job.InputPath) ?? "";
        try { FileSize = new FileInfo(job.InputPath).Length; } catch { FileSize = 0; }
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
    }
}
