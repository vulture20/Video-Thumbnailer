using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;

namespace VideoThumbnailer.Gui.Controls;

/// <summary>
/// Text box for a color (#RGB, #ARGB, #RRGGBB or #AARRGGBB) with a small swatch that shows the
/// color on a checkerboard, so transparency is visible. An unreadable value gets a red frame; the sheet then
/// uses the built-in default for that color. A click on the swatch opens a color picker (spectrum, palette,
/// sliders and opacity); every change there is written back as #AARRGGBB.
/// </summary>
public sealed class ColorField : Grid
{
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<ColorField, string?>(nameof(Text), defaultBindingMode: BindingMode.TwoWay);

    private readonly TextBox _box;
    private readonly Border _swatch;
    private readonly Border _frame;
    private readonly Button _button;
    private readonly ColorView _picker;
    private bool _syncing;

    public ColorField()
    {
        ColumnDefinitions = new ColumnDefinitions("*,Auto");
        ColumnSpacing = 6;
        _box = new TextBox { PlaceholderText = "#AARRGGBB", MinWidth = 0 };
        _box.Bind(TextBox.TextProperty, new Binding(nameof(Text)) { Source = this, Mode = BindingMode.TwoWay });
        _swatch = new Border { CornerRadius = new CornerRadius(3) };
        _frame = new Border
        {
            Width = 26, Height = 26, CornerRadius = new CornerRadius(4), BorderThickness = new Thickness(1),
            VerticalAlignment = VerticalAlignment.Center, ClipToBounds = true,
            Background = Checkerboard, Child = _swatch,
        };
        _picker = new ColorView
        {
            IsAlphaEnabled = true, IsAlphaVisible = true, IsAccentColorsVisible = false,
            Width = 320, Height = 380,
        };
        _picker.ColorChanged += (_, e) =>
        {
            if (_syncing) return;
            _syncing = true;
            Text = ToHex(e.NewColor);
            _syncing = false;
        };
        var flyout = new Flyout { Content = _picker, Placement = PlacementMode.LeftEdgeAlignedTop };
        flyout.Opening += (_, _) => SyncPicker();
        _button = new Button
        {
            Content = _frame, Padding = new Thickness(0), Background = Brushes.Transparent, BorderThickness = new Thickness(0),
            VerticalAlignment = VerticalAlignment.Center, Flyout = flyout,
        };
        ToolTip.SetTip(_button, Localization.Loc.T("ColorPick"));
        SetColumn(_button, 1);
        Children.Add(_box);
        Children.Add(_button);
        Update();
    }

    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }

    /// <summary>The inner text box (for tests and focus).</summary>
    public TextBox Box => _box;

    /// <summary>The swatch button that opens the color picker, and the picker itself (for tests).</summary>
    public Button PickerButton => _button;
    public ColorView Picker => _picker;

    /// <summary>#AARRGGBB, the format of the presets.</summary>
    public static string ToHex(Color c) => $"#{c.A:X2}{c.R:X2}{c.G:X2}{c.B:X2}";

    private void SyncPicker()
    {
        _syncing = true;
        _picker.Color = TryParse(Text, out var c) ? c : Colors.White;
        _syncing = false;
    }

    /// <summary>True if <see cref="Text"/> is a color the sheet can use.</summary>
    public bool IsValid { get; private set; }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TextProperty)
        {
            Update();
            if (!_syncing && _button.Flyout is { IsOpen: true }) SyncPicker();
        }
    }

    private void Update()
    {
        IsValid = TryParse(Text, out var color);
        _swatch.Background = IsValid ? new SolidColorBrush(color) : InvalidBrush;
        _swatch.Child = IsValid ? null : new TextBlock
        {
            Text = "?", Foreground = Brushes.White, FontWeight = FontWeight.Bold,
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
        };
        _frame.BorderBrush = IsValid ? new SolidColorBrush(Color.FromArgb(0x60, 0x80, 0x80, 0x80)) : InvalidBrush;
        ToolTip.SetTip(_frame, IsValid ? null : Localization.Loc.T("ColorInvalid"));
    }

    /// <summary>Same rules as the sheet renderer (SkiaSharp's color parser).</summary>
    public static bool TryParse(string? text, out Color color)
    {
        color = default;
        if (string.IsNullOrWhiteSpace(text) || !SkiaSharp.SKColor.TryParse(text, out var c)) return false;
        color = Color.FromArgb(c.Alpha, c.Red, c.Green, c.Blue);
        return true;
    }

    private static readonly IBrush InvalidBrush = new SolidColorBrush(Color.FromRgb(0xC8, 0x3C, 0x3C));

    private static readonly IBrush Checkerboard = new VisualBrush
    {
        TileMode = TileMode.Tile,
        SourceRect = new RelativeRect(0, 0, 8, 8, RelativeUnit.Absolute),
        DestinationRect = new RelativeRect(0, 0, 8, 8, RelativeUnit.Absolute),
        Visual = new Canvas
        {
            Width = 8, Height = 8, Background = Brushes.White,
            Children =
            {
                new Border { Width = 4, Height = 4, Background = Brushes.LightGray },
                new Border { Width = 4, Height = 4, Background = Brushes.LightGray, [Canvas.LeftProperty] = 4.0, [Canvas.TopProperty] = 4.0 },
            },
        },
    };
}
