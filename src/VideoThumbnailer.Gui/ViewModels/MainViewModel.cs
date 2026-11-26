using System.Collections.ObjectModel;
using System.Diagnostics;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VideoThumbnailer.Core.Batch;
using VideoThumbnailer.Core.Ffmpeg;
using VideoThumbnailer.Core.Input;
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

    public static Choice[] Languages { get; } = Choice.ForEnum<UiLanguage>("Lang");
    public bool IsIdle => !IsRunning;
    public string PauseButtonText => Loc.T(IsPaused ? "Resume" : "Pause");

    partial void OnLanguageChanged(UiLanguage value)
    {
        Loc.Instance.Language = value;
        _app.Language = value == UiLanguage.System ? null : value.ToString();
        OnPropertyChanged(nameof(PauseButtonText));
        foreach (var j in Jobs) j.Refresh();
        if (!IsRunning) StatusText = Loc.T("StatusIntro");
    }

    public MainViewModel(IUiServices ui)
    {
        _ui = ui;
        _app = AppSettings.Load();
        Language = Enum.TryParse<UiLanguage>(_app.Language, true, out var lang) ? lang : UiLanguage.System;
        Loc.Instance.Language = Language;
        StatusText = Loc.T("StatusIntro");
        Ffmpeg = new FfmpegViewModel(ui, _app);
        Recursive = _app.Recursive;
        MaxParallelProcesses = _app.MaxParallelProcesses ?? BatchOptions.DefaultParallelism;
        MaxParallelVideos = _app.MaxParallelVideos ?? Math.Max(1, BatchOptions.DefaultParallelism / 2);
        ReloadPresets(_app.LastPreset);
        Settings.Load(_app.LastSettings ?? SelectedPreset ?? BuiltInPresets.Default);
        Jobs.CollectionChanged += (_, _) => UpdateStats();
        _clockTimer = new DispatcherTimer(TimeSpan.FromSeconds(1), DispatcherPriority.Background, (_, _) => UpdateStats());
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

    // ------------------------------------------------------------------ queue

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
        PresetStore.SaveFile(s, file);
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
