using System.ComponentModel;
using VideoThumbnailer.Core.Layout;
using VideoThumbnailer.Gui.Localization;

namespace VideoThumbnailer.Gui.ViewModels;

/// <summary>A tile of the layout picker: the plain grid, a built-in special grid or one from the loaded preset.</summary>
public sealed class LayoutChoice : INotifyPropertyChanged
{
    private LayoutChoice(GridTemplate? template, bool builtIn, bool own = false)
    {
        Template = template;
        IsBuiltIn = builtIn;
        IsOwn = own;
        Loc.Instance.LanguageChanged += () =>
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Label)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Tip)));
        };
    }

    /// <summary>Null = plain grid of columns × rows.</summary>
    public GridTemplate? Template { get; }
    public bool IsBuiltIn { get; }
    public bool IsPlain => Template is null;
    /// <summary>An own grid from the grid editor (#36), saved as ".vtgrid" in the settings folder.</summary>
    public bool IsOwn { get; }

    public string Label => Template is null ? Loc.T("LayoutPlain") : IsBuiltIn ? Loc.T("Layout." + Template.Name) : Template.Name;

    public string Tip => Template is null
        ? Loc.T("LayoutPlainTip")
        : Loc.T("LayoutTemplateTip", Template.Tiles.Count, Template.Columns, Template.Rows)
          + (IsBuiltIn ? "" : string.IsNullOrWhiteSpace(Template.Description) ? "" : "\n" + Template.Description);

    public event PropertyChangedEventHandler? PropertyChanged;

    public static LayoutChoice Plain() => new(null, true);

    public static IEnumerable<LayoutChoice> BuiltIns() => BuiltInTemplates.All.Select(t => new LayoutChoice(t.Clone(), true));

    public static LayoutChoice Custom(GridTemplate t) => new(t.Clone(), false);

    public static LayoutChoice Own(GridTemplate t) => new(t.Clone(), false, true);
}
