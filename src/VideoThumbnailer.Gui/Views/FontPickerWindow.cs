using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using SkiaSharp;
using VideoThumbnailer.Gui.Localization;

namespace VideoThumbnailer.Gui.Views;

/// <summary>
/// Font selection for the sheet: searchable list of the installed font families (as the sheet renderer sees them,
/// via SkiaSharp) plus "built-in DejaVu Sans", each shown in its own font, and a larger preview with a timestamp
/// and header sample. Returns "" for the built-in font, a family name, or null when cancelled.
/// </summary>
public sealed class FontPickerWindow : Window
{
    /// <summary>An entry of the list; <see cref="Family"/> is null for the built-in font.</summary>
    public sealed record Entry(string? Family, string Label)
    {
        public override string ToString() => Label;
    }

    private readonly List<Entry> _all;
    private readonly ListBox _list;
    private readonly TextBox _search;
    private readonly TextBlock _sampleTitle;
    private readonly TextBlock _sampleText;
    private readonly TextBlock _sampleTime;

    public FontPickerWindow(string? current)
    {
        Title = Loc.T("FontPickerTitle");
        Width = 520;
        Height = 560;
        MinWidth = 380;
        MinHeight = 400;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        _all = [new Entry(null, Loc.T("FontFamilyPlaceholder")), .. InstalledFamilies().Select(f => new Entry(f, f))];

        _search = new TextBox { PlaceholderText = Loc.T("FontSearch"), Name = "FontSearch" };
        _search.TextChanged += (_, _) => Filter();

        _list = new ListBox
        {
            Name = "FontList",
            ItemTemplate = new FuncDataTemplate<Entry>((e, _) => new TextBlock
            {
                Text = e?.Label, FontSize = 15, VerticalAlignment = VerticalAlignment.Center,
                FontFamily = PreviewFamily(e?.Family),
            }),
        };
        _list.SelectionChanged += (_, _) => UpdateSample();
        _list.DoubleTapped += (_, _) => Accept();

        _sampleTitle = new TextBlock { Text = "Urlaub_2026.mp4", FontSize = 22, FontWeight = FontWeight.Bold, Foreground = Brushes.White };
        _sampleText = new TextBlock { Text = Loc.T("FontSampleText"), FontSize = 14, Foreground = new SolidColorBrush(Color.Parse("#E6E6E6")) };
        _sampleTime = new TextBlock { Text = "00:12:34", FontSize = 18, FontWeight = FontWeight.Bold, Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Right };
        var sample = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#1E1E1E")), CornerRadius = new CornerRadius(6), Padding = new Thickness(14, 10),
            Child = new StackPanel { Spacing = 4, Children = { _sampleTitle, _sampleText, _sampleTime } },
        };

        var ok = new Button { Content = Loc.T("Ok"), IsDefault = true, MinWidth = 90, HorizontalContentAlignment = HorizontalAlignment.Center, Name = "FontOk" };
        ok.Classes.Add("accent");
        ok.Click += (_, _) => Accept();
        var cancel = new Button { Content = Loc.T("Cancel"), IsCancel = true, MinWidth = 90, HorizontalContentAlignment = HorizontalAlignment.Center };
        cancel.Click += (_, _) => Close(null);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 8, Children = { ok, cancel } };

        var root = new Grid { RowDefinitions = new RowDefinitions("Auto,*,Auto,Auto"), RowSpacing = 10, Margin = new Thickness(16) };
        Grid.SetRow(_list, 1);
        Grid.SetRow(sample, 2);
        Grid.SetRow(buttons, 3);
        root.Children.Add(_search);
        root.Children.Add(_list);
        root.Children.Add(sample);
        root.Children.Add(buttons);
        Content = root;

        Filter();
        _list.SelectedItem = _all.FirstOrDefault(e => string.Equals(e.Family, string.IsNullOrWhiteSpace(current) ? null : current, StringComparison.OrdinalIgnoreCase))
                             ?? _all[0];
        Opened += (_, _) => { _search.Focus(); _list.ScrollIntoView(_list.SelectedItem!); };
    }

    /// <summary>The entries currently shown (after the search filter).</summary>
    public IReadOnlyList<Entry> Visible => (IReadOnlyList<Entry>?)_list.ItemsSource ?? [];

    public TextBox Search => _search;
    public ListBox List => _list;

    /// <summary>Installed families as the sheet renderer resolves them (SkiaSharp), sorted, without duplicates.</summary>
    public static IReadOnlyList<string> InstalledFamilies() =>
        SKFontManager.Default.FontFamilies
            .Where(f => !string.IsNullOrWhiteSpace(f) && !f.StartsWith('.'))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.CurrentCultureIgnoreCase)
            .ToList();

    private static readonly Dictionary<string, bool> Previewable = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The family for showing a sample in the UI. Some installed fonts can't be read by the UI's text engine
    /// (it throws while laying out text), so each family is tried once and falls back to the default font.
    /// </summary>
    public static FontFamily PreviewFamily(string? family)
    {
        if (string.IsNullOrWhiteSpace(family)) return FontFamily.Default;
        if (!Previewable.TryGetValue(family, out var ok))
        {
            try { ok = FontManager.Current.TryGetGlyphTypeface(new Typeface(family), out _); }
            catch (Exception) { ok = false; }
            Previewable[family] = ok;
        }
        return ok ? new FontFamily(family) : FontFamily.Default;
    }

    private void Filter()
    {
        var q = _search.Text?.Trim() ?? "";
        var selected = _list.SelectedItem;
        var items = q.Length == 0 ? _all : _all.Where(e => e.Label.Contains(q, StringComparison.CurrentCultureIgnoreCase)).ToList();
        _list.ItemsSource = items;
        if (selected is Entry e && items.Contains(e)) _list.SelectedItem = e;
        else if (items.Count > 0) _list.SelectedIndex = 0;
    }

    private void UpdateSample()
    {
        var family = PreviewFamily((_list.SelectedItem as Entry)?.Family);
        _sampleTitle.FontFamily = family;
        _sampleText.FontFamily = family;
        _sampleTime.FontFamily = family;
    }

    /// <summary>Closes with the selected entry: "" for the built-in font, otherwise the family name.</summary>
    public void Accept()
    {
        if (_list.SelectedItem is Entry e) Close(e.Family ?? "");
    }

    public static Task<string?> ShowAsync(Window owner, string? current) =>
        new FontPickerWindow(current) { Icon = owner.Icon }.ShowDialog<string?>(owner);
}
