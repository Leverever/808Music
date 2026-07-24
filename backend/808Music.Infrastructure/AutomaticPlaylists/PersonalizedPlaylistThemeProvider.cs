using _808Music.Application;
using _808Music.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace _808Music.Infrastructure.AutomaticPlaylists;

public sealed class PersonalizedPlaylistThemeProvider : IPersonalizedPlaylistThemeProvider
{
    private readonly IApplicationDbContext _dbContext;
    private IReadOnlyList<PersonalizedPlaylistThemeDefinition>? _activeThemes;

    public PersonalizedPlaylistThemeProvider(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<PersonalizedPlaylistThemeDefinition>> GetActiveThemesAsync(
        CancellationToken cancellationToken = default)
    {
        if (_activeThemes is not null)
        {
            return _activeThemes;
        }

        var themes = await _dbContext.PersonalizedPlaylistThemes
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.ThemeKey)
            .Select(x => new ThemeProjection(
                x.Id,
                x.ThemeKey,
                x.Name,
                x.Description,
                x.TrackCount,
                x.SortOrder))
            .ToListAsync(cancellationToken);

        if (themes.Count == 0)
        {
            _activeThemes = [];
            return _activeThemes;
        }

        var themeIds = themes.Select(x => x.Id).ToArray();
        var labels = await _dbContext.PersonalizedPlaylistThemeLabels
            .AsNoTracking()
            .Where(x => themeIds.Contains(x.ThemeId))
            .OrderBy(x => x.Label)
            .Select(x => new LabelProjection(
                x.ThemeId,
                x.Label,
                x.Polarity,
                x.Source,
                x.TagNamespace,
                x.Weight))
            .ToListAsync(cancellationToken);

        var labelsByTheme = labels
            .GroupBy(x => x.ThemeId)
            .ToDictionary(
                x => x.Key,
                x => (IReadOnlyList<PersonalizedPlaylistThemeLabelDefinition>)x
                    .Select(label => new PersonalizedPlaylistThemeLabelDefinition(
                        label.Label,
                        label.Polarity,
                        label.Source,
                        label.TagNamespace,
                        (double)label.Weight))
                    .ToArray());

        _activeThemes = themes
            .Select(theme => new PersonalizedPlaylistThemeDefinition(
                theme.Id,
                theme.ThemeKey,
                theme.Name,
                theme.Description,
                theme.TrackCount,
                theme.SortOrder,
                labelsByTheme.GetValueOrDefault(theme.Id, [])))
            .ToArray();

        return _activeThemes;
    }

    public async Task<PersonalizedPlaylistThemeDefinition?> FindByKeyAsync(
        string themeKey,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(themeKey))
        {
            return null;
        }

        var themes = await GetActiveThemesAsync(cancellationToken);

        return themes.FirstOrDefault(x =>
            x.ThemeKey.Equals(themeKey.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    private sealed record ThemeProjection(
        Guid Id,
        string ThemeKey,
        string Name,
        string Description,
        int TrackCount,
        int SortOrder);

    private sealed record LabelProjection(
        Guid ThemeId,
        string Label,
        Domain.Enums.PersonalizedPlaylistThemeLabelPolarity Polarity,
        Domain.Enums.PersonalizedPlaylistThemeLabelSource Source,
        string? TagNamespace,
        decimal Weight);
}
