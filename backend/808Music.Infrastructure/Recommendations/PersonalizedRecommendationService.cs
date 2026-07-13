using _808Music.Application;
using _808Music.Application.Abstractions;
using _808Music.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace _808Music.Infrastructure.Recommendations;

public sealed class PersonalizedRecommendationService : IPersonalizedRecommendationService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IApplicationDbContext _dbContext;
    private readonly IPersonalizedPlaylistThemeProvider _themeProvider;
    private readonly PersonalizedRecommendationOptions _options;

    public PersonalizedRecommendationService(
        IApplicationDbContext dbContext,
        IPersonalizedPlaylistThemeProvider themeProvider,
        IOptions<PersonalizedRecommendationOptions> options)
    {
        _dbContext = dbContext;
        _themeProvider = themeProvider;
        _options = options.Value;
    }

    public async Task<IReadOnlyList<PersonalizedRecommendation>> GetRecommendationsAsync(
        PersonalizedRecommendationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var limit = Math.Clamp(request.Limit <= 0 ? 25 : request.Limit, 1, 100);
        var seedTrackIds = request.SeedTrackIds
            .Where(x => x > 0)
            .Distinct()
            .ToArray();
        var excludedTrackIds = request.ExcludedTrackIds
            .Where(x => x > 0)
            .Distinct()
            .ToHashSet();

        var profile = await LoadProfileAsync(request.UserId, cancellationToken);
        var seedAnalysesByTrack = await LoadLatestActiveAnalysesByTrackAsync(seedTrackIds, cancellationToken);
        var seedTagsByTrack = await LoadTagsByTrackAsync(seedAnalysesByTrack, cancellationToken);
        var seedClustersByTrack = await LoadClustersByTrackAsync(seedTrackIds, cancellationToken);
        var seedContext = BuildSeedContext(
            seedTrackIds,
            seedAnalysesByTrack,
            seedTagsByTrack,
            seedClustersByTrack,
            request.Intent);
        var theme = string.IsNullOrWhiteSpace(request.ThemeKey)
            ? null
            : await _themeProvider.FindByKeyAsync(request.ThemeKey, cancellationToken);
        var candidateTrackIds = await LoadCandidateTrackIdsAsync(
            excludedTrackIds,
            profile,
            seedContext,
            theme,
            cancellationToken);
        var trackProjections = await LoadCandidateTracksByIdsAsync(candidateTrackIds, cancellationToken);
        if (trackProjections.Count == 0)
        {
            return [];
        }

        var allRelevantTrackIds = trackProjections
            .Select(x => x.Id)
            .Concat(seedTrackIds)
            .Distinct()
            .ToArray();
        var analysesByTrack = await LoadLatestActiveAnalysesByTrackAsync(allRelevantTrackIds, cancellationToken);
        var tagsByTrack = await LoadTagsByTrackAsync(analysesByTrack, cancellationToken);
        var clustersByTrack = await LoadClustersByTrackAsync(allRelevantTrackIds, cancellationToken);
        var artistIdsByTrack = await LoadArtistIdsByTrackAsync(
            trackProjections.Select(x => x.Id).ToArray(),
            cancellationToken);

        var maxStreams = Math.Max(1, trackProjections.Max(x => x.Streams));
        var maxTrackId = Math.Max(1, trackProjections.Max(x => x.Id));

        var scoredCandidates = trackProjections
            .Select(track => ScoreCandidate(
                track,
                request.Intent,
                profile,
                seedContext,
                theme,
                tagsByTrack.GetValueOrDefault(track.Id, TrackTags.Empty),
                clustersByTrack.GetValueOrDefault(track.Id, TrackClusters.Empty),
                analysesByTrack.GetValueOrDefault(track.Id),
                artistIdsByTrack.GetValueOrDefault(track.Id, []),
                maxStreams,
                maxTrackId,
                limit))
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .ThenByDescending(x => x.FreshnessPopularity)
            .ThenBy(x => x.TrackId)
            .ToArray();

        return ApplyDiversity(scoredCandidates, limit);
    }

    private async Task<CachedUserProfile> LoadProfileAsync(
        int userId,
        CancellationToken cancellationToken)
    {
        if (userId <= 0)
        {
            return CachedUserProfile.Empty;
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var cache = await _dbContext.UserMusicProfileCaches
            .AsNoTracking()
            .Where(x => x.UserId == userId && x.ProfileDate <= today)
            .OrderByDescending(x => x.ProfileDate)
            .FirstOrDefaultAsync(cancellationToken);

        if (cache is null)
        {
            return CachedUserProfile.Empty;
        }

        var embedding = DeserializeOrEmpty<double[]>(cache.EmbeddingJson);
        var recentTrackIds = DeserializeOrEmpty<int[]>(cache.RecentTrackIdsJson).ToHashSet();
        var tagAffinities = DeserializeOrEmpty<TagAffinityProjection[]>(cache.TagAffinitiesJson)
            .GroupBy(x => NormalizeTag(x.Label))
            .Where(x => !string.IsNullOrWhiteSpace(x.Key))
            .ToDictionary(
                x => x.Key,
                x => x.Sum(tag => tag.Score));
        var clusterAffinities = DeserializeOrEmpty<ClusterAffinityProjection[]>(cache.ClusterAffinitiesJson)
            .GroupBy(x => CreateClusterLookupKey(x.ClusterRunId, x.ClusterKey))
            .Where(x => !string.IsNullOrWhiteSpace(x.Key))
            .ToDictionary(
                x => x.Key,
                x => x.Sum(cluster => cluster.Score));

        return new CachedUserProfile(embedding, tagAffinities, clusterAffinities, recentTrackIds);
    }

    private async Task<IReadOnlyList<int>> LoadCandidateTrackIdsAsync(
        IReadOnlySet<int> excludedTrackIds,
        CachedUserProfile profile,
        SeedContext seedContext,
        PersonalizedPlaylistThemeDefinition? theme,
        CancellationToken cancellationToken)
    {
        var maxCandidates = Math.Max(100, _options.MaxCandidateTracks);
        var perSignalTake = Math.Max(25, maxCandidates / 4);
        var candidateIds = new List<int>(maxCandidates);
        var seenCandidateIds = new HashSet<int>();

        await AddCandidateIdsAsync(
            candidateIds,
            seenCandidateIds,
            LoadPopularCandidateIdsAsync(excludedTrackIds, perSignalTake, cancellationToken),
            maxCandidates);
        await AddCandidateIdsAsync(
            candidateIds,
            seenCandidateIds,
            LoadClusterCandidateIdsAsync(excludedTrackIds, profile, seedContext, perSignalTake, cancellationToken),
            maxCandidates);
        await AddCandidateIdsAsync(
            candidateIds,
            seenCandidateIds,
            LoadTagCandidateIdsAsync(excludedTrackIds, profile, seedContext, theme, perSignalTake, cancellationToken),
            maxCandidates);
        await AddCandidateIdsAsync(
            candidateIds,
            seenCandidateIds,
            LoadNearestEmbeddingCandidateIdsAsync(excludedTrackIds, profile, seedContext, perSignalTake, cancellationToken),
            maxCandidates);

        if (candidateIds.Count < maxCandidates)
        {
            await AddCandidateIdsAsync(
                candidateIds,
                seenCandidateIds,
                LoadPopularCandidateIdsAsync(excludedTrackIds, maxCandidates, cancellationToken),
                maxCandidates);
        }

        return candidateIds;
    }

    private static async Task AddCandidateIdsAsync(
        ICollection<int> candidateIds,
        ISet<int> seenCandidateIds,
        Task<IReadOnlyList<int>> candidateIdTask,
        int maxCandidates)
    {
        var sourceCandidateIds = await candidateIdTask;

        foreach (var candidateId in sourceCandidateIds)
        {
            if (candidateIds.Count >= maxCandidates)
            {
                break;
            }

            if (seenCandidateIds.Add(candidateId))
            {
                candidateIds.Add(candidateId);
            }
        }
    }

    private async Task<IReadOnlyList<int>> LoadPopularCandidateIdsAsync(
        IReadOnlySet<int> excludedTrackIds,
        int take,
        CancellationToken cancellationToken)
    {
        return await _dbContext.Tracks
            .AsNoTracking()
            .Where(x => !excludedTrackIds.Contains(x.Id))
            .OrderByDescending(x => x.Streams)
            .ThenByDescending(x => x.Id)
            .Take(take)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<int>> LoadClusterCandidateIdsAsync(
        IReadOnlySet<int> excludedTrackIds,
        CachedUserProfile profile,
        SeedContext seedContext,
        int take,
        CancellationToken cancellationToken)
    {
        var desiredClusterKeys = seedContext.ClusterLookupKeys
            .Concat(profile.ClusterAffinities
                .Where(x => x.Value > 0)
                .OrderByDescending(x => x.Value)
                .Take(25)
                .Select(x => x.Key))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (desiredClusterKeys.Count == 0)
        {
            return [];
        }

        var assignments = await (
            from assignment in _dbContext.TrackClusterAssignments.AsNoTracking()
            join run in _dbContext.AudioClusterRuns.AsNoTracking()
                on assignment.ClusterRunId equals run.Id
            where
                run.IsActive &&
                run.Status == AudioClusterRunStatus.Ready &&
                !assignment.IsNoise &&
                !excludedTrackIds.Contains(assignment.TrackId)
            select new TrackClusterProjection(
                assignment.TrackId,
                assignment.ClusterRunId,
                assignment.ClusterKey,
                assignment.MembershipScore))
            .ToListAsync(cancellationToken);

        return assignments
            .Where(x => desiredClusterKeys.Contains(CreateClusterLookupKey(x.ClusterRunId, x.ClusterKey)))
            .GroupBy(x => x.TrackId)
            .Select(x => new
            {
                TrackId = x.Key,
                Score = x.Sum(assignment => assignment.MembershipScore is null ? 1 : (double)assignment.MembershipScore.Value)
            })
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.TrackId)
            .Take(take)
            .Select(x => x.TrackId)
            .ToArray();
    }

    private async Task<IReadOnlyList<int>> LoadTagCandidateIdsAsync(
        IReadOnlySet<int> excludedTrackIds,
        CachedUserProfile profile,
        SeedContext seedContext,
        PersonalizedPlaylistThemeDefinition? theme,
        int take,
        CancellationToken cancellationToken)
    {
        var desiredTagKeys = seedContext.TagScores.Keys
            .Concat(profile.TagAffinities
                .Where(x => x.Value > 0)
                .OrderByDescending(x => x.Value)
                .Take(50)
                .Select(x => x.Key))
            .Concat(theme?.PositiveLabels
                .Where(x => x.Source == PersonalizedPlaylistThemeLabelSource.EssentiaTag)
                .Select(x => NormalizeTag(x.Label)) ?? [])
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (desiredTagKeys.Count == 0)
        {
            return [];
        }

        var tags = await (
            from tag in _dbContext.TrackAudioTags.AsNoTracking()
            join analysis in _dbContext.TrackAudioAnalyses.AsNoTracking()
                on tag.TrackAudioAnalysisId equals analysis.Id
            where
                analysis.IsActive &&
                analysis.Status == AudioAnalysisStatus.Ready &&
                !excludedTrackIds.Contains(analysis.TrackId)
            select new TrackTagCandidateProjection(
                analysis.TrackId,
                tag.Label,
                tag.Score))
            .ToListAsync(cancellationToken);

        return tags
            .Where(x => desiredTagKeys.Contains(NormalizeTag(x.Label)))
            .GroupBy(x => x.TrackId)
            .Select(x => new
            {
                TrackId = x.Key,
                Score = x.Sum(tag => (double)tag.Score)
            })
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.TrackId)
            .Take(take)
            .Select(x => x.TrackId)
            .ToArray();
    }

    private async Task<IReadOnlyList<int>> LoadNearestEmbeddingCandidateIdsAsync(
        IReadOnlySet<int> excludedTrackIds,
        CachedUserProfile profile,
        SeedContext seedContext,
        int take,
        CancellationToken cancellationToken)
    {
        var referenceEmbedding = seedContext.Embedding.Length > 0
            ? seedContext.Embedding
            : profile.Embedding;

        if (referenceEmbedding.Length == 0)
        {
            return [];
        }

        var analyses = await _dbContext.TrackAudioAnalyses
            .AsNoTracking()
            .Where(x =>
                x.IsActive &&
                x.Status == AudioAnalysisStatus.Ready &&
                x.EmbeddingJson != null &&
                !excludedTrackIds.Contains(x.TrackId))
            .Select(x => new ActiveAnalysis(
                x.Id,
                x.TrackId,
                x.EmbeddingJson,
                x.CompletedAt,
                x.CreatedAt))
            .ToListAsync(cancellationToken);

        return analyses
            .GroupBy(x => x.TrackId)
            .Select(x => x
                .OrderByDescending(analysis => analysis.CompletedAt ?? analysis.CreatedAt)
                .First())
            .Select(x => new
            {
                x.TrackId,
                Score = CalculateEmbeddingSimilarity(
                    TryDeserializeEmbedding(x.EmbeddingJson),
                    referenceEmbedding)
            })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.TrackId)
            .Take(take)
            .Select(x => x.TrackId)
            .ToArray();
    }

    private async Task<IReadOnlyList<TrackProjection>> LoadCandidateTracksByIdsAsync(
        IReadOnlyCollection<int> candidateTrackIds,
        CancellationToken cancellationToken)
    {
        if (candidateTrackIds.Count == 0)
        {
            return [];
        }

        return await _dbContext.Tracks
            .AsNoTracking()
            .Where(x => candidateTrackIds.Contains(x.Id))
            .Select(x => new TrackProjection(
                x.Id,
                x.Streams,
                x.AlbumId))
            .ToListAsync(cancellationToken);
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

    private async Task<IReadOnlyDictionary<int, TrackTags>> LoadTagsByTrackAsync(
        IReadOnlyDictionary<int, ActiveAnalysis> analysesByTrack,
        CancellationToken cancellationToken)
    {
        if (analysesByTrack.Count == 0)
        {
            return new Dictionary<int, TrackTags>();
        }

        var analysisTrackLookup = analysesByTrack.Values
            .ToDictionary(x => x.Id, x => x.TrackId);
        var analysisIds = analysisTrackLookup.Keys.ToArray();
        var tags = await _dbContext.TrackAudioTags
            .AsNoTracking()
            .Where(x => analysisIds.Contains(x.TrackAudioAnalysisId))
            .Select(x => new TrackTagProjection(
                x.TrackAudioAnalysisId,
                x.Label,
                x.Score))
            .ToListAsync(cancellationToken);

        var tagsByTrack = new Dictionary<int, Dictionary<string, TrackTag>>();

        foreach (var tag in tags)
        {
            if (!analysisTrackLookup.TryGetValue(tag.TrackAudioAnalysisId, out var trackId))
            {
                continue;
            }

            var normalizedLabel = NormalizeTag(tag.Label);
            if (string.IsNullOrWhiteSpace(normalizedLabel))
            {
                continue;
            }

            if (!tagsByTrack.TryGetValue(trackId, out var trackTags))
            {
                trackTags = new Dictionary<string, TrackTag>();
                tagsByTrack[trackId] = trackTags;
            }

            var score = (double)tag.Score;
            if (!trackTags.TryGetValue(normalizedLabel, out var existingTag) ||
                existingTag.Score < score)
            {
                trackTags[normalizedLabel] = new TrackTag(tag.Label, score);
            }
        }

        return tagsByTrack.ToDictionary(
            x => x.Key,
            x => new TrackTags(x.Value));
    }

    private async Task<IReadOnlyDictionary<int, TrackClusters>> LoadClustersByTrackAsync(
        IReadOnlyCollection<int> trackIds,
        CancellationToken cancellationToken)
    {
        if (trackIds.Count == 0)
        {
            return new Dictionary<int, TrackClusters>();
        }

        var activeRunIds = await _dbContext.AudioClusterRuns
            .AsNoTracking()
            .Where(x => x.IsActive && x.Status == AudioClusterRunStatus.Ready)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        if (activeRunIds.Count == 0)
        {
            return new Dictionary<int, TrackClusters>();
        }

        var assignments = await _dbContext.TrackClusterAssignments
            .AsNoTracking()
            .Where(x =>
                trackIds.Contains(x.TrackId) &&
                activeRunIds.Contains(x.ClusterRunId) &&
                !x.IsNoise)
            .Select(x => new TrackClusterProjection(
                x.TrackId,
                x.ClusterRunId,
                x.ClusterKey,
                x.MembershipScore))
            .ToListAsync(cancellationToken);

        return assignments
            .GroupBy(x => x.TrackId)
            .ToDictionary(
                x => x.Key,
                x => new TrackClusters(
                    x.Select(assignment => new TrackCluster(
                            CreateClusterLookupKey(assignment.ClusterRunId, assignment.ClusterKey),
                            assignment.ClusterKey,
                            assignment.MembershipScore is null ? 1 : (double)assignment.MembershipScore.Value))
                        .ToArray()));
    }

    private async Task<IReadOnlyDictionary<int, int[]>> LoadArtistIdsByTrackAsync(
        IReadOnlyCollection<int> trackIds,
        CancellationToken cancellationToken)
    {
        if (trackIds.Count == 0)
        {
            return new Dictionary<int, int[]>();
        }

        var artistTracks = await _dbContext.ArtistTracks
            .AsNoTracking()
            .Where(x => trackIds.Contains(x.TrackId))
            .Select(x => new ArtistTrackProjection(x.TrackId, x.ArtistId, x.IsLead))
            .ToListAsync(cancellationToken);

        return artistTracks
            .GroupBy(x => x.TrackId)
            .ToDictionary(
                x => x.Key,
                x => x
                    .OrderByDescending(artistTrack => artistTrack.IsLead)
                    .ThenBy(artistTrack => artistTrack.ArtistId)
                    .Select(artistTrack => artistTrack.ArtistId)
                    .Distinct()
                    .ToArray());
    }

    private static SeedContext BuildSeedContext(
        IReadOnlyList<int> seedTrackIds,
        IReadOnlyDictionary<int, ActiveAnalysis> analysesByTrack,
        IReadOnlyDictionary<int, TrackTags> tagsByTrack,
        IReadOnlyDictionary<int, TrackClusters> clustersByTrack,
        PersonalizedRecommendationIntent intent)
    {
        if (seedTrackIds.Count == 0)
        {
            return SeedContext.Empty;
        }

        var seedWeights = seedTrackIds
            .Select((trackId, index) => new
                WeightedTrack(
                    trackId,
                    intent == PersonalizedRecommendationIntent.Autoplay
                    ? index + 1d
                    : 1d))
            .ToArray();

        var embedding = BuildWeightedEmbedding(
            seedWeights,
            analysesByTrack);

        var tagScores = new Dictionary<string, double>();
        var clusterKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var seed in seedWeights)
        {
            if (tagsByTrack.TryGetValue(seed.TrackId, out var tags))
            {
                foreach (var (normalizedLabel, tag) in tags.Values)
                {
                    tagScores[normalizedLabel] = tagScores.GetValueOrDefault(normalizedLabel) + tag.Score * seed.Weight;
                }
            }

            if (clustersByTrack.TryGetValue(seed.TrackId, out var clusters))
            {
                foreach (var cluster in clusters.Values)
                {
                    clusterKeys.Add(cluster.LookupKey);
                }
            }
        }

        return new SeedContext(embedding, tagScores, clusterKeys);
    }

    private ScoredCandidate ScoreCandidate(
        TrackProjection track,
        PersonalizedRecommendationIntent intent,
        CachedUserProfile profile,
        SeedContext seedContext,
        PersonalizedPlaylistThemeDefinition? theme,
        TrackTags tags,
        TrackClusters clusters,
        ActiveAnalysis? analysis,
        int[] artistIds,
        int maxStreams,
        int maxTrackId,
        int limit)
    {
        var embedding = TryDeserializeEmbedding(analysis?.EmbeddingJson);
        var profileEmbeddingSimilarity = CalculateEmbeddingSimilarity(embedding, profile.Embedding);
        var profileTagScore = CalculateAffinityScore(tags, profile.TagAffinities);
        var profileClusterScore = CalculateClusterAffinityScore(clusters, profile.ClusterAffinities);
        var userProfile = AverageAvailable(profileEmbeddingSimilarity, profileTagScore, profileClusterScore);

        var sharedTags = CalculateTagOverlap(tags, seedContext.TagScores);
        var clusterMatch = CalculateClusterMatch(clusters, seedContext.ClusterLookupKeys);
        var seedEmbeddingSimilarity = CalculateEmbeddingSimilarity(embedding, seedContext.Embedding);
        var seedSimilarity = AverageAvailable(seedEmbeddingSimilarity, sharedTags, clusterMatch);
        var smoothContinuation = AverageAvailable(sharedTags, clusterMatch);
        var tagClusterAffinity = AverageAvailable(profileTagScore, profileClusterScore);
        var themeMatch = CalculateThemeMatch(tags, theme);
        var freshnessPopularity = CalculateFreshnessPopularity(track, maxStreams, maxTrackId);
        var novelty = profile.RecentTrackIds.Contains(track.Id) ? 0.2 : 1;
        var score = CalculateIntentScore(
            intent,
            userProfile,
            tagClusterAffinity,
            freshnessPopularity,
            novelty,
            seedSimilarity,
            sharedTags,
            clusterMatch,
            smoothContinuation,
            themeMatch,
            profileClusterScore);

        if (score <= 0.000001)
        {
            score = 0.05 + freshnessPopularity * 0.2;
        }

        score *= novelty;

        var matchedTags = BuildMatchedTags(
            tags,
            profile,
            seedContext,
            theme,
            Math.Max(1, _options.MaxMatchedTags));
        var sourceSignals = new Dictionary<string, double>
        {
            ["userProfile"] = RoundScore(userProfile),
            ["tagClusterAffinity"] = RoundScore(tagClusterAffinity),
            ["freshnessPopularity"] = RoundScore(freshnessPopularity),
            ["novelty"] = RoundScore(novelty),
            ["seedSimilarity"] = RoundScore(seedSimilarity),
            ["sharedTags"] = RoundScore(sharedTags),
            ["clusterMatch"] = RoundScore(clusterMatch),
            ["smoothContinuation"] = RoundScore(smoothContinuation),
            ["themeMatch"] = RoundScore(themeMatch)
        };
        var primaryClusterKey = clusters.Values
            .OrderByDescending(x => x.MembershipScore)
            .Select(x => x.ClusterKey)
            .FirstOrDefault();

        return new ScoredCandidate(
            track.Id,
            track.AlbumId,
            artistIds,
            RoundScore(score),
            BuildReason(intent, sourceSignals, matchedTags, theme),
            matchedTags,
            primaryClusterKey,
            sourceSignals,
            freshnessPopularity,
            profile.RecentTrackIds.Contains(track.Id));
    }

    private static double CalculateIntentScore(
        PersonalizedRecommendationIntent intent,
        double userProfile,
        double tagClusterAffinity,
        double freshnessPopularity,
        double novelty,
        double seedSimilarity,
        double sharedTags,
        double clusterMatch,
        double smoothContinuation,
        double themeMatch,
        double profileClusterScore)
    {
        return intent switch
        {
            PersonalizedRecommendationIntent.SongRadio =>
                seedSimilarity * 0.50 +
                sharedTags * 0.25 +
                clusterMatch * 0.15 +
                userProfile * 0.10,

            PersonalizedRecommendationIntent.Autoplay =>
                seedSimilarity * 0.45 +
                userProfile * 0.25 +
                smoothContinuation * 0.20 +
                novelty * 0.10,

            PersonalizedRecommendationIntent.DailyThematicPlaylist =>
                userProfile * 0.40 +
                themeMatch * 0.30 +
                profileClusterScore * 0.15 +
                freshnessPopularity * 0.15,

            _ =>
                userProfile * 0.45 +
                tagClusterAffinity * 0.30 +
                freshnessPopularity * 0.15 +
                novelty * 0.10
        };
    }

    private IReadOnlyList<PersonalizedRecommendation> ApplyDiversity(
        IReadOnlyList<ScoredCandidate> candidates,
        int limit)
    {
        var selected = new List<ScoredCandidate>(limit);
        var selectedTrackIds = new HashSet<int>();
        var artistCounts = new Dictionary<int, int>();
        var albumCounts = new Dictionary<int, int>();
        var sameArtistLimit = Math.Max(1, _options.SameArtistLimit);
        var sameAlbumLimit = Math.Max(1, _options.SameAlbumLimit);

        foreach (var candidate in candidates)
        {
            TryAddCandidate(
                candidate,
                selected,
                selectedTrackIds,
                artistCounts,
                albumCounts,
                sameArtistLimit,
                sameAlbumLimit,
                enforceRecentlyPlayedFilter: true,
                enforceArtistAndAlbumCaps: true);

            if (selected.Count >= limit)
            {
                break;
            }
        }

        foreach (var candidate in candidates)
        {
            TryAddCandidate(
                candidate,
                selected,
                selectedTrackIds,
                artistCounts,
                albumCounts,
                sameArtistLimit,
                sameAlbumLimit,
                enforceRecentlyPlayedFilter: false,
                enforceArtistAndAlbumCaps: true);

            if (selected.Count >= limit)
            {
                break;
            }
        }

        foreach (var candidate in candidates)
        {
            TryAddCandidate(
                candidate,
                selected,
                selectedTrackIds,
                artistCounts,
                albumCounts,
                sameArtistLimit,
                sameAlbumLimit,
                enforceRecentlyPlayedFilter: false,
                enforceArtistAndAlbumCaps: false);

            if (selected.Count >= limit)
            {
                break;
            }
        }

        return selected
            .Take(limit)
            .Select(x => new PersonalizedRecommendation(
                x.TrackId,
                x.Score,
                x.Reason,
                x.MatchedTags,
                x.ClusterKey,
                x.SourceSignals))
            .ToArray();
    }

    private static void TryAddCandidate(
        ScoredCandidate candidate,
        ICollection<ScoredCandidate> selected,
        ISet<int> selectedTrackIds,
        IDictionary<int, int> artistCounts,
        IDictionary<int, int> albumCounts,
        int sameArtistLimit,
        int sameAlbumLimit,
        bool enforceRecentlyPlayedFilter,
        bool enforceArtistAndAlbumCaps)
    {
        if (selectedTrackIds.Contains(candidate.TrackId))
        {
            return;
        }

        if (enforceRecentlyPlayedFilter && candidate.IsRecentlyPlayed)
        {
            return;
        }

        if (enforceArtistAndAlbumCaps)
        {
            if (candidate.ArtistIds.Any(artistId => GetCount(artistCounts, artistId) >= sameArtistLimit))
            {
                return;
            }

            if (candidate.AlbumId is not null &&
                GetCount(albumCounts, candidate.AlbumId.Value) >= sameAlbumLimit)
            {
                return;
            }
        }

        selected.Add(candidate);
        selectedTrackIds.Add(candidate.TrackId);

        foreach (var artistId in candidate.ArtistIds)
        {
            artistCounts[artistId] = GetCount(artistCounts, artistId) + 1;
        }

        if (candidate.AlbumId is not null)
        {
            albumCounts[candidate.AlbumId.Value] = GetCount(albumCounts, candidate.AlbumId.Value) + 1;
        }
    }

    private static int GetCount(IDictionary<int, int> counts, int key)
    {
        return counts.TryGetValue(key, out var count)
            ? count
            : 0;
    }

    private static double[] BuildWeightedEmbedding(
        IReadOnlyCollection<WeightedTrack> weightedTracks,
        IReadOnlyDictionary<int, ActiveAnalysis> analysesByTrack)
    {
        double[]? weightedSums = null;
        double totalWeight = 0;

        foreach (var weightedTrack in weightedTracks)
        {
            if (!analysesByTrack.TryGetValue(weightedTrack.TrackId, out var analysis))
            {
                continue;
            }

            var embedding = TryDeserializeEmbedding(analysis.EmbeddingJson);
            if (embedding.Length == 0)
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
                weightedSums[i] += embedding[i] * weightedTrack.Weight;
            }

            totalWeight += weightedTrack.Weight;
        }

        if (weightedSums is null || totalWeight <= 0)
        {
            return [];
        }

        return weightedSums
            .Select(x => x / totalWeight)
            .ToArray();
    }

    private static double CalculateEmbeddingSimilarity(double[] candidateEmbedding, double[] referenceEmbedding)
    {
        if (candidateEmbedding.Length == 0 ||
            referenceEmbedding.Length == 0 ||
            candidateEmbedding.Length != referenceEmbedding.Length)
        {
            return 0;
        }

        var dot = 0d;
        var candidateMagnitude = 0d;
        var referenceMagnitude = 0d;

        for (var i = 0; i < candidateEmbedding.Length; i++)
        {
            dot += candidateEmbedding[i] * referenceEmbedding[i];
            candidateMagnitude += candidateEmbedding[i] * candidateEmbedding[i];
            referenceMagnitude += referenceEmbedding[i] * referenceEmbedding[i];
        }

        if (candidateMagnitude <= 0 || referenceMagnitude <= 0)
        {
            return 0;
        }

        return Clamp01(dot / (Math.Sqrt(candidateMagnitude) * Math.Sqrt(referenceMagnitude)));
    }

    private static double CalculateAffinityScore(
        TrackTags tags,
        IReadOnlyDictionary<string, double> affinities)
    {
        if (tags.Values.Count == 0 || affinities.Count == 0)
        {
            return 0;
        }

        var positiveAffinityTotal = affinities
            .Where(x => x.Value > 0)
            .Sum(x => x.Value);

        if (positiveAffinityTotal <= 0)
        {
            return 0;
        }

        var score = 0d;

        foreach (var (normalizedLabel, tag) in tags.Values)
        {
            if (affinities.TryGetValue(normalizedLabel, out var affinity))
            {
                score += tag.Score * affinity;
            }
        }

        return Clamp01(score / positiveAffinityTotal);
    }

    private static double CalculateClusterAffinityScore(
        TrackClusters clusters,
        IReadOnlyDictionary<string, double> affinities)
    {
        if (clusters.Values.Count == 0 || affinities.Count == 0)
        {
            return 0;
        }

        var positiveAffinityTotal = affinities
            .Where(x => x.Value > 0)
            .Sum(x => x.Value);

        if (positiveAffinityTotal <= 0)
        {
            return 0;
        }

        var score = clusters.Values.Sum(cluster =>
            affinities.GetValueOrDefault(cluster.LookupKey) * cluster.MembershipScore);

        return Clamp01(score / positiveAffinityTotal);
    }

    private static double CalculateTagOverlap(
        TrackTags tags,
        IReadOnlyDictionary<string, double> referenceTagScores)
    {
        if (tags.Values.Count == 0 || referenceTagScores.Count == 0)
        {
            return 0;
        }

        var denominator = referenceTagScores.Values.Sum();
        if (denominator <= 0)
        {
            return 0;
        }

        var score = 0d;

        foreach (var (normalizedLabel, tag) in tags.Values)
        {
            if (referenceTagScores.TryGetValue(normalizedLabel, out var referenceScore))
            {
                score += tag.Score * referenceScore;
            }
        }

        return Clamp01(score / denominator);
    }

    private static double CalculateClusterMatch(
        TrackClusters clusters,
        IReadOnlySet<string> referenceClusterKeys)
    {
        if (clusters.Values.Count == 0 || referenceClusterKeys.Count == 0)
        {
            return 0;
        }

        return clusters.Values.Any(cluster => referenceClusterKeys.Contains(cluster.LookupKey))
            ? 1
            : 0;
    }

    private static double CalculateThemeMatch(
        TrackTags tags,
        PersonalizedPlaylistThemeDefinition? theme)
    {
        if (theme is null || tags.Values.Count == 0)
        {
            return 0;
        }

        var positiveHints = theme.PositiveLabels
            .Where(x => x.Source == PersonalizedPlaylistThemeLabelSource.EssentiaTag)
            .ToArray();
        var negativeHints = theme.NegativeLabels
            .Where(x => x.Source == PersonalizedPlaylistThemeLabelSource.EssentiaTag)
            .ToArray();

        var positiveScore = WeightedAverageMatchingTagScore(tags, positiveHints);
        var negativeScore = WeightedAverageMatchingTagScore(tags, negativeHints);

        return Clamp01(positiveScore - negativeScore * 0.6);
    }

    private static double WeightedAverageMatchingTagScore(
        TrackTags tags,
        IReadOnlyCollection<PersonalizedPlaylistThemeLabelDefinition> hints)
    {
        if (hints.Count == 0)
        {
            return 0;
        }

        var matches = hints
            .Select(hint => new
            {
                Weight = Math.Max(0, hint.Weight),
                Score = tags.Values
                    .Where(tag => TagsMatch(tag.Key, NormalizeTag(hint.Label)))
                    .Select(tag => tag.Value.Score)
                    .DefaultIfEmpty(0)
                    .Max()
            })
            .Where(x => x.Weight > 0 && x.Score > 0)
            .ToArray();

        return matches.Length == 0
            ? 0
            : Clamp01(
                matches.Sum(x => x.Score * x.Weight) /
                matches.Sum(x => x.Weight));
    }

    private static double CalculateFreshnessPopularity(
        TrackProjection track,
        int maxStreams,
        int maxTrackId)
    {
        var popularity = Math.Log(1 + Math.Max(0, track.Streams)) / Math.Log(1 + maxStreams);
        var freshness = (double)track.Id / maxTrackId;

        return Clamp01(popularity * 0.70 + freshness * 0.30);
    }

    private static IReadOnlyList<string> BuildMatchedTags(
        TrackTags tags,
        CachedUserProfile profile,
        SeedContext seedContext,
        PersonalizedPlaylistThemeDefinition? theme,
        int maxMatchedTags)
    {
        if (tags.Values.Count == 0)
        {
            return [];
        }

        var themeHints = theme?.PositiveLabels
            .Where(x => x.Source == PersonalizedPlaylistThemeLabelSource.EssentiaTag)
            .Select(x => NormalizeTag(x.Label))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToHashSet(StringComparer.OrdinalIgnoreCase) ?? [];

        return tags.Values
            .Where(tag =>
                profile.TagAffinities.GetValueOrDefault(tag.Key) > 0 ||
                seedContext.TagScores.ContainsKey(tag.Key) ||
                themeHints.Any(hint => TagsMatch(tag.Key, hint)))
            .OrderByDescending(tag => tag.Value.Score)
            .ThenBy(tag => tag.Value.Label)
            .Take(maxMatchedTags)
            .Select(tag => tag.Value.Label)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string BuildReason(
        PersonalizedRecommendationIntent intent,
        IReadOnlyDictionary<string, double> sourceSignals,
        IReadOnlyList<string> matchedTags,
        PersonalizedPlaylistThemeDefinition? theme)
    {
        if (intent == PersonalizedRecommendationIntent.DailyThematicPlaylist &&
            theme is not null &&
            sourceSignals.GetValueOrDefault("themeMatch") >= 0.2)
        {
            return matchedTags.Count > 0
                ? $"Matches {theme.Name} through {string.Join(", ", matchedTags.Take(3))}."
                : $"Matches {theme.Name}.";
        }

        if (sourceSignals.GetValueOrDefault("seedSimilarity") >= 0.45)
        {
            return "Close to the seed track's audio profile.";
        }

        if (matchedTags.Count > 0 && sourceSignals.GetValueOrDefault("sharedTags") >= 0.15)
        {
            return $"Shares tags: {string.Join(", ", matchedTags.Take(3))}.";
        }

        if (sourceSignals.GetValueOrDefault("userProfile") >= 0.25)
        {
            return "Fits your recent listening profile.";
        }

        if (sourceSignals.GetValueOrDefault("clusterMatch") > 0)
        {
            return "Comes from a similar audio cluster.";
        }

        if (sourceSignals.GetValueOrDefault("freshnessPopularity") >= 0.3)
        {
            return "Popularity and freshness fallback while your taste profile grows.";
        }

        return "Catalog fallback for this recommendation intent.";
    }

    private static double AverageAvailable(params double[] values)
    {
        var availableValues = values
            .Where(x => x > 0)
            .ToArray();

        return availableValues.Length == 0
            ? 0
            : Clamp01(availableValues.Average());
    }

    private static double[] TryDeserializeEmbedding(string? embeddingJson)
    {
        if (string.IsNullOrWhiteSpace(embeddingJson))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<double[]>(embeddingJson, JsonOptions) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static T DeserializeOrEmpty<T>(string? json)
        where T : class
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return typeof(T).IsArray
                ? (Array.CreateInstance(typeof(T).GetElementType()!, 0) as T)!
                : Activator.CreateInstance<T>();
        }

        try
        {
            return JsonSerializer.Deserialize<T>(json, JsonOptions) ?? Activator.CreateInstance<T>();
        }
        catch (JsonException)
        {
            return typeof(T).IsArray
                ? (Array.CreateInstance(typeof(T).GetElementType()!, 0) as T)!
                : Activator.CreateInstance<T>();
        }
    }

    private static string NormalizeTag(string value)
    {
        return new string(value
            .Trim()
            .ToLowerInvariant()
            .Where(char.IsLetterOrDigit)
            .ToArray());
    }

    private static bool TagsMatch(string candidateTag, string hint)
    {
        return candidateTag.Equals(hint, StringComparison.OrdinalIgnoreCase) ||
            candidateTag.Contains(hint, StringComparison.OrdinalIgnoreCase) ||
            hint.Contains(candidateTag, StringComparison.OrdinalIgnoreCase);
    }

    private static string CreateClusterLookupKey(Guid clusterRunId, string clusterKey)
    {
        return $"{clusterRunId:N}:{clusterKey.Trim().ToLowerInvariant()}";
    }

    private static double Clamp01(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            return 0;
        }

        return Math.Clamp(value, 0, 1);
    }

    private static double RoundScore(double value)
    {
        return Math.Round(Clamp01(value), 8, MidpointRounding.AwayFromZero);
    }

    private sealed record TrackProjection(
        int Id,
        int Streams,
        int? AlbumId);

    private sealed record ActiveAnalysis(
        Guid Id,
        int TrackId,
        string? EmbeddingJson,
        DateTime? CompletedAt,
        DateTime CreatedAt);

    private sealed record TrackTagProjection(
        Guid TrackAudioAnalysisId,
        string Label,
        decimal Score);

    private sealed record TrackTagCandidateProjection(
        int TrackId,
        string Label,
        decimal Score);

    private sealed record TrackClusterProjection(
        int TrackId,
        Guid ClusterRunId,
        string ClusterKey,
        decimal? MembershipScore);

    private sealed record ArtistTrackProjection(
        int TrackId,
        int ArtistId,
        bool IsLead);

    private sealed record WeightedTrack(
        int TrackId,
        double Weight);

    private sealed record TagAffinityProjection(
        string Namespace,
        string Label,
        double Score);

    private sealed record ClusterAffinityProjection(
        Guid ClusterRunId,
        string ClusterKey,
        double Score);

    private sealed record TrackTag(
        string Label,
        double Score);

    private sealed record TrackTags(
        IReadOnlyDictionary<string, TrackTag> Values)
    {
        public static TrackTags Empty { get; } = new(new Dictionary<string, TrackTag>());
    }

    private sealed record TrackCluster(
        string LookupKey,
        string ClusterKey,
        double MembershipScore);

    private sealed record TrackClusters(
        IReadOnlyList<TrackCluster> Values)
    {
        public static TrackClusters Empty { get; } = new([]);
    }

    private sealed record CachedUserProfile(
        double[] Embedding,
        IReadOnlyDictionary<string, double> TagAffinities,
        IReadOnlyDictionary<string, double> ClusterAffinities,
        IReadOnlySet<int> RecentTrackIds)
    {
        public static CachedUserProfile Empty { get; } = new(
            [],
            new Dictionary<string, double>(),
            new Dictionary<string, double>(),
            new HashSet<int>());
    }

    private sealed record SeedContext(
        double[] Embedding,
        IReadOnlyDictionary<string, double> TagScores,
        IReadOnlySet<string> ClusterLookupKeys)
    {
        public static SeedContext Empty { get; } = new(
            [],
            new Dictionary<string, double>(),
            new HashSet<string>(StringComparer.OrdinalIgnoreCase));
    }

    private sealed record ScoredCandidate(
        int TrackId,
        int? AlbumId,
        int[] ArtistIds,
        double Score,
        string Reason,
        IReadOnlyList<string> MatchedTags,
        string? ClusterKey,
        IReadOnlyDictionary<string, double> SourceSignals,
        double FreshnessPopularity,
        bool IsRecentlyPlayed);
}
