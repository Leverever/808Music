using _808Music.Domain.Catalog;
using _808Music.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace _808Music.Application.PlaylistThemes;

public sealed record AdminPlaylistThemeLabelInput(
    string Label,
    PersonalizedPlaylistThemeLabelPolarity Polarity,
    PersonalizedPlaylistThemeLabelSource Source,
    string? TagNamespace,
    decimal Weight);

public sealed record CreateAdminPlaylistThemeCommand(
    string ThemeKey,
    string Name,
    string Description,
    bool IsActive,
    int TrackCount,
    int SortOrder,
    IReadOnlyList<AdminPlaylistThemeLabelInput> Labels);

public sealed record UpdateAdminPlaylistThemeCommand(
    string Name,
    string Description,
    bool IsActive,
    int TrackCount,
    int SortOrder,
    IReadOnlyList<AdminPlaylistThemeLabelInput> Labels);

public sealed record AdminPlaylistThemeLabelResponse(
    Guid Id,
    string Label,
    PersonalizedPlaylistThemeLabelPolarity Polarity,
    PersonalizedPlaylistThemeLabelSource Source,
    string? TagNamespace,
    decimal Weight);

public sealed record AdminPlaylistThemeTagNamespaceResponse(
    string Namespace,
    IReadOnlyList<string> Labels);

public sealed record AdminPlaylistThemeResponse(
    Guid Id,
    string ThemeKey,
    string Name,
    string Description,
    bool IsActive,
    int TrackCount,
    int SortOrder,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    IReadOnlyList<AdminPlaylistThemeLabelResponse> Labels);

public interface IAdminPlaylistThemeManagementHandler
{
    Task<IReadOnlyList<AdminPlaylistThemeResponse>> List(
        CancellationToken cancellationToken = default);

    Task<AdminPlaylistThemeResponse?> Get(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<AdminPlaylistThemeResponse> Create(
        CreateAdminPlaylistThemeCommand command,
        CancellationToken cancellationToken = default);

    Task<AdminPlaylistThemeResponse?> Update(
        Guid id,
        UpdateAdminPlaylistThemeCommand command,
        CancellationToken cancellationToken = default);

    Task<AdminPlaylistThemeResponse?> SetActive(
        Guid id,
        bool isActive,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AdminPlaylistThemeTagNamespaceResponse>> GetTagCatalog(
        CancellationToken cancellationToken = default);
}

public sealed class PlaylistThemeConflictException : InvalidOperationException
{
    public PlaylistThemeConflictException(string message) : base(message)
    {
    }

    public PlaylistThemeConflictException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

public sealed partial class AdminPlaylistThemeManagementHandler
    : IAdminPlaylistThemeManagementHandler
{
    private const int MaximumTrackCount = 50;
    private const int MaximumLabels = 100;
    private readonly IApplicationDbContext _dbContext;

    public AdminPlaylistThemeManagementHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<AdminPlaylistThemeResponse>> List(
        CancellationToken cancellationToken = default)
    {
        var themes = await _dbContext.PersonalizedPlaylistThemes
            .AsNoTracking()
            .Include(theme => theme.Labels)
            .OrderBy(theme => theme.SortOrder)
            .ThenBy(theme => theme.ThemeKey)
            .ToListAsync(cancellationToken);

        return themes.Select(ToResponse).ToArray();
    }

    public async Task<AdminPlaylistThemeResponse?> Get(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
        {
            return null;
        }

        var theme = await _dbContext.PersonalizedPlaylistThemes
            .AsNoTracking()
            .Include(item => item.Labels)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        return theme is null ? null : ToResponse(theme);
    }

    public async Task<AdminPlaylistThemeResponse> Create(
        CreateAdminPlaylistThemeCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ValidateThemeFields(command.Name, command.Description, command.TrackCount, command.SortOrder);
        var themeKey = NormalizeThemeKey(command.ThemeKey);
        var labels = ValidateAndMapLabels(command.Labels);

        if (await _dbContext.PersonalizedPlaylistThemes
            .AnyAsync(theme => theme.ThemeKey == themeKey, cancellationToken))
        {
            throw new PlaylistThemeConflictException(
                $"A playlist theme with key '{themeKey}' already exists.");
        }

        var now = DateTime.UtcNow;
        var theme = new PersonalizedPlaylistTheme(
            themeKey,
            command.Name,
            command.Description,
            command.TrackCount,
            command.SortOrder,
            now);
        theme.SetActive(command.IsActive, now);
        theme.ReplaceLabels(labels, now);

        _dbContext.PersonalizedPlaylistThemes.Add(theme);
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            throw new PlaylistThemeConflictException(
                $"A playlist theme with key '{themeKey}' already exists.",
                ex);
        }

        return ToResponse(theme);
    }

    public async Task<AdminPlaylistThemeResponse?> Update(
        Guid id,
        UpdateAdminPlaylistThemeCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ValidateThemeFields(command.Name, command.Description, command.TrackCount, command.SortOrder);
        var labels = ValidateAndMapLabels(command.Labels);

        var theme = await _dbContext.PersonalizedPlaylistThemes
            .Include(item => item.Labels)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (theme is null)
        {
            return null;
        }

        var now = DateTime.UtcNow;
        theme.Update(
            command.Name,
            command.Description,
            command.TrackCount,
            command.SortOrder,
            now);
        theme.SetActive(command.IsActive, now);
        var previousLabels = theme.Labels.ToArray();
        theme.ReplaceLabels(labels, now);
        _dbContext.PersonalizedPlaylistThemeLabels.RemoveRange(previousLabels);
        _dbContext.PersonalizedPlaylistThemeLabels.AddRange(theme.Labels);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return ToResponse(theme);
    }

    public async Task<AdminPlaylistThemeResponse?> SetActive(
        Guid id,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        var theme = await _dbContext.PersonalizedPlaylistThemes
            .Include(item => item.Labels)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (theme is null)
        {
            return null;
        }

        theme.SetActive(isActive, DateTime.UtcNow);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return ToResponse(theme);
    }

    public async Task<IReadOnlyList<AdminPlaylistThemeTagNamespaceResponse>> GetTagCatalog(
        CancellationToken cancellationToken = default)
    {
        var analyzedTags = await _dbContext.TrackAudioTags
            .AsNoTracking()
            .Where(tag =>
                !string.IsNullOrWhiteSpace(tag.Namespace) &&
                !string.IsNullOrWhiteSpace(tag.Label))
            .Select(tag => new
            {
                Namespace = tag.Namespace.Trim(),
                Label = tag.Label.Trim()
            })
            .Distinct()
            .ToListAsync(cancellationToken);

        var themeTags = await _dbContext.PersonalizedPlaylistThemeLabels
            .AsNoTracking()
            .Where(label =>
                label.Source == PersonalizedPlaylistThemeLabelSource.EssentiaTag &&
                label.TagNamespace != null &&
                label.TagNamespace != string.Empty)
            .Select(label => new
            {
                Namespace = label.TagNamespace!,
                Label = label.Label
            })
            .Distinct()
            .ToListAsync(cancellationToken);

        return analyzedTags
            .Concat(themeTags)
            .GroupBy(
                tag => tag.Namespace,
                StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group => new AdminPlaylistThemeTagNamespaceResponse(
                group.Key,
                group
                    .Select(tag => tag.Label)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(label => label, StringComparer.OrdinalIgnoreCase)
                    .ToArray()))
            .ToArray();
    }

    private static string NormalizeThemeKey(string themeKey)
    {
        if (string.IsNullOrWhiteSpace(themeKey))
        {
            throw new ArgumentException("Theme key is required.", nameof(themeKey));
        }

        var normalized = themeKey.Trim().ToLowerInvariant();
        if (normalized.Length > 100 || !ThemeKeyPattern().IsMatch(normalized))
        {
            throw new ArgumentException(
                "Theme key must be a lowercase slug containing only letters, numbers and single hyphens.",
                nameof(themeKey));
        }

        return normalized;
    }

    private static void ValidateThemeFields(
        string name,
        string? description,
        int trackCount,
        int sortOrder)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 200)
        {
            throw new ArgumentException(
                "Theme name is required and cannot exceed 200 characters.",
                nameof(name));
        }

        if ((description?.Trim().Length ?? 0) > 500)
        {
            throw new ArgumentException(
                "Theme description cannot exceed 500 characters.",
                nameof(description));
        }

        if (trackCount is < 1 or > MaximumTrackCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(trackCount),
                $"Track count must be between 1 and {MaximumTrackCount}.");
        }

        if (sortOrder is < 0 or > 100_000)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sortOrder),
                "Sort order must be between 0 and 100000.");
        }
    }

    private static IReadOnlyList<PersonalizedPlaylistThemeLabelSpecification> ValidateAndMapLabels(
        IReadOnlyList<AdminPlaylistThemeLabelInput>? labels)
    {
        if (labels is null || labels.Count is < 1 or > MaximumLabels)
        {
            throw new ArgumentException(
                $"A theme must have between 1 and {MaximumLabels} labels.",
                nameof(labels));
        }

        return labels.Select((label, index) =>
        {
            if (string.IsNullOrWhiteSpace(label.Label) || label.Label.Trim().Length > 100)
            {
                throw new ArgumentException(
                    $"Label {index + 1} is required and cannot exceed 100 characters.",
                    nameof(labels));
            }

            if (!Enum.IsDefined(label.Polarity))
            {
                throw new ArgumentException(
                    $"Label {index + 1} has an invalid polarity.",
                    nameof(labels));
            }

            if (!Enum.IsDefined(label.Source))
            {
                throw new ArgumentException(
                    $"Label {index + 1} has an invalid source.",
                    nameof(labels));
            }

            var tagNamespace = label.Source == PersonalizedPlaylistThemeLabelSource.EssentiaTag
                ? label.TagNamespace?.Trim()
                : null;
            if (label.Source == PersonalizedPlaylistThemeLabelSource.EssentiaTag &&
                (string.IsNullOrWhiteSpace(tagNamespace) || tagNamespace.Length > 50))
            {
                throw new ArgumentException(
                    $"Label {index + 1} tag namespace is required and cannot exceed 50 characters.",
                    nameof(labels));
            }

            if (label.Weight is <= 0 or > 100)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(labels),
                    $"Label {index + 1} weight must be greater than 0 and at most 100.");
            }

            return new PersonalizedPlaylistThemeLabelSpecification(
                label.Label,
                label.Polarity,
                label.Source,
                tagNamespace,
                label.Weight);
        }).ToArray();
    }

    private static AdminPlaylistThemeResponse ToResponse(PersonalizedPlaylistTheme theme)
    {
        return new AdminPlaylistThemeResponse(
            theme.Id,
            theme.ThemeKey,
            theme.Name,
            theme.Description,
            theme.IsActive,
            theme.TrackCount,
            theme.SortOrder,
            theme.CreatedAt,
            theme.UpdatedAt,
            theme.Labels
                .OrderBy(label => label.Polarity)
                .ThenBy(label => label.Source)
                .ThenBy(label => label.Label)
                .Select(label => new AdminPlaylistThemeLabelResponse(
                    label.Id,
                    label.Label,
                    label.Polarity,
                    label.Source,
                    label.TagNamespace,
                    label.Weight))
                .ToArray());
    }

    [GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*$")]
    private static partial Regex ThemeKeyPattern();
}
