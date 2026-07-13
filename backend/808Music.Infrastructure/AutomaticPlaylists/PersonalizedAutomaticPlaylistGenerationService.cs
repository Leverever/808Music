using _808Music.Application;
using _808Music.Application.Abstractions;
using _808Music.Domain.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace _808Music.Infrastructure.AutomaticPlaylists;

public sealed class PersonalizedAutomaticPlaylistGenerationService : IAutomaticPlaylistGenerationService
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IPersonalizedRecommendationService _recommendationService;
    private readonly IPersonalizedPlaylistThemeProvider _themeProvider;
    private readonly AutomaticPlaylistOptions _options;
    private readonly ILogger<PersonalizedAutomaticPlaylistGenerationService> _logger;

    public PersonalizedAutomaticPlaylistGenerationService(
        IApplicationDbContext dbContext,
        IPersonalizedRecommendationService recommendationService,
        IPersonalizedPlaylistThemeProvider themeProvider,
        IOptions<AutomaticPlaylistOptions> options,
        ILogger<PersonalizedAutomaticPlaylistGenerationService> logger)
    {
        _dbContext = dbContext;
        _recommendationService = recommendationService;
        _themeProvider = themeProvider;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<AutomaticPlaylistGenerationResult> GenerateDailyAsync(
        DateOnly playlistDate,
        CancellationToken cancellationToken = default)
    {
        var themes = await ResolveThemesAsync(cancellationToken);
        if (themes.Count == 0)
        {
            _logger.LogWarning("No active automatic playlist themes were found in the database.");

            return new AutomaticPlaylistGenerationResult(playlistDate, 0);
        }

        var activeUserIds = await LoadActiveUserIdsAsync(playlistDate, cancellationToken);
        var generatedPlaylistCount = 0;

        foreach (var userId in activeUserIds)
        {
            foreach (var theme in themes)
            {
                try
                {
                    var wasEnsured = await EnsureThemePlaylistAsync(
                        userId,
                        theme,
                        playlistDate,
                        cancellationToken);

                    if (wasEnsured)
                    {
                        generatedPlaylistCount++;
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(
                        ex,
                        "Failed to generate automatic playlist {ThemeKey} for user {UserId} on {PlaylistDate}",
                        theme.ThemeKey,
                        userId,
                        playlistDate);
                }
            }
        }

        return new AutomaticPlaylistGenerationResult(
            playlistDate,
            generatedPlaylistCount);
    }

    private async Task<IReadOnlyList<int>> LoadActiveUserIdsAsync(
        DateOnly playlistDate,
        CancellationToken cancellationToken)
    {
        var sourceWindowDays = Math.Max(1, _options.SourceWindowDays);
        var windowStart = playlistDate
            .AddDays(-sourceWindowDays)
            .ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var windowEnd = playlistDate
            .AddDays(1)
            .ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        return await _dbContext.UserTrackInteractions
            .AsNoTracking()
            .Where(x => x.OccurredAt >= windowStart && x.OccurredAt < windowEnd)
            .Select(x => x.UserId)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync(cancellationToken);
    }

    private async Task<bool> EnsureThemePlaylistAsync(
        int userId,
        ThemeToGenerate theme,
        DateOnly playlistDate,
        CancellationToken cancellationToken)
    {
        var playlist = await _dbContext.GeneratedPersonalizedPlaylists
            .FirstOrDefaultAsync(
                x => x.UserId == userId &&
                    x.ThemeKey == theme.ThemeKey &&
                    x.PlaylistDate == playlistDate,
                cancellationToken);

        if (playlist is not null)
        {
            playlist.RefreshMetadata(theme.Id, theme.Name, theme.Description);

            var existingTrackCount = await _dbContext.GeneratedPersonalizedPlaylistTracks
                .AsNoTracking()
                .CountAsync(x => x.PlaylistId == playlist.Id, cancellationToken);

            if (existingTrackCount > 0)
            {
                await _dbContext.SaveChangesAsync(cancellationToken);

                return true;
            }
        }
        else
        {
            playlist = new GeneratedPersonalizedPlaylist(
                userId,
                theme.Id,
                theme.ThemeKey,
                theme.Name,
                theme.Description,
                playlistDate,
                DateTime.UtcNow);

            _dbContext.GeneratedPersonalizedPlaylists.Add(playlist);
        }

        var recommendations = await _recommendationService.GetRecommendationsAsync(
            new PersonalizedRecommendationRequest(
                userId,
                PersonalizedRecommendationIntent.DailyThematicPlaylist,
                [],
                theme.ThemeKey,
                theme.TrackCount,
                []),
            cancellationToken);

        var staleTracks = await _dbContext.GeneratedPersonalizedPlaylistTracks
            .Where(x => x.PlaylistId == playlist.Id)
            .ToListAsync(cancellationToken);

        if (staleTracks.Count > 0)
        {
            _dbContext.GeneratedPersonalizedPlaylistTracks.RemoveRange(staleTracks);
        }

        var position = 1;
        var playlistTracks = recommendations
            .Select(recommendation => new GeneratedPersonalizedPlaylistTrack(
                playlist.Id,
                recommendation.TrackId,
                position++,
                ToStoredScore(recommendation.Score),
                recommendation.Reason))
            .ToArray();

        if (playlistTracks.Length > 0)
        {
            await _dbContext.GeneratedPersonalizedPlaylistTracks
                .AddRangeAsync(playlistTracks, cancellationToken);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }

    private async Task<IReadOnlyList<ThemeToGenerate>> ResolveThemesAsync(
        CancellationToken cancellationToken)
    {
        var defaultTrackCount = Math.Clamp(
            _options.DefaultTrackCount <= 0 ? 25 : _options.DefaultTrackCount,
            1,
            Math.Max(1, _options.MaxTrackCount));
        var maxTrackCount = Math.Max(1, _options.MaxTrackCount);

        var themes = await _themeProvider.GetActiveThemesAsync(cancellationToken);

        return themes
            .Select(theme => new ThemeToGenerate(
                theme.Id,
                theme.ThemeKey,
                theme.Name,
                theme.Description,
                Math.Clamp(
                    theme.TrackCount <= 0 ? defaultTrackCount : theme.TrackCount,
                    1,
                    maxTrackCount)))
            .ToArray();
    }

    private static decimal ToStoredScore(double score)
    {
        if (double.IsNaN(score) || double.IsInfinity(score))
        {
            return 0m;
        }

        return Math.Round(
            (decimal)Math.Clamp(score, 0, 1),
            6,
            MidpointRounding.AwayFromZero);
    }

    private sealed record ThemeToGenerate(
        Guid Id,
        string ThemeKey,
        string Name,
        string Description,
        int TrackCount);
}
