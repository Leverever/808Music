using _808Music.Domain.Enums;

namespace _808Music.Domain.Catalog;

public class PersonalizedPlaylistThemeLabel
{
    private PersonalizedPlaylistThemeLabel()
    {
        Label = string.Empty;
    }

    public PersonalizedPlaylistThemeLabel(
        Guid themeId,
        string label,
        PersonalizedPlaylistThemeLabelPolarity polarity,
        PersonalizedPlaylistThemeLabelSource source,
        string? tagNamespace,
        decimal weight = 1m)
    {
        if (themeId == Guid.Empty)
        {
            throw new ArgumentException("Theme id is required.", nameof(themeId));
        }

        if (string.IsNullOrWhiteSpace(label))
        {
            throw new ArgumentException("Label is required.", nameof(label));
        }

        if (weight <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(weight), "Weight must be positive.");
        }

        ThemeId = themeId;
        Label = label.Trim().Length <= 100 ? label.Trim() : label.Trim()[..100];
        Polarity = polarity;
        Source = source;
        TagNamespace = NormalizeTagNamespace(tagNamespace, source);
        Weight = weight;
    }

    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid ThemeId { get; private set; }
    public string Label { get; private set; }
    public PersonalizedPlaylistThemeLabelPolarity Polarity { get; private set; }
    public PersonalizedPlaylistThemeLabelSource Source { get; private set; }
    public string? TagNamespace { get; private set; }
    public decimal Weight { get; private set; }

    private static string? NormalizeTagNamespace(
        string? tagNamespace,
        PersonalizedPlaylistThemeLabelSource source)
    {
        if (source != PersonalizedPlaylistThemeLabelSource.EssentiaTag)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(tagNamespace))
        {
            throw new ArgumentException(
                "A tag namespace is required for analyzed audio tags.",
                nameof(tagNamespace));
        }

        var normalized = tagNamespace.Trim();
        if (normalized.Length > 50)
        {
            throw new ArgumentException(
                "A tag namespace cannot exceed 50 characters.",
                nameof(tagNamespace));
        }

        return normalized;
    }
}
