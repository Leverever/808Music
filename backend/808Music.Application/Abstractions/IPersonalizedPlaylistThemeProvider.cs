using _808Music.Domain.Enums;

namespace _808Music.Application.Abstractions;

public interface IPersonalizedPlaylistThemeProvider
{
    Task<IReadOnlyList<PersonalizedPlaylistThemeDefinition>> GetActiveThemesAsync(
        CancellationToken cancellationToken = default);

    Task<PersonalizedPlaylistThemeDefinition?> FindByKeyAsync(
        string themeKey,
        CancellationToken cancellationToken = default);
}

public sealed record PersonalizedPlaylistThemeDefinition(
    Guid Id,
    string ThemeKey,
    string Name,
    string Description,
    int TrackCount,
    int SortOrder,
    IReadOnlyList<PersonalizedPlaylistThemeLabelDefinition> Labels)
{
    public IReadOnlyList<PersonalizedPlaylistThemeLabelDefinition> PositiveLabels =>
        Labels.Where(x => x.Polarity == PersonalizedPlaylistThemeLabelPolarity.Positive).ToArray();

    public IReadOnlyList<PersonalizedPlaylistThemeLabelDefinition> NegativeLabels =>
        Labels.Where(x => x.Polarity == PersonalizedPlaylistThemeLabelPolarity.Negative).ToArray();
}

public sealed record PersonalizedPlaylistThemeLabelDefinition(
    string Label,
    PersonalizedPlaylistThemeLabelPolarity Polarity,
    PersonalizedPlaylistThemeLabelSource Source,
    string? TagNamespace,
    double Weight);
