using SkiaSharp;

namespace VideoThumbnailer.Core.Rendering;

/// <summary>Resolves typefaces. Falls back to the bundled DejaVu Sans so rendering works without system fonts.</summary>
public static class FontProvider
{
    private static readonly Lazy<SKTypeface> Regular = new(() => LoadEmbedded("VideoThumbnailer.Fonts.DejaVuSans.ttf"));
    private static readonly Lazy<SKTypeface> BoldFace = new(() => LoadEmbedded("VideoThumbnailer.Fonts.DejaVuSans-Bold.ttf"));

    public static SKTypeface Get(string? family, bool bold)
    {
        if (!string.IsNullOrWhiteSpace(family))
        {
            var style = bold ? SKFontStyle.Bold : SKFontStyle.Normal;
            var tf = SKTypeface.FromFamilyName(family, style);
            if (tf is not null && string.Equals(tf.FamilyName, family, StringComparison.OrdinalIgnoreCase))
                return tf;
        }
        return bold ? BoldFace.Value : Regular.Value;
    }

    private static SKTypeface LoadEmbedded(string name)
    {
        using var stream = typeof(FontProvider).Assembly.GetManifestResourceStream(name)
                           ?? throw new InvalidOperationException($"Embedded font missing: {name}");
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        ms.Position = 0;
        return SKTypeface.FromStream(ms) ?? SKTypeface.Default;
    }
}
