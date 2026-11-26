using System.Globalization;
using System.Runtime.InteropServices;

namespace VideoThumbnailer.Core.Localization;

/// <summary>
/// Supported languages ("en", "de") and detection of the system language. Works without ICU
/// (the CLI runs with invariant globalization): falls back to environment variables and, on Windows, the OS UI language.
/// </summary>
public static class Lang
{
    public const string English = "en";
    public const string German = "de";
    public static readonly IReadOnlyList<string> Supported = [English, German];

    /// <summary>Language for texts produced by the core (messages, sheet labels when the sheet follows the app). Set by GUI/CLI.</summary>
    public static string Current { get; set; } = Detect();

    /// <summary>Normalizes "de", "de-DE", "German", "deutsch" … to "de"/"en"; null for unknown values and "auto"/"system".</summary>
    public static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var v = value.Trim().ToLowerInvariant();
        if (v is "auto" or "system") return null;
        if (v.StartsWith("de") || v is "german" or "deutsch") return German;
        if (v.StartsWith("en") || v is "english" or "englisch") return English;
        return null;
    }

    /// <summary>Returns "de" if the user's UI language is German, otherwise "en".</summary>
    public static string Detect()
    {
        var name = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        if (!string.IsNullOrEmpty(name) && name != "iv")
            return name == "de" ? German : English;

        foreach (var var in new[] { "LC_ALL", "LC_MESSAGES", "LANG", "LANGUAGE" })
            if (Normalize(Environment.GetEnvironmentVariable(var)) is { } fromEnv) return fromEnv;

        if (OperatingSystem.IsWindows())
        {
            try
            {
                // Primary language id 0x07 = German.
                return (GetUserDefaultUILanguage() & 0x3FF) == 0x07 ? German : English;
            }
            catch { /* ignore */ }
        }
        return English;
    }

    /// <summary>Number format for sheet texts: decimal comma for German, without needing culture data.</summary>
    public static NumberFormatInfo NumberFormat(string lang)
    {
        if (lang != German) return NumberFormatInfo.InvariantInfo;
        var nf = (NumberFormatInfo)NumberFormatInfo.InvariantInfo.Clone();
        nf.NumberDecimalSeparator = ",";
        nf.NumberGroupSeparator = ".";
        return nf;
    }

    [DllImport("kernel32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern ushort GetUserDefaultUILanguage();
}
