using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Markup.Xaml;

namespace VideoThumbnailer.Gui.Localization;

/// <summary>
/// XAML: <c>Text="{l:T AddFiles}"</c> binds to the translated string and follows language changes.
/// With <c>Shortcut=AddFiles</c> the keyboard shortcut of that action is added in brackets (for tooltips, #34).
/// </summary>
public sealed class TExtension(string key) : MarkupExtension
{
    public string Key { get; set; } = key;
    public string? Shortcut { get; set; }

    public override object ProvideValue(IServiceProvider serviceProvider) =>
        new Binding($"[{Key}]")
        {
            Source = Loc.Instance,
            Mode = BindingMode.OneWay,
            Converter = Shortcut is null ? null : new WithShortcut(Shortcut),
        };

    private sealed class WithShortcut(string action) : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => WithKeys(value as string, action);
        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
    }

    /// <summary>"Add files… (Ctrl+O)"; a text ending in "…" keeps it.</summary>
    public static string WithKeys(string? text, string action)
    {
        var keys = Input.Shortcuts.Display(action, Loc.Instance.Code);
        return keys.Length == 0 ? text ?? "" : $"{text} ({keys})";
    }
}
