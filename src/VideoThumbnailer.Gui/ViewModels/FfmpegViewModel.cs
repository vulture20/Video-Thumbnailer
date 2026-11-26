using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VideoThumbnailer.Core.Ffmpeg;
using VideoThumbnailer.Core.Settings;
using VideoThumbnailer.Core.Util;
using VideoThumbnailer.Gui.Localization;

namespace VideoThumbnailer.Gui.ViewModels;

/// <summary>ffmpeg status, first-start download question and update handling.</summary>
public partial class FfmpegViewModel : ObservableObject
{
    private static readonly TimeSpan UpdateCheckInterval = TimeSpan.FromDays(7);
    private readonly IUiServices _ui;
    private readonly AppSettings _app;
    private readonly FfmpegInstaller? _installer = FfmpegInstaller.ForCurrentPlatform();
    private FfmpegRelease? _latest;

    [ObservableProperty] private string _statusLine = Loc.T("FfmpegChecking");
    [ObservableProperty] private string? _detailLine;
    [ObservableProperty] [NotifyCanExecuteChangedFor(nameof(InstallOrUpdateCommand), nameof(CheckForUpdateCommand))] private bool _isBusy;
    [ObservableProperty] private double _downloadPercent;
    [ObservableProperty] private bool _updateAvailable;
    [ObservableProperty] private string? _customDirectory;
    [ObservableProperty] private bool _checkUpdatesAutomatically;

    public FfmpegTools? Tools { get; private set; }
    public bool CanDownload => _installer is not null;
    public string SourceName => _installer?.Source.Name ?? Loc.T("FfmpegNoSource");
    public string SourceLine => Loc.T("FfmpegSource", SourceName);
    public string InstallButtonText => Loc.T(UpdateAvailable ? "FfmpegInstallUpdate" : Tools?.Origin == FfmpegOrigin.Managed ? "FfmpegReinstall" : "FfmpegDownload");

    public FfmpegViewModel(IUiServices ui, AppSettings app)
    {
        _ui = ui;
        _app = app;
        CustomDirectory = app.FfmpegDirectory;
        CheckUpdatesAutomatically = app.CheckFfmpegUpdates;
        Refresh();
        Loc.Instance.LanguageChanged += () =>
        {
            if (IsBusy) return;
            Refresh();
            OnPropertyChanged(nameof(SourceName));
            OnPropertyChanged(nameof(SourceLine));
        };
    }

    partial void OnCustomDirectoryChanged(string? value)
    {
        _app.FfmpegDirectory = string.IsNullOrWhiteSpace(value) ? null : value;
        Refresh();
    }

    partial void OnCheckUpdatesAutomaticallyChanged(bool value) => _app.CheckFfmpegUpdates = value;
    partial void OnUpdateAvailableChanged(bool value) => OnPropertyChanged(nameof(InstallButtonText));

    public void Refresh()
    {
        Tools = FfmpegLocator.Locate(_app.FfmpegDirectory);
        if (Tools is null)
        {
            StatusLine = Loc.T("FfmpegNotFound");
            DetailLine = Loc.T(CanDownload ? "FfmpegNotFoundAuto" : "FfmpegNotFoundManual");
        }
        else
        {
            StatusLine = Tools.Origin switch
            {
                FfmpegOrigin.Managed => Loc.T("FfmpegManaged"),
                FfmpegOrigin.Custom => Loc.T("FfmpegCustom"),
                _ => Loc.T("FfmpegSystem"),
            };
            var manifest = _installer?.ReadManifest();
            DetailLine = Tools.Origin == FfmpegOrigin.Managed && manifest is not null
                ? Loc.T("FfmpegBuildInfo", manifest.Version, manifest.Source, manifest.InstalledAt.LocalDateTime.ToString("d"))
                : Tools.FfmpegPath;
        }
        OnPropertyChanged(nameof(InstallButtonText));
    }

    /// <summary>First start: ask once whether ffmpeg should be downloaded; afterwards check for updates weekly.</summary>
    public async Task StartupCheckAsync()
    {
        if (Tools is null && CanDownload && !_app.FfmpegDownloadAsked)
        {
            _app.FfmpegDownloadAsked = true;
            SaveQuietly();
            await EnsureAsync();
            return;
        }
        if (Tools?.Origin == FfmpegOrigin.Managed && _app.CheckFfmpegUpdates &&
            (_app.LastFfmpegUpdateCheck is null || DateTimeOffset.UtcNow - _app.LastFfmpegUpdateCheck > UpdateCheckInterval))
        {
            await CheckForUpdateAsync(silent: true);
        }
    }

    /// <summary>Returns usable tools, asking to download ffmpeg if it is missing.</summary>
    public async Task<FfmpegTools?> EnsureAsync()
    {
        Refresh();
        if (Tools is not null) return Tools;
        if (!CanDownload)
        {
            await _ui.ShowMessageAsync(Loc.T("DlgFfmpegMissingTitle"), Loc.T("DlgFfmpegMissingManual"));
            return null;
        }
        var ok = await _ui.ConfirmAsync(Loc.T("DlgFfmpegNeededTitle"),
            Loc.T("DlgFfmpegNeeded", _installer!.Source.Homepage, _installer.InstallRoot),
            Loc.T("DlgDownload"), Loc.T("DlgNotNow"));
        if (!ok) return null;
        await InstallAsync(null);
        return Tools;
    }

    [RelayCommand(CanExecute = nameof(CanRunInstaller))]
    private Task InstallOrUpdate() => InstallAsync(_latest);

    [RelayCommand(CanExecute = nameof(CanRunInstaller))]
    private Task CheckForUpdate() => CheckForUpdateAsync(silent: false);

    private bool CanRunInstaller() => !IsBusy && CanDownload;

    [RelayCommand]
    private async Task BrowseCustomDirectoryAsync()
    {
        var dir = await _ui.PickFolderAsync(Loc.T("DlgFfmpegFolderTitle"));
        if (dir is null) return;
        if (FfmpegLocator.FromDirectory(dir, FfmpegOrigin.Custom) is null)
        {
            await _ui.ShowMessageAsync(Loc.T("DlgFfmpegFolder"), Loc.T("DlgFfmpegFolderMissing", FfmpegLocator.ExeName("ffmpeg"), FfmpegLocator.ExeName("ffprobe")));
            return;
        }
        CustomDirectory = dir;
    }

    [RelayCommand]
    private void ClearCustomDirectory() => CustomDirectory = null;

    private async Task CheckForUpdateAsync(bool silent)
    {
        if (_installer is null) return;
        IsBusy = true;
        try
        {
            var info = await Task.Run(() => _installer.CheckForUpdateAsync());
            _latest = info.Latest;
            _app.LastFfmpegUpdateCheck = DateTimeOffset.UtcNow;
            SaveQuietly();
            UpdateAvailable = info.UpdateAvailable && Tools?.Origin == FfmpegOrigin.Managed;
            if (UpdateAvailable) DetailLine = Loc.T("FfmpegUpdateAvailable", info.Latest.Version, info.Installed?.Version ?? Loc.T("Unknown"));
            else if (!silent) await _ui.ShowMessageAsync("ffmpeg", Tools?.Origin == FfmpegOrigin.Managed
                ? Loc.T("DlgFfmpegUpToDate", info.Latest.Version)
                : Loc.T("DlgFfmpegNotManaged", info.Latest.Version));
        }
        catch (Exception ex)
        {
            if (!silent) await _ui.ShowMessageAsync("ffmpeg", Loc.T("DlgFfmpegCheckFailed", ex.Message));
        }
        finally { IsBusy = false; }
    }

    private async Task InstallAsync(FfmpegRelease? release)
    {
        if (_installer is null) return;
        IsBusy = true;
        DownloadPercent = 0;
        try
        {
            var progress = new Progress<DownloadProgress>(p =>
            {
                if (p.TotalBytes is > 0) DownloadPercent = 100.0 * p.BytesReceived / p.TotalBytes.Value;
                StatusLine = p.TotalBytes is > 0
                    ? Loc.T("FfmpegDownloadingOf", p.File, Formatting.Size(p.BytesReceived), Formatting.Size(p.TotalBytes.Value))
                    : Loc.T("FfmpegDownloading", p.File, Formatting.Size(p.BytesReceived));
            });
            await Task.Run(() => _installer.InstallAsync(release, progress));
            UpdateAvailable = false;
            Refresh();
        }
        catch (Exception ex)
        {
            Refresh();
            await _ui.ShowMessageAsync(Loc.T("DlgFfmpegDownloadFailed"), ex.Message);
        }
        finally
        {
            IsBusy = false;
            DownloadPercent = 0;
        }
    }

    private void SaveQuietly()
    {
        try { _app.Save(); } catch { /* ignore */ }
    }
}
