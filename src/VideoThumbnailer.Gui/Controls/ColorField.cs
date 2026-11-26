using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;

namespace VideoThumbnailer.Gui.Controls;

/// <summary>
/// Text box for a color (#RGB, #ARGB, #RRGGBB or #AARRGGBB) with a small swatch that shows the
/// color on a checkerboard, so transparency is visible. An unreadable value gets a red frame; the sheet then
/// uses the built-in default for that color.
/// </summary>
public sealed class ColorField : Grid
{
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<ColorField, string?>(nameof(Text), defaultBindingMode: BindingMode.TwoWay);

    private readonly TextBox _box;
    private readonly Border _swatch;
    private readonly Border _frame;

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
        SetColumn(_frame, 1);
        Children.Add(_box);
        Children.Add(_frame);
        Update();
    }

    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }

    /// <summary>The inner text box (for tests and focus).</summary>
    public TextBox Box => _box;

    /// <summary>True if <see cref="Text"/> is a color the sheet can use.</summary>
    public bool IsValid { get; private set; }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TextProperty) Update();
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
        ToolTip.SetTip(_frame, IsValid ? Text : Localization.Loc.T("ColorInvalid"));
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
