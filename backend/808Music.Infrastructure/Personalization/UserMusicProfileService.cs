using _808Music.Application;
using _808Music.Application.Abstractions;
using _808Music.Domain.Catalog;
using _808Music.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace _808Music.Infrastructure.Personalization;

public sealed class UserMusicProfileService : IUserMusicProfileService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IApplicationDbContext _dbContext;
    private readonly UserMusicProfileOptions _options;
    private readonly ILogger<UserMusicProfileService> _logger;

    public UserMusicProfileService(
        IApplicationDbContext dbContext,
        IOptions<UserMusicProfileOptions> options,
        ILogger<UserMusicProfileService> logger)
    {
        _dbContext = dbContext;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<UserMusicProfile> GetOrRefreshDailyProfileAsync(
        int userId,
        DateOnly profileDate,
        CancellationToken cancellationToken = default)
    {
        if (userId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(userId), "User id must be positive.");
        }

        var sourceWindowDays = Math.Max(1, _options.SourceWindowDays);
        var windowEndUtc = profileDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc).AddDays(1);
        var windowStartUtc = windowEndUtc.AddDays(-sourceWindowDays);
        var halfLifeDays = Math.Max(1, _options.RecencyHalfLifeDays);

        var interactions = await _dbContext.UserTrackInteractions
            .AsNoTracking()
            .Where(x =>
                x.UserId == userId &&
                x.OccurredAt >= windowStartUtc &&
                x.OccurredAt < windowEndUtc)
            .OrderByDescending(x => x.OccurredAt)
            .ToListAsync(cancellationToken);

        var weightedTracks = BuildTrackWeights(interactions, windowEndUtc, halfLifeDays);
        var trackIds = weightedTracks.Keys.ToArray();
        var positiveTrackWeights = weightedTracks
            .Where(x => x.Value > 0)
            .ToDictionary(x => x.Key, x => x.Value);

        var latestAnalysesByTrack = await LoadLatestActiveAnalysesByTrackAsync(trackIds, cancellationToken);

        var embedding = BuildWeightedAverageEmbedding(latestAnalysesByTrack, positiveTrackWeights);
        var tagAffinities = await BuildTagAffinitiesAsync(
            latestAnalysesByTrack,
            weightedTracks,
            Math.Max(1, _options.MaxTagAffinities),
            cancellationToken);
        var clusterAffinities = await BuildClusterAffinitiesAsync(
            trackIds,
            weightedTracks,
            Math.Max(1, _options.MaxClusterAffinities),
            cancellationToken);
        var recentTrackIds = interactions
            .Select(x => x.TrackId)
            .Distinct()
            .Take(Math.Max(1, _options.MaxRecentTrackIds))
            .ToArray();
        var favoriteArtistIds = await BuildFavoriteArtistIdsAsync(
            positiveTrackWeights,
            Math.Max(1, _options.MaxFavoriteArtistIds),
            cancellationToken);
        var favoriteAlbumIds = await BuildFavoriteAlbumIdsAsync(
            positiveTrackWeights,
            Math.Max(1, _options.MaxFavoriteAlbumIds),
            cancellationToken);

        var embeddingJson = Serialize(embedding);
        var tagAffinitiesJson = Serialize(tagAffinities);
        var clusterAffinitiesJson = Serialize(clusterAffinities);
        var recentTrackIdsJson = Serialize(recentTrackIds);
        var favoriteArtistIdsJson = Serialize(favoriteArtistIds);
        var favoriteAlbumIdsJson = Serialize(favoriteAlbumIds);

        var cache = await _dbContext.UserMusicProfileCaches
            .FirstOrDefaultAsync(
                x => x.UserId == userId && x.ProfileDate == profileDate,
                cancellationToken);

        if (cache is null)
        {
            cache = new UserMusicProfileCache(
                userId,
                profileDate,
                interactions.Count,
                sourceWindowDays,
                embeddingJson,
                tagAffinitiesJson,
                clusterAffinitiesJson,
                recentTrackIdsJson,
                favoriteArtistIdsJson,
                favoriteAlbumIdsJson);

            _dbContext.UserMusicProfileCaches.Add(cache);
        }
        else
        {
            cache.Refresh(
                interactions.Count,
                sourceWindowDays,
                embeddingJson,
                tagAffinitiesJson,
                clusterAffinitiesJson,
                recentTrackIdsJson,
                favoriteArtistIdsJson,
                favoriteAlbumIdsJson);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return ToProfile(cache);
    }

    public async Task<UserMusicProfileRefreshResult> RefreshActiveUserProfilesAsync(
        DateOnly profileDate,
        CancellationToken cancellationToken = default)
    {
        var sourceWindowDays = Math.Max(1, _options.SourceWindowDays);
        var windowEndUtc = profileDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc).AddDays(1);
        var windowStartUtc = windowEndUtc.AddDays(-sourceWindowDays);

        var activeUserIds = await _dbContext.UserTrackInteractions
            .AsNoTracking()
            .Where(x => x.OccurredAt >= windowStartUtc && x.OccurredAt < windowEndUtc)
            .Select(x => x.UserId)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync(cancellationToken);

        var refreshedCount = 0;

        foreach (var activeUserId in activeUserIds)
        {
            try
            {
                await GetOrRefreshDailyProfileAsync(activeUserId, profileDate, cancellationToken);
                refreshedCount++;
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogError(
                    ex,
                    "Failed to refresh music profile cache for user {UserId} and date {ProfileDate}",
                    activeUserId,
                    profileDate);
            }
        }

        return new UserMusicProfileRefreshResult(profileDate, refreshedCount);
    }

    private static Dictionary<int, double> BuildTrackWeights(
        IReadOnlyCollection<UserTrackInteraction> interactions,
        DateTime windowEndUtc,
        double halfLifeDays)
    {
        var weightedTracks = new Dictionary<int, double>();

        foreach (var interaction in interactions)
        {
            var baseWeight = GetInteractionWeight(interaction.InteractionType);
            if (baseWeight == 0)
            {
                continue;
            }

            var ageDays = Math.Max(0, (windowEndUtc - interaction.OccurredAt).TotalDays);
            var recencyWeight = Math.Pow(0.5, ageDays / halfLifeDays);
            var score = baseWeight * recencyWeight;

            if (weightedTracks.TryGetValue(interaction.TrackId, out var existingScore))
            {
                weightedTracks[interaction.TrackId] = existingScore + score;
            }
            else
            {
                weightedTracks[interaction.TrackId] = score;
            }
        }

        return weightedTracks;
    }

    private async Task<IReadOnlyDictionary<int, ActiveAnalysis>> LoadLatestActiveAnalysesByTrackAsync(
        IReadOnlyCollection<int> trackIds,
        CancellationToken cancellationToken)
    {
        if (trackIds.Count == 0)
        {
            return new Dictionary<int, ActiveAnalysis>();
        }

        var analyses = await _dbContext.TrackAudioAnalyses
            .AsNoTracking()
            .Where(x =>
                trackIds.Contains(x.TrackId) &&
                x.IsActive &&
                x.Status == AudioAnalysisStatus.Ready)
            .Select(x => new ActiveAnalysis(
                x.Id,
                x.TrackId,
                x.EmbeddingJson,
                x.CompletedAt,
                x.CreatedAt))
            .ToListAsync(cancellationToken);

        return analyses
            .GroupBy(x => x.TrackId)
            .ToDictionary(
                x => x.Key,
                x => x
                    .OrderByDescending(analysis => analysis.CompletedAt ?? analysis.CreatedAt)
                    .First());
    }

    private static double[] BuildWeightedAverageEmbedding(
        IReadOnlyDictionary<int, ActiveAnalysis> analysesByTrack,
        IReadOnlyDictionary<int, double> positiveTrackWeights)
    {
        double[]? weightedSums = null;
        double totalWeight = 0;

        foreach (var (trackId, trackWeight) in positiveTrackWeights)
        {
            if (!analysesByTrack.TryGetValue(trackId, out var analysis) ||
                string.IsNullOrWhiteSpace(analysis.EmbeddingJson))
            {
                continue;
            }

            var embedding = TryDeserializeEmbedding(analysis.EmbeddingJson);
            if (embedding is null || embedding.Length == 0)
            {
                continue;
            }

            if (weightedSums is null)
            {
                weightedSums = new double[embedding.Length];
            }

            if (embedding.Length != weightedSums.Length)
            {
                continue;
            }

            for (var i = 0; i < embedding.Length; i++)
            {
                weightedSums[i] += embedding[i] * trackWeight;
            }

            totalWeight += trackWeight;
        }

        if (weightedSums is null || totalWeight <= 0)
        {
            return [];
        }

        return weightedSums
            .Select(x => RoundScore(x / totalWeight))
            .ToArray();
    }

    private async Task<TagAffinity[]> BuildTagAffinitiesAsync(
        IReadOnlyDictionary<int, ActiveAnalysis> analysesByTrack,
        IReadOnlyDictionary<int, double> weightedTracks,
        int maxItems,
        CancellationToken cancellationToken)
    {
        var analysisTrackLookup = analysesByTrack.Values
            .ToDictionary(x => x.Id, x => x.TrackId);

        if (analysisTrackLookup.Count == 0)
        {
            return [];
        }

        var analysisIds = analysisTrackLookup.Keys.ToArray();
        var tags = await _dbContext.TrackAudioTags
            .AsNoTracking()
            .Where(x => analysisIds.Contains(x.TrackAudioAnalysisId))
            .Select(x => new TagProjection(
                x.TrackAudioAnalysisId,
                x.Namespace,
                x.Label,
                x.Score))
            .ToListAsync(cancellationToken);

        var scores = new Dictionary<(string Namespace, string Label), double>();

        foreach (var tag in tags)
        {
            if (!analysisTrackLookup.TryGetValue(tag.TrackAudioAnalysisId, out var trackId) ||
                !weightedTracks.TryGetValue(trackId, out var trackWeight))
            {
                continue;
            }

            var key = (tag.Namespace, tag.Label);
            var score = trackWeight * (double)tag.Score;

            if (scores.TryGetValue(key, out var existingScore))
            {
                scores[key] = existingScore + score;
            }
            else
            {
                scores[key] = score;
            }
        }

        return scores
            .Where(x => Math.Abs(x.Value) > 0.000001)
            .OrderByDescending(x => Math.Abs(x.Value))
            .Take(maxItems)
            .Select(x => new TagAffinity(x.Key.Namespace, x.Key.Label, RoundScore(x.Value)))
            .ToArray();
    }

    private async Task<ClusterAffinity[]> BuildClusterAffinitiesAsync(
        IReadOnlyCollection<int> trackIds,
        IReadOnlyDictionary<int, double> weightedTracks,
        int maxItems,
        CancellationToken cancellationToken)
    {
        if (trackIds.Count == 0)
        {
            return [];
        }

        var activeRunIds = await _dbContext.AudioClusterRuns
            .AsNoTracking()
            .Where(x => x.IsActive && x.Status == AudioClusterRunStatus.Ready)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        if (activeRunIds.Count == 0)
        {
            return [];
        }

        var assignments = await _dbContext.TrackClusterAssignments
            .AsNoTracking()
            .Where(x =>
                trackIds.Contains(x.TrackId) &&
                activeRunIds.Contains(x.ClusterRunId) &&
                !x.IsNoise)
            .Select(x => new ClusterAssignmentProjection(
                x.ClusterRunId,
                x.TrackId,
                x.ClusterKey,
                x.MembershipScore))
            .ToListAsync(cancellationToken);

        var scores = new Dictionary<(Guid ClusterRunId, string ClusterKey), double>();

        foreach (var assignment in assignments)
        {
            if (!weightedTracks.TryGetValue(assignment.TrackId, out var trackWeight))
            {
                continue;
            }

            var membershipStrength = assignment.MembershipScore is null
                ? 1
                : (double)assignment.MembershipScore.Value;
            var key = (assignment.ClusterRunId, assignment.ClusterKey);
            var score = trackWeight * membershipStrength;

            if (scores.TryGetValue(key, out var existingScore))
            {
                scores[key] = existingScore + score;
            }
            else
            {
                scores[key] = score;
            }
        }

        return scores
            .Where(x => Math.Abs(x.Value) > 0.000001)
            .OrderByDescending(x => Math.Abs(x.Value))
            .Take(maxItems)
            .Select(x => new ClusterAffinity(
                x.Key.ClusterRunId,
                x.Key.ClusterKey,
                RoundScore(x.Value)))
            .ToArray();
    }

    private async Task<FavoriteArtist[]> BuildFavoriteArtistIdsAsync(
        IReadOnlyDictionary<int, double> positiveTrackWeights,
        int maxItems,
        CancellationToken cancellationToken)
    {
        if (positiveTrackWeights.Count == 0)
        {
            return [];
        }

        var trackIds = positiveTrackWeights.Keys.ToArray();
        var artistTracks = await _dbContext.ArtistTracks
            .AsNoTracking()
            .Where(x => trackIds.Contains(x.TrackId))
            .Select(x => new ArtistTrackProjection(
                x.TrackId,
                x.ArtistId,
                x.IsLead))
            .ToListAsync(cancellationToken);

        var scores = new Dictionary<int, double>();

        foreach (var artistTrack in artistTracks)
        {
            if (!positiveTrackWeights.TryGetValue(artistTrack.TrackId, out var trackWeight))
            {
                continue;
            }

            var artistWeight = artistTrack.IsLead ? 1 : 0.75;
            var score = trackWeight * artistWeight;

            if (scores.TryGetValue(artistTrack.ArtistId, out var existingScore))
            {
                scores[artistTrack.ArtistId] = existingScore + score;
            }
            else
            {
                scores[artistTrack.ArtistId] = score;
            }
        }

        return scores
            .Where(x => x.Value > 0)
            .OrderByDescending(x => x.Value)
            .Take(maxItems)
            .Select(x => new FavoriteArtist(x.Key, RoundScore(x.Value)))
            .ToArray();
    }

    private async Task<FavoriteAlbum[]> BuildFavoriteAlbumIdsAsync(
        IReadOnlyDictionary<int, double> positiveTrackWeights,
        int maxItems,
        CancellationToken cancellationToken)
    {
        if (positiveTrackWeights.Count == 0)
        {
            return [];
        }

        var trackIds = positiveTrackWeights.Keys.ToArray();
        var trackAlbums = await _dbContext.Tracks
            .AsNoTracking()
            .Where(x => trackIds.Contains(x.Id) && x.AlbumId != null)
            .Select(x => new TrackAlbumProjection(
                x.Id,
                x.AlbumId ?? 0))
            .ToListAsync(cancellationToken);

        var scores = new Dictionary<int, double>();

        foreach (var trackAlbum in trackAlbums)
        {
            if (trackAlbum.AlbumId <= 0 ||
                !positiveTrackWeights.TryGetValue(trackAlbum.TrackId, out var trackWeight))
            {
                continue;
            }

            if (scores.TryGetValue(trackAlbum.AlbumId, out var existingScore))
            {
                scores[trackAlbum.AlbumId] = existingScore + trackWeight;
            }
            else
            {
                scores[trackAlbum.AlbumId] = trackWeight;
            }
        }

        return scores
            .Where(x => x.Value > 0)
            .OrderByDescending(x => x.Value)
            .Take(maxItems)
            .Select(x => new FavoriteAlbum(x.Key, RoundScore(x.Value)))
            .ToArray();
    }

    private static double GetInteractionWeight(UserTrackInteractionType interactionType)
    {
        return interactionType switch
        {
            UserTrackInteractionType.Liked => 4,
            UserTrackInteractionType.AddedToPlaylist => 3,
            UserTrackInteractionType.PlayCompleted => 2,
            UserTrackInteractionType.PlayStarted => 0.5,
            UserTrackInteractionType.Skipped => -1.5,
            UserTrackInteractionType.Unliked => -3,
            UserTrackInteractionType.RemovedFromPlaylist => -3,
            _ => 0
        };
    }

    private static double[]? TryDeserializeEmbedding(string embeddingJson)
    {
        try
        {
            return JsonSerializer.Deserialize<double[]>(embeddingJson, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static UserMusicProfile ToProfile(UserMusicProfileCache cache)
    {
        return new UserMusicProfile(
            cache.Id,
            cache.UserId,
            cache.ProfileDate,
            cache.GeneratedAt,
            cache.SourceInteractionCount,
            cache.SourceWindowDays,
            cache.EmbeddingJson,
            cache.TagAffinitiesJson,
            cache.ClusterAffinitiesJson,
            cache.RecentTrackIdsJson,
            cache.FavoriteArtistIdsJson,
            cache.FavoriteAlbumIdsJson);
    }

    private static string Serialize<T>(T value)
    {
        return JsonSerializer.Serialize(value, JsonOptions);
    }

    private static double RoundScore(double value)
    {
        return Math.Round(value, 8, MidpointRounding.AwayFromZero);
    }

    private sealed record ActiveAnalysis(
        Guid Id,
        int TrackId,
        string? EmbeddingJson,
        DateTime? CompletedAt,
        DateTime CreatedAt);

    private sealed record TagProjection(
        Guid TrackAudioAnalysisId,
        string Namespace,
        string Label,
        decimal Score);

    private sealed record ClusterAssignmentProjection(
        Guid ClusterRunId,
        int TrackId,
        string ClusterKey,
        decimal? MembershipScore);

    private sealed record ArtistTrackProjection(
        int TrackId,
        int ArtistId,
        bool IsLead);

    private sealed record TrackAlbumProjection(
        int TrackId,
        int AlbumId);

    private sealed record TagAffinity(
        string Namespace,
        string Label,
        double Score);

    private sealed record ClusterAffinity(
        Guid ClusterRunId,
        string ClusterKey,
        double Score);

    private sealed record FavoriteArtist(
        int ArtistId,
        double Score);

    private sealed record FavoriteAlbum(
        int AlbumId,
        double Score);
}
