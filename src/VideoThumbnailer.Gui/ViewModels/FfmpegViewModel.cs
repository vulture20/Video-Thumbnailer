using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VideoThumbnailer.Core.Ffmpeg;
using VideoThumbnailer.Core.Settings;
using VideoThumbnailer.Core.Util;

namespace VideoThumbnailer.Gui.ViewModels;

/// <summary>ffmpeg status, first-start download question and update handling.</summary>
public partial class FfmpegViewModel : ObservableObject
{
    private static readonly TimeSpan UpdateCheckInterval = TimeSpan.FromDays(7);
    private readonly IUiServices _ui;
    private readonly AppSettings _app;
    private readonly FfmpegInstaller? _installer = FfmpegInstaller.ForCurrentPlatform();
    private FfmpegRelease? _latest;

    [ObservableProperty] private string _statusLine = "Checking ffmpeg…";
    [ObservableProperty] private string? _detailLine;
    [ObservableProperty] [NotifyCanExecuteChangedFor(nameof(InstallOrUpdateCommand), nameof(CheckForUpdateCommand))] private bool _isBusy;
    [ObservableProperty] private double _downloadPercent;
    [ObservableProperty] private bool _updateAvailable;
    [ObservableProperty] private string? _customDirectory;
    [ObservableProperty] private bool _checkUpdatesAutomatically;

    public FfmpegTools? Tools { get; private set; }
    public bool CanDownload => _installer is not null;
    public string SourceName => _installer?.Source.Name ?? "no automatic download for this platform";
    public string InstallButtonText => UpdateAvailable ? "Install update" : Tools?.Origin == FfmpegOrigin.Managed ? "Reinstall" : "Download ffmpeg";

    public FfmpegViewModel(IUiServices ui, AppSettings app)
    {
        _ui = ui;
        _app = app;
        CustomDirectory = app.FfmpegDirectory;
        CheckUpdatesAutomatically = app.CheckFfmpegUpdates;
        Refresh();
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
            StatusLine = "ffmpeg not found";
            DetailLine = CanDownload ? "It is downloaded automatically when needed (you will be asked first)." : "Please install ffmpeg and ffprobe or choose their folder.";
        }
        else
        {
            StatusLine = Tools.Origin switch
            {
                FfmpegOrigin.Managed => "ffmpeg: downloaded by Video-Thumbnailer",
                FfmpegOrigin.Custom => "ffmpeg: custom folder",
                _ => "ffmpeg: system installation (PATH)",
            };
            var manifest = _installer?.ReadManifest();
            DetailLine = Tools.Origin == FfmpegOrigin.Managed && manifest is not null
                ? $"Build {manifest.Version} from {manifest.Source}, installed {manifest.InstalledAt.LocalDateTime:d}"
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
            await _ui.ShowMessageAsync("ffmpeg missing",
                "Video-Thumbnailer needs ffmpeg and ffprobe. Please install them (e.g. with your package manager) or choose their folder in the settings.");
            return null;
        }
        var ok = await _ui.ConfirmAsync("ffmpeg needed",
            "Video-Thumbnailer uses ffmpeg to read videos. ffmpeg is not installed yet.\n\n" +
            $"Download it now from {_installer!.Source.Homepage}?\n" +
            $"Size about 100–150 MB. It is stored in:\n{_installer.InstallRoot}\n\nThe download is verified with SHA-256.",
            "Download", "Not now");
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
        var dir = await _ui.PickFolderAsync("Folder containing ffmpeg and ffprobe");
        if (dir is null) return;
        if (FfmpegLocator.FromDirectory(dir, FfmpegOrigin.Custom) is null)
        {
            await _ui.ShowMessageAsync("ffmpeg folder", $"{FfmpegLocator.ExeName("ffmpeg")} and {FfmpegLocator.ExeName("ffprobe")} were not found in this folder.");
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
            if (UpdateAvailable) DetailLine = $"Update available: build {info.Latest.Version} (installed: {info.Installed?.Version ?? "unknown"})";
            else if (!silent) await _ui.ShowMessageAsync("ffmpeg", Tools?.Origin == FfmpegOrigin.Managed
                ? $"ffmpeg is up to date (build {info.Latest.Version})."
                : $"Latest available build: {info.Latest.Version}. Your current ffmpeg is not managed by Video-Thumbnailer.");
        }
        catch (Exception ex)
        {
            if (!silent) await _ui.ShowMessageAsync("ffmpeg", "Could not check for updates: " + ex.Message);
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
                StatusLine = $"Downloading {p.File}: {Formatting.Size(p.BytesReceived)}" + (p.TotalBytes is > 0 ? $" of {Formatting.Size(p.TotalBytes.Value)}" : "");
            });
            await Task.Run(() => _installer.InstallAsync(release, progress));
            UpdateAvailable = false;
            Refresh();
        }
        catch (Exception ex)
        {
            Refresh();
            await _ui.ShowMessageAsync("ffmpeg download failed", ex.Message);
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
