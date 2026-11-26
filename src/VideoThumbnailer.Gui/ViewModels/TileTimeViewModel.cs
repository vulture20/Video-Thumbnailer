using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VideoThumbnailer.Core.Layout;
using VideoThumbnailer.Core.Util;
using VideoThumbnailer.Gui.Localization;

namespace VideoThumbnailer.Gui.ViewModels;

public enum TileTimeResult { Cancel, Ok, Automatic }

/// <summary>
/// Editor for the capture time of one tile. Every valid change is reported through <see cref="Changed"/> at once,
/// so the live preview behind the dialog shows the new picture while the user is still choosing.
/// </summary>
public partial class TileTimeViewModel : ObservableObject
{
    private bool _syncing;

    /// <param name="index">Tile in timeline order, from 0.</param>
    /// <param name="initial">Time shown first (the manual one, or the time currently used).</param>
    public TileTimeViewModel(int index, double duration, double frameRate, double automaticTime, ManualTime initial,
                             bool animated, double defaultClipSeconds)
    {
        Index = index;
        Duration = Math.Max(0, duration);
        FrameStep = frameRate > 0 ? 1.0 / frameRate : 0.04;
        AutomaticTime = automaticTime;
        IsAnimated = animated;
        DefaultClipSeconds = defaultClipSeconds;
        _time = Clamp(initial.Time);
        _timeText = ManualTime.FormatTime(_time);
        _clipText = initial.ClipSeconds?.ToString("0.###", CultureInfo.InvariantCulture) ?? "";
    }

    public int Index { get; }
    public double Duration { get; }
    public double FrameStep { get; }
    public double AutomaticTime { get; }
    public bool IsAnimated { get; }
    public double DefaultClipSeconds { get; }

    public string Title => Loc.T("TileTimeTitle", Index + 1);
    public string Info => Loc.T("TileTimeInfo", Formatting.Duration(Duration, millis: Duration % 1 != 0), ManualTime.FormatTime(AutomaticTime));
    public string ClipLabel => Loc.T("TileTimeClip", DefaultClipSeconds.ToString("0.#", CultureInfo.CurrentCulture));

    /// <summary>Fires with the new manual time after every valid change.</summary>
    public event Action<ManualTime>? Changed;

    [ObservableProperty] private double _time;
    [ObservableProperty] private string _timeText;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(IsValid))] private bool _timeInvalid;
    [ObservableProperty] private string _clipText;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(IsValid))] private bool _clipInvalid;

    public bool IsValid => !TimeInvalid && !ClipInvalid;

    /// <summary>The time as entered (null while the input is invalid).</summary>
    public ManualTime? Current => IsValid ? new ManualTime(Time, ClipSeconds()) : null;

    private double Clamp(double t) => Duration > 0 ? Math.Clamp(t, 0, Duration) : Math.Max(0, t);

    partial void OnTimeChanged(double value)
    {
        if (_syncing) return;
        _syncing = true;
        TimeText = ManualTime.FormatTime(value);
        TimeInvalid = false;
        _syncing = false;
        Notify();
    }

    partial void OnTimeTextChanged(string value)
    {
        if (_syncing) return;
        if (!ManualTime.TryParseTime(value ?? "", out var t)) { TimeInvalid = true; return; }
        _syncing = true;
        Time = Clamp(t);
        TimeInvalid = false;
        _syncing = false;
        Notify();
    }

    partial void OnClipTextChanged(string value)
    {
        ClipInvalid = !string.IsNullOrWhiteSpace(value) && ParseClip(value) is null;
        Notify();
    }

    private double? ClipSeconds() => string.IsNullOrWhiteSpace(ClipText) ? null : ParseClip(ClipText);

    private static double? ParseClip(string text) =>
        double.TryParse(text.Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) && v > 0
            ? Math.Clamp(v, 0.5, 30) : null;

    private void Notify()
    {
        if (Current is { } c) Changed?.Invoke(c);
    }

    /// <summary>Moves the time by the given number of seconds; "frame" / "-frame" move by one video frame.</summary>
    [RelayCommand]
    private void Step(string amount)
    {
        var d = amount switch
        {
            "frame" => FrameStep,
            "-frame" => -FrameStep,
            _ => double.Parse(amount, CultureInfo.InvariantCulture),
        };
        Time = Clamp(Time + d);
    }
}
