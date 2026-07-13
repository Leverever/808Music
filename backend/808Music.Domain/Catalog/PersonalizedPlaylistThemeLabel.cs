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
        Weight = weight;
    }

    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid ThemeId { get; private set; }
    public string Label { get; private set; }
    public PersonalizedPlaylistThemeLabelPolarity Polarity { get; private set; }
    public PersonalizedPlaylistThemeLabelSource Source { get; private set; }
    public decimal Weight { get; private set; }
}
