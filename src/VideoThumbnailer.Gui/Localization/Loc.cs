using System.ComponentModel;
using System.Globalization;

namespace VideoThumbnailer.Gui.Localization;

public enum UiLanguage { System, English, German }

/// <summary>
/// Minimal string table for the GUI. Bind in XAML with <c>{l:T Key}</c>; in code use <see cref="T(string, object[])"/>.
/// Switching <see cref="Language"/> updates all bound texts immediately.
/// </summary>
public sealed class Loc : INotifyPropertyChanged
{
    public static Loc Instance { get; } = new();

    private string _code = "en";
    private UiLanguage _language = UiLanguage.System;

    public event PropertyChangedEventHandler? PropertyChanged;
    /// <summary>Raised after the language changed (for texts that are not bound).</summary>
    public event Action? LanguageChanged;

    /// <summary>Two-letter code of the active language ("en" or "de").</summary>
    public string Code => _code;

    public UiLanguage Language
    {
        get => _language;
        set
        {
            _language = value;
            var code = Resolve(value);
            Core.Localization.Lang.Current = code;
            if (code == _code) return;
            _code = code;
            Core.Localization.Lang.Current = code;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Code)));
            LanguageChanged?.Invoke();
        }
    }

    public string this[string key] => Get(key, _code);

    public static string T(string key, params object?[] args)
    {
        var s = Instance[key];
        return args.Length == 0 ? s : string.Format(CultureInfo.CurrentCulture, s, args);
    }

    /// <summary>Language used for "System": German if the OS UI language is German, otherwise English.</summary>
    public static string SystemLanguageCode() => Core.Localization.Lang.Detect();

    public static string Resolve(UiLanguage l) => l switch
    {
        UiLanguage.German => "de",
        UiLanguage.English => "en",
        _ => SystemLanguageCode(),
    };

    internal static string Get(string key, string code)
    {
        if (!Strings.Table.TryGetValue(key, out var entry)) return "⟦" + key + "⟧";
        return code == "de" ? entry.De : entry.En;
    }
}
