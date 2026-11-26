using Avalonia;
using Avalonia.Styling;
using System.Collections.ObjectModel;
using System.Diagnostics;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VideoThumbnailer.Core.Batch;
using VideoThumbnailer.Core.Extraction;
using VideoThumbnailer.Core.Ffmpeg;
using VideoThumbnailer.Core.Input;
using VideoThumbnailer.Core.Layout;
using VideoThumbnailer.Core.Rendering;
using VideoThumbnailer.Core.Settings;
using VideoThumbnailer.Core.Util;
using VideoThumbnailer.Gui.Localization;

namespace VideoThumbnailer.Gui.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IUiServices _ui;
    private readonly AppSettings _app;
    private readonly PresetStore _presets = new();
    private BatchProcessor? _processor;
    private CancellationTokenSource? _cts;
    private readonly Stopwatch _runClock = new();
    private readonly DispatcherTimer _clockTimer;
    private readonly DispatcherTimer _previewTimer = new() { Interval = TimeSpan.FromMilliseconds(150) };
    private int _previewGeneration;

    public ObservableCollection<JobViewModel> Jobs { get; } = [];
    public ObservableCollection<JobViewModel> SelectedJobs { get; } = [];
    public ObservableCollection<SheetSettings> Presets { get; } = [];
    public SettingsViewModel Settings { get; } = new();
    public FfmpegViewModel Ffmpeg { get; }

    [ObservableProperty] private SheetSettings? _selectedPreset;
    [ObservableProperty] private bool _recursive;
    [ObservableProperty] private decimal _maxParallelProcesses;
    [ObservableProperty] private decimal _maxParallelVideos;
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartCommand), nameof(PauseCommand), nameof(CancelCommand), nameof(ClearAllCommand), nameof(RemoveSelectedCommand))]
    [NotifyPropertyChangedFor(nameof(IsIdle))]
    private bool _isRunning;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(PauseButtonText))] private bool _isPaused;
    [ObservableProperty] private double _overallProgress;
    [ObservableProperty] private string _statusText = Loc.T("StatusIntro");
    [ObservableProperty] private UiLanguage _language;

    // ---- live preview (center of the window)
    [ObservableProperty] private Bitmap? _previewImage;
    [ObservableProperty] private string _previewCaption = "";
    /// <summary>Video whose name and size appear in the preview: the selected one, otherwise the first in the queue.</summary>
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(PreviewTitle), nameof(HasPreviewManualTimes), nameof(PreviewManualTimesText))] private JobViewModel? _previewJob;
    /// <summary>The previewed video has capture times chosen by hand (shows the reset button).</summary>
    public bool HasPreviewManualTimes => PreviewJob?.HasManualTimes == true;
    public string PreviewManualTimesText => Loc.T("ManualTimesReset", PreviewJob?.ManualTimeCount ?? 0);
    /// <summary>Tiles can be clicked to choose their time: real pictures of a queued video are shown.</summary>
    [ObservableProperty] private bool _canEditTiles;
    public string PreviewTitle => PreviewJob?.FileName ?? Loc.T("PreviewSampleName");
    [ObservableProperty] private string _previewNote = Loc.T("PreviewNote");
    /// <summary>Live preview on (default) or off; off stops rendering and every ffmpeg run for the preview.</summary>
    [ObservableProperty] private bool _livePreview = true;
    [ObservableProperty] private string _previewNoteTip = Loc.T("PreviewNoteTip");
    public string QueueTitle => Loc.T("QueueTitle", Jobs.Count);

    // ---- collapsible queue
    /// <summary>Below this window width the queue folds into a narrow strip by itself.</summary>
    public const double NarrowWindowWidth = 1120;
    [ObservableProperty] private bool _isQueueCollapsed;
    private bool _isNarrow;
    private bool _collapsedAutomatically;

    /// <summary>Folds or unfolds the queue by hand; that choice holds until the window crosses the width limit again.</summary>
    [RelayCommand]
    private void ToggleQueue()
    {
        IsQueueCollapsed = !IsQueueCollapsed;
        _collapsedAutomatically = false;
    }

    /// <summary>
    /// Called when the window width changes. Only crossing <see cref="NarrowWindowWidth"/> acts: getting narrow folds
    /// the queue, getting wide again unfolds it if it was folded automatically.
    /// </summary>
    public void OnWindowWidthChanged(double width)
    {
        var narrow = width < NarrowWindowWidth;
        if (narrow == _isNarrow) return;
        _isNarrow = narrow;
        if (narrow && !IsQueueCollapsed) { IsQueueCollapsed = true; _collapsedAutomatically = true; }
        else if (!narrow && _collapsedAutomatically) { IsQueueCollapsed = false; _collapsedAutomatically = false; }
    }

    public static Choice[] Languages { get; } = Choice.ForEnum<UiLanguage>("Lang");
    public static Choice[] Themes { get; } = Choice.ForEnum<UiTheme>("Theme");

    /// <summary>Show all settings; false = simple mode with only the basic ones (default).</summary>
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(IsSimple))] private bool _isAdvanced;
    [ObservableProperty] private UiTheme _theme;

    public bool IsSimple
    {
        get => !IsAdvanced;
        set => IsAdvanced = !value;
    }

    partial void OnIsAdvancedChanged(bool value) => _app.AdvancedMode = value;

    partial void OnThemeChanged(UiTheme value)
    {
        _app.Theme = value == UiTheme.System ? null : value.ToString();
        ApplyTheme(value);
    }

    internal static void ApplyTheme(UiTheme theme)
    {
        if (Application.Current is { } app)
            app.RequestedThemeVariant = theme switch
            {
                UiTheme.Light => ThemeVariant.Light,
                UiTheme.Dark => ThemeVariant.Dark,
                _ => ThemeVariant.Default,
            };
    }
    public bool IsIdle => !IsRunning;
    public string PauseButtonText => Loc.T(IsPaused ? "Resume" : "Pause");

    partial void OnLanguageChanged(UiLanguage value)
    {
        Loc.Instance.Language = value;
        _app.Language = value == UiLanguage.System ? null : value.ToString();
        OnPropertyChanged(nameof(PauseButtonText));
        foreach (var j in Jobs) j.Refresh();
        if (!IsRunning) StatusText = Loc.T("StatusIntro");
        OnPropertyChanged(nameof(PreviewTitle));
        OnPropertyChanged(nameof(QueueTitle));
        OnPropertyChanged(nameof(PreviewManualTimesText));
        UpdatePreviewTexts();
        SchedulePreview();
    }

    public MainViewModel(IUiServices ui)
    {
        _ui = ui;
        _app = AppSettings.Load();
        Language = Enum.TryParse<UiLanguage>(_app.Language, true, out var lang) ? lang : UiLanguage.System;
        Loc.Instance.Language = Language;
        IsAdvanced = _app.AdvancedMode;
        LivePreview = _app.LivePreview;
        Theme = Enum.TryParse<UiTheme>(_app.Theme, true, out var theme) ? theme : UiTheme.System;
        ApplyTheme(Theme);
        StatusText = Loc.T("StatusIntro");
        Ffmpeg = new FfmpegViewModel(ui, _app);
        Recursive = _app.Recursive;
        MaxParallelProcesses = _app.MaxParallelProcesses ?? BatchOptions.DefaultParallelism;
        MaxParallelVideos = _app.MaxParallelVideos ?? Math.Max(1, BatchOptions.DefaultParallelism / 2);
        ReloadPresets(_app.LastPreset);
        Settings.Load(_app.LastSettings ?? SelectedPreset ?? BuiltInPresets.Default);
        Jobs.CollectionChanged += (_, _) =>
        {
            UpdateStats();
            OnPropertyChanged(nameof(QueueTitle));
            if (PreviewJob is null || !Jobs.Contains(PreviewJob)) PreviewJob = Jobs.FirstOrDefault();
        };
        _clockTimer = new DispatcherTimer(TimeSpan.FromSeconds(1), DispatcherPriority.Background, (_, _) => UpdateStats());
        _previewTimer.Tick += (_, _) => { _previewTimer.Stop(); _ = UpdatePreviewAsync(); };
        Settings.PropertyChanged += (_, _) => SchedulePreview();
        Ffmpeg.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(FfmpegViewModel.IsReady)) SchedulePreview(); };
        RenderPreviewNow();
    }

    /// <summary>Called once the window is shown.</summary>
    public Task OnOpenedAsync() => Ffmpeg.StartupCheckAsync();

    public void HandleStartupArguments(string[] args)
    {
        var inputs = new List<string>();
        foreach (var a in args)
        {
            if (a.EndsWith(PresetStore.FileExtension, StringComparison.OrdinalIgnoreCase) && File.Exists(a))
            {
                try
                {
                    var p = PresetStore.LoadFile(a);
                    _presets.Save(p);
                    ReloadPresets(p.Name);
                }
                catch (Exception ex) { StatusText = Loc.T("StatusPresetLoadFailed", ex.Message); }
            }
            else inputs.Add(a);
        }
        if (inputs.Count > 0) AddPaths(inputs);
    }

    partial void OnSelectedPresetChanged(SheetSettings? value)
    {
        if (value is not null && !_suppressPresetLoad) Settings.Load(value);
    }
    private bool _suppressPresetLoad;

    private void ReloadPresets(string? select)
    {
        _suppressPresetLoad = true;
        Presets.Clear();
        foreach (var p in _presets.LoadAll()) Presets.Add(p);
        SelectedPreset = Presets.FirstOrDefault(p => string.Equals(p.Name, select, StringComparison.OrdinalIgnoreCase))
                         ?? Presets.FirstOrDefault(p => p.Name == "Standard") ?? Presets.FirstOrDefault();
        _suppressPresetLoad = false;
    }

    // ------------------------------------------------------------------ preview

    partial void OnPreviewJobChanged(JobViewModel? value) => SchedulePreview();

    partial void OnLivePreviewChanged(bool value)
    {
        _app.LivePreview = value;
        if (value) { SchedulePreview(); return; }
        // Off: stop everything that is running for the preview and free the image.
        _previewTimer.Stop();
        _previewGeneration++;
        _previewCts?.Cancel();
        var old = PreviewImage;
        PreviewImage = null;
        old?.Dispose();
    }

    /// <summary>The window got the focus again: a video may have changed on disk meanwhile, so check the preview.</summary>
    public void OnWindowActivated()
    {
        if (LivePreview && PreviewJob is not null && _previewSource?.ForgetIfChanged(PreviewJob.Job.InputPath) == true) SchedulePreview();
    }

    /// <summary>Re-renders the preview shortly after the last change (several changes in a row render once).</summary>
    private void SchedulePreview()
    {
        if (!LivePreview) return;
        _previewTimer.Stop();
        _previewTimer.Start();
    }

    private enum PreviewMode { Sample, NoFfmpeg, Unreadable, Real }

    private sealed record PreviewData(byte[] Png, int Width, int Height, int Columns, int Rows, int Tiles, int RealFrames,
                                      IReadOnlyList<TileRect> TileRects, PlannedTime[] Plan, Core.Media.MediaInfo Info, SheetSettings Settings, string? JobPath);

    private PreviewSource? _previewSource;
    private CancellationTokenSource? _previewCts;

    /// <summary>Real video data for the preview while ffmpeg is available; recreated when ffmpeg changes.</summary>
    private PreviewSource? CurrentPreviewSource()
    {
        var tools = Ffmpeg.Tools;
        if (tools is null) return _previewSource = null;
        if (_previewSource?.Tools != tools)
            _previewSource = new PreviewSource(tools, Math.Clamp(Environment.ProcessorCount / 2, 1, 4));
        return _previewSource;
    }

    private Core.Media.MediaInfo SampleFor(JobViewModel? job) => job is null
        ? SheetPreview.SampleInfo(Loc.T("PreviewSampleName"))
        : job.FileSize > 0 ? SheetPreview.SampleInfo(job.Job.InputPath, job.FileSize) : SheetPreview.SampleInfo(job.Job.InputPath);

    /// <summary>Video information for the preview: the real one when it can be read, otherwise sample values.</summary>
    private async Task<(Core.Media.MediaInfo Info, PreviewSource? Source, PreviewMode Mode)> ResolvePreviewInfoAsync(JobViewModel? job, CancellationToken ct)
    {
        if (job is null) return (SampleFor(null), null, PreviewMode.Sample);
        var source = CurrentPreviewSource();
        if (source is null) return (SampleFor(job), null, PreviewMode.NoFfmpeg);
        try
        {
            var info = await source.GetInfoAsync(job.Job.InputPath, ct);
            return info.Video is null ? (SampleFor(job), null, PreviewMode.Unreadable) : (info, source, PreviewMode.Real);
        }
        catch (OperationCanceledException) { throw; }
        catch { return (SampleFor(job), null, PreviewMode.Unreadable); }
    }

    /// <summary>Black frames are skipped in the preview as on the sheet (window 0 = off).</summary>
    private static double BlackSearch(SheetSettings s)
    {
        var n = s.Clone();
        n.Normalize();
        return n.SkipBlackFrames ? n.BlackFrameSearchSeconds : 0;
    }

    private static PreviewData BuildPreview(SheetSettings s, Core.Media.MediaInfo info, PreviewSource? source, IReadOnlyList<ManualTime?>? manual, string? jobPath)
    {
        var search = BlackSearch(s);
        using var result = SheetPreview.Render(s, info, maxWidth: 1200, source is null ? null : p => source.TryGetFrame(info.Path, p, search), manual);
        using var ms = new MemoryStream();
        SheetRenderer.Encode(result.Bitmap, ms, ImageFormat.Png, 100);
        var shown = s.Clone();
        shown.Normalize();
        return new PreviewData(ms.ToArray(), result.SheetWidth, result.SheetHeight, shown.Columns, shown.Rows, result.Tiles, result.RealFrames,
                               result.TileRects ?? [], SheetPreview.PlanTimes(s, info, manual), info, s, jobPath);
    }

    private PreviewData? _lastPreview;
    private PreviewMode _lastPreviewMode = PreviewMode.Sample;

    private void ApplyPreview(PreviewData d, PreviewMode mode)
    {
        _lastPreview = d;
        _lastPreviewMode = mode;
        var old = PreviewImage;
        PreviewImage = new Bitmap(new MemoryStream(d.Png));
        old?.Dispose();
        UpdatePreviewTexts();
    }

    /// <summary>Caption under the sheet and the note above it (where the pictures come from, loading progress).</summary>
    private void UpdatePreviewTexts()
    {
        if (_lastPreview is not { } d) return;
        PreviewCaption = d.Settings.Template is { IsValid: true } t
            ? Loc.T("PreviewCaptionTemplate", BuiltInTemplates.IsBuiltIn(t) ? Loc.T("Layout." + t.Name) : t.Name, d.Tiles, d.Width, d.Height)
            : Loc.T("PreviewCaption", d.Columns, d.Rows, d.Tiles, d.Width, d.Height);
        PreviewNote = _lastPreviewMode switch
        {
            PreviewMode.Real when d.RealFrames >= d.Tiles => Loc.T("PreviewNoteReal"),
            PreviewMode.Real => Loc.T("PreviewNoteLoading", d.RealFrames, d.Tiles),
            PreviewMode.NoFfmpeg => Loc.T("PreviewNoteNoFfmpeg"),
            PreviewMode.Unreadable => Loc.T("PreviewNoteUnreadable"),
            _ => Loc.T("PreviewNote"),
        };
        PreviewNoteTip = Loc.T(_lastPreviewMode == PreviewMode.Real ? "PreviewNoteRealTip" : "PreviewNoteTip");
        CanEditTiles = _lastPreviewMode == PreviewMode.Real && d.JobPath is not null;
    }

    /// <summary>Renders the preview synchronously from what is known right now (start-up and tests).</summary>
    public void RenderPreviewNow()
    {
        if (!LivePreview) return;
        _previewGeneration++;
        try
        {
            var job = PreviewJob;
            var source = CurrentPreviewSource();
            var manual = job?.Job.ManualTimes;
            if (job is not null && source?.TryGetInfo(job.Job.InputPath) is { Video: not null } known)
                ApplyPreview(BuildPreview(CurrentSettings(), known, source, manual, job.Job.InputPath), PreviewMode.Real);
            else
                ApplyPreview(BuildPreview(CurrentSettings(), SampleFor(job), null, manual, job?.Job.InputPath),
                    job is null ? PreviewMode.Sample : source is null ? PreviewMode.NoFfmpeg : PreviewMode.Sample);
        }
        catch (Exception ex) { PreviewCaption = Loc.T("PreviewFailed", ex.Message); }
    }

    /// <summary>
    /// Renders the preview: first at once with what is cached, then, for a real video, again every 300 ms while
    /// missing frames are extracted in the background. A newer change cancels an older run.
    /// </summary>
    public async Task UpdatePreviewAsync()
    {
        _previewTimer.Stop(); // this run covers any change that was still waiting
        if (!LivePreview) return;
        var generation = ++_previewGeneration;
        _previewCts?.Cancel();
        var cts = _previewCts = new CancellationTokenSource();
        var ct = cts.Token;
        var s = CurrentSettings();
        var job = PreviewJob;
        var manual = job?.Job.ManualTimes;
        var jobPath = job?.Job.InputPath;
        try
        {
            var (info, source, mode) = await ResolvePreviewInfoAsync(job, ct);
            if (generation != _previewGeneration) return;
            var data = await Task.Run(() => BuildPreview(s, info, source, manual, jobPath), ct);
            if (generation != _previewGeneration) return;
            ApplyPreview(data, mode);
            if (source is null || data.RealFrames >= data.Tiles) return;

            var times = SheetPreview.PlanTimes(s, info, manual);
            var search = BlackSearch(s);
            var fresh = 0;
            var load = Task.Run(() => source.LoadFramesAsync(info, times, search, () => Interlocked.Exchange(ref fresh, 1), ct), ct);
            while (true)
            {
                await Task.WhenAny(load, Task.Delay(300, ct));
                var finished = load.IsCompleted;
                if (generation != _previewGeneration) return;
                if (Interlocked.Exchange(ref fresh, 0) == 1 || finished)
                {
                    data = await Task.Run(() => BuildPreview(s, info, source, manual, jobPath), ct);
                    if (generation != _previewGeneration) return;
                    ApplyPreview(data, mode);
                }
                if (finished) break;
            }
            await load; // surfaces extraction errors
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            if (generation == _previewGeneration) PreviewCaption = Loc.T("PreviewFailed", ex.Message);
        }
    }

    // ------------------------------------------------------------------ capture times by hand

    /// <summary>Tile (timeline index) under a point of the preview image, given as fractions (0..1) of its size; -1 if none.</summary>
    public int TileIndexAt(double fx, double fy)
    {
        if (_lastPreview is not { } d || d.Width <= 0 || d.Height <= 0) return -1;
        var x = fx * d.Width;
        var y = fy * d.Height;
        for (var i = 0; i < d.TileRects.Count; i++)
        {
            var r = d.TileRects[i];
            if (x >= r.X && x < r.X + r.Width && y >= r.Y && y < r.Y + r.Height) return i;
        }
        return -1;
    }

    /// <summary>Click on the preview image: opens the time dialog for the tile under the pointer.</summary>
    public async Task EditTileAtAsync(double fx, double fy)
    {
        var index = TileIndexAt(fx, fy);
        if (index >= 0) await EditTileAsync(index);
    }

    /// <summary>
    /// Lets the user choose the capture time of one tile of the previewed video. While the dialog is open every
    /// change is shown in the preview; Cancel restores the times from before.
    /// </summary>
    public async Task EditTileAsync(int index)
    {
        if (_lastPreview is not { } d || !CanEditTiles || PreviewJob is not { } job || d.JobPath != job.Job.InputPath) { StatusText = Loc.T("PreviewClickNeedsVideo"); return; }
        if (index < 0 || index >= d.Plan.Length) return;
        var before = job.Job.ManualTimes;
        var planned = d.Plan[index];
        var current = before is not null && index < before.Count ? before[index] : null;
        // Start from the time the tile shows now (after a black frame was skipped, that is the time actually used).
        var shown = _previewSource?.TryGetFrame(d.Info.Path, planned, BlackSearch(d.Settings))?.Time ?? planned.Time;
        var auto = SheetPreview.PlanTimes(d.Settings, d.Info)[index].Time;
        var anim = d.Settings.Animation;
        var editor = new TileTimeViewModel(index, d.Info.Duration, d.Info.Video?.FrameRate ?? 0, auto,
                                           current ?? new ManualTime(shown), anim.Enabled, anim.ClipSeconds);
        editor.Changed += t => SetManualTime(job, index, t);
        var result = await _ui.EditTileTimeAsync(editor);
        switch (result)
        {
            case TileTimeResult.Ok when editor.Current is { } t: SetManualTime(job, index, t); break;
            case TileTimeResult.Automatic: SetManualTime(job, index, null); break;
            default: SetManualTimes(job, before); break;
        }
    }

    /// <summary>Sets (or with null clears) the manual time of one tile of a video.</summary>
    public void SetManualTime(JobViewModel job, int index, ManualTime? time)
    {
        var list = (job.Job.ManualTimes ?? []).ToList();
        while (list.Count <= index) list.Add(null);
        list[index] = time;
        SetManualTimes(job, list);
    }

    private void SetManualTimes(JobViewModel job, IReadOnlyList<ManualTime?>? times)
    {
        var list = times?.ToList() ?? [];
        while (list.Count > 0 && list[^1] is null) list.RemoveAt(list.Count - 1);
        job.Job.ManualTimes = list.Count == 0 ? null : list.ToArray();
        job.RefreshManualTimes();
        if (job == PreviewJob)
        {
            OnPropertyChanged(nameof(HasPreviewManualTimes));
            OnPropertyChanged(nameof(PreviewManualTimesText));
            SchedulePreview();
        }
    }

    /// <summary>All capture times of the previewed video back to automatic.</summary>
    [RelayCommand]
    private void ResetManualTimes()
    {
        if (PreviewJob is { } job) SetManualTimes(job, null);
    }

    // ------------------------------------------------------------------ queue

    /// <summary>Asks for one or more web addresses (separated by spaces or lines) and queues them.</summary>
    [RelayCommand]
    private async Task AddUrlAsync()
    {
        var text = await _ui.PromptTextAsync(Loc.T("DlgAddUrl"), Loc.T("DlgAddUrlMessage"), "https://");
        if (string.IsNullOrWhiteSpace(text)) return;
        AddUrls(text);
    }

    /// <summary>Queues every http(s) address in <paramref name="text"/>; reports the first entry that is not one.</summary>
    public void AddUrls(string text)
    {
        var parts = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Where(p => p != "https://" && p != "http://").ToList();
        var urls = parts.Where(VideoSource.IsUrl).ToList();
        if (urls.Count > 0) AddPaths(urls);
        if (parts.FirstOrDefault(p => !VideoSource.IsUrl(p)) is { } bad) StatusText = Loc.T("StatusInvalidUrl", bad);
    }

    public void AddPaths(IEnumerable<string> paths)
    {
        var existing = Jobs.Select(j => j.Job.InputPath).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var added = 0;
        foreach (var v in VideoScanner.Scan(paths, Recursive))
        {
            if (!existing.Add(v.Path)) continue;
            var job = new ThumbnailJob(v.Path, v.Root);
            var vm = new JobViewModel(job);
            job.Changed += _ => Dispatcher.UIThread.Post(() => { vm.Refresh(); UpdateStats(); });
            Jobs.Add(vm);
            added++;
        }
        StatusText = added == 0 ? Loc.T("StatusNoNewVideos") : Loc.T("StatusAdded", added);
        StartCommand.NotifyCanExecuteChanged();
        ClearAllCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private async Task AddFilesAsync() => AddPaths(await _ui.PickVideoFilesAsync());

    [RelayCommand]
    private async Task PickFontAsync()
    {
        var family = await _ui.PickFontAsync(Settings.FontFamily);
        if (family is not null) Settings.FontFamily = family.Length == 0 ? null : family;
    }

    [RelayCommand]
    private async Task PickBackgroundImageAsync()
    {
        var file = await _ui.PickImageFileAsync(Settings.BackgroundImagePath, "BackgroundImagePick");
        if (file is not null) Settings.BackgroundImagePath = file;
    }

    [RelayCommand]
    private void ClearBackgroundImage() => Settings.BackgroundImagePath = null;

    [RelayCommand]
    private async Task PickWatermarkImageAsync()
    {
        var file = await _ui.PickImageFileAsync(Settings.WatermarkImagePath, "WatermarkImagePick");
        if (file is not null) Settings.WatermarkImagePath = file;
    }

    [RelayCommand]
    private void ClearWatermarkImage() => Settings.WatermarkImagePath = null;

    /// <summary>
    /// The window moved to another screen or the screen changed: "fit to screen" sheets follow it. The size is also
    /// remembered for the command line.
    /// </summary>
    public void OnScreenChanged(ScreenSize? screen)
    {
        if (screen is null || screen == ScreenFitting.Current) return;
        ScreenFitting.Current = screen;
        _app.LastScreen = screen;
        if (Settings.FitToScreen != ScreenFit.Off) SchedulePreview();
    }

    [RelayCommand]
    private async Task AddFolderAsync()
    {
        var folder = await _ui.PickFolderAsync(Loc.T("DlgAddFolder"));
        if (folder is null) return;
        _app.LastInputFolder = folder;
        AddPaths([folder]);
    }

    [RelayCommand(CanExecute = nameof(IsIdle))]
    private void RemoveSelected()
    {
        foreach (var j in SelectedJobs.ToList()) Jobs.Remove(j);
    }

    [RelayCommand]
    private void ClearFinished()
    {
        foreach (var j in Jobs.Where(j => j.IsFinished && j.Status != JobStatus.Running).ToList()) Jobs.Remove(j);
    }

    [RelayCommand(CanExecute = nameof(IsIdle))]
    private void ClearAll() => Jobs.Clear();

    [RelayCommand]
    private void Sort(string key)
    {
        IEnumerable<JobViewModel> sorted = key switch
        {
            "size" => Jobs.OrderByDescending(j => j.FileSize),
            "status" => Jobs.OrderBy(j => j.Status).ThenBy(j => j.FileName, StringComparer.OrdinalIgnoreCase),
            "folder" => Jobs.OrderBy(j => j.Folder, StringComparer.OrdinalIgnoreCase).ThenBy(j => j.FileName, StringComparer.OrdinalIgnoreCase),
            _ => Jobs.OrderBy(j => j.FileName, StringComparer.OrdinalIgnoreCase),
        };
        var list = sorted.ToList();
        Jobs.Clear();
        foreach (var j in list) Jobs.Add(j);
    }

    [RelayCommand]
    private void RetryFailed()
    {
        foreach (var j in Jobs.Where(j => j.Status is JobStatus.Failed or JobStatus.Cancelled)) j.Job.Reset();
        StartCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private void OpenOutput(JobViewModel? job)
    {
        if (job?.OutputPath is { } p && File.Exists(p)) _ui.OpenPath(p);
        else if (job is not null) _ui.OpenPath(job.Job.InputPath);
    }

    // ------------------------------------------------------------------ run

    private bool CanStart() => !IsRunning && Jobs.Count > 0;

    [RelayCommand(CanExecute = nameof(CanStart))]
    private async Task StartAsync()
    {
        var settings = CurrentSettings();
        var problems = settings.Clone().Normalize();
        if (problems.Count > 0) { await _ui.ShowMessageAsync(Loc.T("DlgCheckSettings"), string.Join("\n", problems)); return; }

        var tools = await Ffmpeg.EnsureAsync();
        if (tools is null) return;

        SaveAppSettings();
        foreach (var j in Jobs.Where(j => j.Status is JobStatus.Cancelled)) j.Job.Reset();
        var jobs = Jobs.Select(j => j.Job).Where(j => j.Status == JobStatus.Pending).ToList();
        if (jobs.Count == 0) { StatusText = Loc.T("StatusNothingToDo"); return; }

        _processor = new BatchProcessor(tools);
        _cts = new CancellationTokenSource();
        IsRunning = true; IsPaused = false;
        _runClock.Restart();
        _clockTimer.Start();
        try
        {
            var options = new BatchOptions { MaxParallelProcesses = (int)MaxParallelProcesses, MaxParallelVideos = (int)MaxParallelVideos };
            var stats = await Task.Run(() => _processor.RunAsync(jobs, settings, options, _cts.Token));
            var elapsed = Formatting.Duration(stats.Elapsed.TotalSeconds);
            StatusText = stats.Cancelled > 0
                ? Loc.T("StatusFinishedCancelled", elapsed, stats.Done, stats.Skipped, stats.Failed, stats.Cancelled)
                : Loc.T("StatusFinished", elapsed, stats.Done, stats.Skipped, stats.Failed);
        }
        finally
        {
            _runClock.Stop();
            _clockTimer.Stop();
            IsRunning = false; IsPaused = false;
            _processor = null;
            _cts.Dispose(); _cts = null;
            StartCommand.NotifyCanExecuteChanged();
        }
    }

    [RelayCommand(CanExecute = nameof(IsRunning))]
    private void Pause()
    {
        if (_processor is null) return;
        if (IsPaused) { _processor.Pause.Resume(); IsPaused = false; _runClock.Start(); }
        else { _processor.Pause.Pause(); IsPaused = true; _runClock.Stop(); }
        UpdateStats();
    }

    [RelayCommand(CanExecute = nameof(IsRunning))]
    private void Cancel()
    {
        _processor?.Pause.Resume();
        _cts?.Cancel();
        StatusText = Loc.T("StatusCancelling");
    }

    private void UpdateStats()
    {
        var total = Jobs.Count;
        if (total == 0) { OverallProgress = 0; return; }
        OverallProgress = Jobs.Sum(j => j.IsFinished ? 1 : j.Progress) / total * 100;
        if (!IsRunning) return;
        var done = Jobs.Count(j => j.Status == JobStatus.Done);
        var failed = Jobs.Count(j => j.Status == JobStatus.Failed);
        var skipped = Jobs.Count(j => j.Status == JobStatus.Skipped);
        var running = Jobs.Count(j => j.Status == JobStatus.Running);
        StatusText = Loc.T("StatusProgress", Loc.T(IsPaused ? "StatusPaused" : "StatusRunning"), done + failed + skipped, total,
            done, skipped, failed, running, Formatting.Duration(_runClock.Elapsed.TotalSeconds));
    }

    // ------------------------------------------------------------------ presets

    private SheetSettings CurrentSettings() => Settings.ToSettings(SelectedPreset?.Name ?? "Custom");

    [RelayCommand]
    private async Task SavePresetAsync()
    {
        var initial = SelectedPreset is { } p && !PresetStore.IsBuiltIn(p.Name) ? p.Name : Loc.T("DlgPresetDefaultName");
        var name = await _ui.PromptTextAsync(Loc.T("DlgSavePreset"), Loc.T("DlgPresetName"), initial);
        if (string.IsNullOrWhiteSpace(name)) return;
        if (PresetStore.IsBuiltIn(name)) { await _ui.ShowMessageAsync(Loc.T("DlgSavePreset"), Loc.T("DlgBuiltInNoOverwrite")); return; }
        var s = Settings.ToSettings(name.Trim());
        s.Description = null;
        _presets.Save(s);
        ReloadPresets(s.Name);
        StatusText = Loc.T("StatusPresetSaved", s.Name);
    }

    [RelayCommand]
    private async Task DeletePresetAsync()
    {
        if (SelectedPreset is not { } p) return;
        if (PresetStore.IsBuiltIn(p.Name)) { await _ui.ShowMessageAsync(Loc.T("DlgDeletePreset"), Loc.T("DlgBuiltInNoDelete")); return; }
        if (!await _ui.ConfirmAsync(Loc.T("DlgDeletePreset"), Loc.T("DlgDeletePresetQuestion", p.Name))) return;
        _presets.Delete(p.Name);
        ReloadPresets(null);
        if (SelectedPreset is { } sel) Settings.Load(sel);
    }

    [RelayCommand]
    private async Task ImportPresetAsync()
    {
        var file = await _ui.PickPresetFileAsync(save: false);
        if (file is null) return;
        try
        {
            var p = PresetStore.LoadFile(file);
            _presets.Save(p);
            ReloadPresets(p.Name);
            if (SelectedPreset is { } sel) Settings.Load(sel);
        }
        catch (Exception ex) { await _ui.ShowMessageAsync(Loc.T("DlgImportPreset"), Loc.T("DlgImportFailed", ex.Message)); }
    }

    [RelayCommand]
    private async Task ExportPresetAsync()
    {
        var s = CurrentSettings();
        var file = await _ui.PickPresetFileAsync(save: true, PresetStore.SafeFileNameForExport(s.Name));
        if (file is null) return;
        PresetStore.ExportFile(s, file);
        StatusText = Loc.T("StatusPresetExported", file);
    }

    [RelayCommand]
    private void ResetToPreset()
    {
        if (SelectedPreset is { } p) Settings.Load(p);
    }

    [RelayCommand]
    private async Task BrowseOutputFolderAsync()
    {
        var folder = await _ui.PickFolderAsync(Loc.T("DlgOutputFolder"));
        if (folder is not null) Settings.OutputFolder = folder;
    }

    public void SaveAppSettings()
    {
        _app.LastSettings = CurrentSettings();
        _app.LastPreset = SelectedPreset?.Name;
        _app.Recursive = Recursive;
        _app.MaxParallelProcesses = (int)MaxParallelProcesses;
        _app.MaxParallelVideos = (int)MaxParallelVideos;
        try { _app.Save(); } catch { /* not critical */ }
    }

    public void OnClosing()
    {
        _cts?.Cancel();
        SaveAppSettings();
    }
}
