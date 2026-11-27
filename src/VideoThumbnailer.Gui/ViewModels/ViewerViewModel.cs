using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using SkiaSharp;
using VideoThumbnailer.Core.Ffmpeg;
using VideoThumbnailer.Core.Input;
using VideoThumbnailer.Core.Output;
using VideoThumbnailer.Core.Rendering;
using VideoThumbnailer.Core.Settings;
using VideoThumbnailer.Core.Util;
using VideoThumbnailer.Core.Viewer;
using VideoThumbnailer.Gui.Localization;

namespace VideoThumbnailer.Gui.ViewModels;

/// <summary>
/// Sheet viewer (#39): shows a sheet with its sidecar (#38); a click on a tile starts the video there in the chosen
/// player. The video can be moved to the trash (never deleted for good).
/// </summary>
public sealed partial class ViewerViewModel : ObservableObject
{
    private readonly AppSettings _app;

    public string SidecarPath { get; }
    public SheetSidecar Sidecar { get; }
    /// <summary>The sheet image file, null when it is missing.</summary>
    public string? SheetPath { get; }
    /// <summary>The video (local path or URL), null when it is not found any more.</summary>
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(HasVideo), nameof(CanDelete), nameof(VideoText))] private string? _videoPath;
    [ObservableProperty] private Bitmap? _image;
    [ObservableProperty] private string _statusText = "";
    [ObservableProperty] private string _hoverText = "";
    [ObservableProperty] private PlayerKind _player;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(IsCustomPlayer))] private string _playerCommand;

    public bool HasVideo => VideoPath is not null;
    public bool CanDelete => VideoPath is not null && !VideoSource.IsUrl(VideoPath);
    public bool IsCustomPlayer => Player == PlayerKind.Custom;
    public string Title => Loc.T("ViewerTitle", Sidecar.Sheet.File.Length > 0 ? Sidecar.Sheet.File : Path.GetFileName(SidecarPath));
    public string VideoText => VideoPath is { } v ? VideoSource.DisplayPath(v) : Loc.T("ViewerVideoMissing");

    public static Choice[] Players { get; } = Choice.ForEnum<PlayerKind>("Player");

    /// <summary>How files go to the trash; tests replace it so nothing lands in the real trash.</summary>
    internal static Action<string> MoveToTrash { get; set; } = Trash.MoveToTrash;

    /// <summary>Called after the video went to the trash (the queue drops it).</summary>
    public event Action<string>? VideoDeleted;

    private ViewerViewModel(string sidecarPath, SheetSidecar sidecar, AppSettings app)
    {
        _app = app;
        SidecarPath = Path.GetFullPath(sidecarPath);
        Sidecar = sidecar;
        SheetPath = sidecar.ResolveSheet(SidecarPath);
        _videoPath = sidecar.ResolveVideo(SidecarPath);
        _player = app.Player;
        _playerCommand = app.PlayerCommand ?? "";
        StatusText = Loc.T(VideoPath is null ? "ViewerStatusNoVideo" : "ViewerStatusHint");
    }

    partial void OnPlayerChanged(PlayerKind value)
    {
        _app.Player = value;
        OnPropertyChanged(nameof(IsCustomPlayer));
    }

    partial void OnPlayerCommandChanged(string value) => _app.PlayerCommand = string.IsNullOrWhiteSpace(value) ? null : value;

    /// <summary>The sidecar of a file: the file itself (.vtsheet) or the sidecar next to a sheet image. Null when there is none.</summary>
    public static string? SidecarFor(string file)
    {
        if (file.EndsWith(SheetSidecar.Suffix, StringComparison.OrdinalIgnoreCase)) return File.Exists(file) ? file : null;
        var p = SheetSidecar.PathFor(file);
        return File.Exists(p) ? p : null;
    }

    /// <summary>Opens a sidecar (or the sidecar of a sheet image). Throws <see cref="InvalidDataException"/> when there is none or it is broken.</summary>
    public static async Task<ViewerViewModel> OpenAsync(string file, AppSettings app, FfmpegTools? tools)
    {
        var path = SidecarFor(file) ?? throw new InvalidDataException(Loc.T("ViewerNoSidecar", Path.GetFileName(file)));
        SheetSidecar sidecar;
        try { sidecar = SheetSidecar.Read(path); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { throw new InvalidDataException(e.Message, e); }
        var vm = new ViewerViewModel(path, sidecar, app);
        vm.Image = await Task.Run(() => vm.LoadImageAsync(tools));
        return vm;
    }

    /// <summary>
    /// The sheet as picture: a still sheet directly; an animated one (video) through its first frame from ffmpeg; without
    /// either a drawing of the tile rectangles with their times.
    /// </summary>
    private async Task<Bitmap> LoadImageAsync(FfmpegTools? tools)
    {
        if (SheetPath is { } sheet)
        {
            if (!Sidecar.Sheet.Animated)
            {
                try { return new Bitmap(sheet); }
                catch (Exception e) when (e is IOException or ArgumentException or InvalidOperationException or NotSupportedException) { /* fall through */ }
            }
            if (tools is not null)
            {
                try
                {
                    var r = await ProcessRunner.RunAsync(tools.FfmpegPath, ["-v", "error", "-i", sheet, "-frames:v", "1", "-f", "image2pipe", "-c:v", "png", "-"]);
                    if (r.ExitCode == 0 && r.StdOut.Length > 0) return new Bitmap(new MemoryStream(r.StdOut));
                }
                catch (Exception e) when (e is IOException or System.ComponentModel.Win32Exception or ArgumentException) { /* fall through */ }
            }
        }
        return Placeholder();
    }

    private Bitmap Placeholder()
    {
        var w = Math.Clamp(Sidecar.Sheet.Width, 16, 8000);
        var h = Math.Clamp(Sidecar.Sheet.Height, 16, 8000);
        using var bmp = new SKBitmap(w, h);
        using (var c = new SKCanvas(bmp))
        {
            c.Clear(new SKColor(0x2B, 0x2D, 0x31));
            using var fill = new SKPaint { Color = new SKColor(0x44, 0x47, 0x4D) };
            using var text = new SKPaint { Color = SKColors.White, IsAntialias = true };
            using var font = new SKFont(FontProvider.Get(null, false), 14);
            foreach (var t in Sidecar.Tiles)
            {
                c.DrawRect(t.X, t.Y, t.Width, t.Height, fill);
                c.DrawText(Formatting.Duration(t.Time), t.X + 6, t.Y + 20, font, text);
            }
        }
        using var ms = new MemoryStream();
        SheetRenderer.Encode(bmp, ms, ImageFormat.Png, 100);
        ms.Position = 0;
        return new Bitmap(ms);
    }

    /// <summary>Tile under a point given relative to the shown picture (0..1).</summary>
    public SidecarTile? TileAt(double relX, double relY) =>
        Sidecar.TileAt(relX * Sidecar.Sheet.Width, relY * Sidecar.Sheet.Height);

    public void Hover(SidecarTile? tile) =>
        HoverText = tile is null ? "" : Loc.T("ViewerTileHover", tile.Index + 1, Formatting.Duration(tile.Time));

    /// <summary>Starts the video at the tile in the chosen player; the status line says what happened.</summary>
    public void Play(SidecarTile tile)
    {
        if (VideoPath is not { } video) { StatusText = Loc.T("ViewerStatusNoVideo"); return; }
        try
        {
            var cmd = PlayerLauncher.Start(Player, PlayerCommand, video, tile.Time);
            var time = Formatting.Duration(tile.Time);
            StatusText = cmd.SupportsTime
                ? Loc.T("ViewerStatusPlaying", Path.GetFileNameWithoutExtension(cmd.FileName), time)
                : Loc.T("ViewerStatusPlayingNoTime", time);
        }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException or IOException)
        {
            StatusText = Loc.T("ViewerStatusPlayFailed", e.Message);
        }
    }

    /// <summary>Files that go to the trash with the video when the sheet is to go too: sheet, sidecar, WebVTT, preview clip.</summary>
    public IReadOnlyList<string> SheetFiles()
    {
        var list = new List<string>();
        if (SheetPath is not null) list.Add(SheetPath);
        list.Add(SidecarPath);
        var basis = SheetPath ?? SidecarPath;
        var (vtt, sprite) = WebVttWriter.PathsFor(basis);
        list.Add(vtt);
        list.Add(sprite);
        var stem = Path.GetFileName(SidecarPath)[..^SheetSidecar.Suffix.Length];
        var dir = Path.GetDirectoryName(SidecarPath)!;
        list.AddRange(Directory.EnumerateFiles(dir, stem + OutputPathResolver.TrailerMarker + ".*"));
        return list.Where(File.Exists).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>Moves the video (and, if asked, the sheet files) to the trash. Returns false and says why when it failed.</summary>
    public bool Delete(bool withSheet)
    {
        if (!CanDelete || VideoPath is not { } video) return false;
        try { MoveToTrash(video); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            StatusText = Loc.T("ViewerStatusTrashFailed", e.Message);
            return false;
        }
        var failed = 0;
        if (withSheet)
            foreach (var f in SheetFiles())
            {
                try { MoveToTrash(f); }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception) { failed++; }
            }
        VideoPath = null;
        StatusText = failed > 0 ? Loc.T("ViewerStatusTrashedPartly", failed) : Loc.T(withSheet ? "ViewerStatusTrashedAll" : "ViewerStatusTrashed");
        VideoDeleted?.Invoke(video);
        return true;
    }
}
