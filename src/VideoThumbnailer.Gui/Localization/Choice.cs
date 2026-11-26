using System.ComponentModel;

namespace VideoThumbnailer.Gui.Localization;

/// <summary>An option for a ComboBox: a value plus a translated label that follows language changes.</summary>
public sealed class Choice : INotifyPropertyChanged
{
    private readonly string _key;

    public Choice(object value, string key)
    {
        Value = value;
        _key = key;
        Loc.Instance.LanguageChanged += () => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Label)));
    }

    public object Value { get; }
    public string Label => Loc.Instance[_key];

    public event PropertyChangedEventHandler? PropertyChanged;

    public override string ToString() => Label;

    /// <summary>One choice per enum value, labels from keys "{prefix}.{EnumName}".</summary>
    public static Choice[] ForEnum<T>(string prefix) where T : struct, Enum =>
        Enum.GetValues<T>().Select(v => new Choice(v, $"{prefix}.{v}")).ToArray();
}
