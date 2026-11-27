using Avalonia.Input;

namespace VideoThumbnailer.Gui.Input;

/// <summary>One keyboard shortcut. <see cref="Command"/> = with Ctrl (Windows/Linux) or Cmd (macOS).</summary>
public sealed record ShortcutKey(Key Key, bool Command = false, bool Shift = false);

/// <summary>
/// Keyboard shortcuts of the main window (#34). The command key is Ctrl, on macOS Cmd. Names are the actions;
/// the first key of an action is the one shown in tooltips.
/// </summary>
public static class Shortcuts
{
    public static readonly IReadOnlyDictionary<string, ShortcutKey[]> All = new Dictionary<string, ShortcutKey[]>
    {
        ["AddFiles"] = [new(Key.O, Command: true)],
        ["AddFolder"] = [new(Key.O, Command: true, Shift: true)],
        ["AddUrl"] = [new(Key.U, Command: true)],
        ["Start"] = [new(Key.Enter, Command: true), new(Key.F5)],
        ["Pause"] = [new(Key.P, Command: true)],
        ["Cancel"] = [new(Key.Escape)],
        ["RemoveSelected"] = [new(Key.Delete)],
        ["RequeueSelected"] = [new(Key.R, Command: true)],
        ["SelectAll"] = [new(Key.A, Command: true)],
        ["ToggleMode"] = [new(Key.E, Command: true)],
        ["ToggleLivePreview"] = [new(Key.L, Command: true)],
        ["ToggleQueue"] = [new(Key.B, Command: true)],
    };

    public static bool IsMac { get; set; } = OperatingSystem.IsMacOS();

    public static KeyModifiers CommandModifier => IsMac ? KeyModifiers.Meta : KeyModifiers.Control;

    public static KeyGesture Gesture(ShortcutKey k) =>
        new(k.Key, (k.Command ? CommandModifier : KeyModifiers.None) | (k.Shift ? KeyModifiers.Shift : KeyModifiers.None));

    /// <summary>Text for tooltips: "Strg+Umschalt+O", "Ctrl+Enter / F5", on macOS "⇧⌘O".</summary>
    public static string Display(string action, string lang)
    {
        if (!All.TryGetValue(action, out var keys)) return "";
        return string.Join(" / ", keys.Select(k => Display(k, lang)));
    }

    public static string Display(ShortcutKey k, string lang) => Display(k, lang, IsMac);

    /// <summary>Text of one key for Windows/Linux or macOS, independent of the running system (README table).</summary>
    public static string Display(ShortcutKey k, string lang, bool mac)
    {
        var de = lang == "de";
        var key = k.Key switch
        {
            Key.Enter => mac ? "↩" : de ? "Eingabe" : "Enter",
            Key.Escape => mac ? "⎋" : "Esc",
            Key.Delete => mac ? "⌫" : de ? "Entf" : "Del",
            _ => k.Key.ToString(),
        };
        if (mac) return (k.Shift ? "⇧" : "") + (k.Command ? "⌘" : "") + key;
        var parts = new List<string>();
        if (k.Command) parts.Add(de ? "Strg" : "Ctrl");
        if (k.Shift) parts.Add(de ? "Umschalt" : "Shift");
        parts.Add(key);
        return string.Join("+", parts);
    }
}
