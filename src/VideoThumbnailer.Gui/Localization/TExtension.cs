using Avalonia.Data;
using Avalonia.Markup.Xaml;

namespace VideoThumbnailer.Gui.Localization;

/// <summary>XAML: <c>Text="{l:T AddFiles}"</c> binds to the translated string and follows language changes.</summary>
public sealed class TExtension(string key) : MarkupExtension
{
    public string Key { get; set; } = key;

    public override object ProvideValue(IServiceProvider serviceProvider) =>
        new Binding($"[{Key}]") { Source = Loc.Instance, Mode = BindingMode.OneWay };
}
