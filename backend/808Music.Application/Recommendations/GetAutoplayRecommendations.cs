using _808Music.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace _808Music.Application.Recommendations;

public sealed record GetAutoplayRecommendationsQuery(
    int UserId,
    IReadOnlyCollection<int> SeedTrackIds,
    IReadOnlyCollection<int> ExcludedTrackIds,
    int Limit);

public sealed record GetAutoplayRecommendationsResult(
    IReadOnlyList<int> SeedTrackIds,
    IReadOnlyList<int> ExcludedTrackIds,
    IReadOnlyList<AutoplayTrackRecommendation> Tracks);

public sealed record AutoplayTrackRecommendation(
    int TrackId,
    string Title,
    int Length,
    int Streams,
    bool IsExplicit,
    int? AlbumId,
    string? AlbumTitle,
    string CoverPath,
    IReadOnlyList<AutoplayTrackArtist> Artists,
    double Score,
    string Reason,
    IReadOnlyList<string> MatchedTags,
    string? ClusterKey,
    IReadOnlyDictionary<string, double> SourceSignals);

public sealed record AutoplayTrackArtist(
    int ArtistId,
    string Name,
    bool IsLead,
    string ProfilePhotoPath);

public interface IGetAutoplayRecommendationsHandler
{
    Task<GetAutoplayRecommendationsResult> Handle(
        GetAutoplayRecommendationsQuery query,
        CancellationToken cancellationToken = default);
}

public sealed class GetAutoplayRecommendationsHandler : IGetAutoplayRecommendationsHandler
{
    private const int DefaultLimit = 25;
    private const int MaxLimit = 50;
    private const int MaxSeedTrackCount = 10;

    private readonly IApplicationDbContext _dbContext;
    private readonly IPersonalizedRecommendationService _recommendationService;

    public GetAutoplayRecommendationsHandler(
        IApplicationDbContext dbContext,
        IPersonalizedRecommendationService recommendationService)
    {
        _dbContext = dbContext;
        _recommendationService = recommendationService;
    }

    public async Task<GetAutoplayRecommendationsResult> Handle(
        GetAutoplayRecommendationsQuery query,
        CancellationToken cancellationToken = default)
    {
        if (query.UserId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(query.UserId), "User id must be positive.");
        }

        var seedTrackIds = NormalizeTrackIds(query.SeedTrackIds);
        if (seedTrackIds.Count == 0)
        {
            throw new ArgumentException("At least one seed track is required.", nameof(query.SeedTrackIds));
        }

        if (seedTrackIds.Count > MaxSeedTrackCount)
        {
            throw new ArgumentException($"Seed track count cannot be greater than {MaxSeedTrackCount}.", nameof(query.SeedTrackIds));
        }

        await EnsureSeedTracksExist(seedTrackIds, cancellationToken);

        var limit = Math.Clamp(
            query.Limit <= 0 ? DefaultLimit : query.Limit,
            1,
            MaxLimit);
        var effectiveExcludedTrackIds = NormalizeTrackIds(
            query.ExcludedTrackIds.Concat(seedTrackIds));
        var excludedArtistIds = await LoadArtistIdsForTracksAsync(
            effectiveExcludedTrackIds,
            cancellationToken);
        var recommendationLimit = Math.Min(100, Math.Max(limit, limit * 3));

        var recommendations = await _recommendationService.GetRecommendationsAsync(
            new PersonalizedRecommendationRequest(
                query.UserId,
                PersonalizedRecommendationIntent.Autoplay,
                seedTrackIds,
                ThemeKey: null,
                recommendationLimit,
                effectiveExcludedTrackIds),
            cancellationToken);

        if (recommendations.Count == 0)
        {
            return new GetAutoplayRecommendationsResult(
                seedTrackIds,
                effectiveExcludedTrackIds,
                []);
        }

        var trackIds = recommendations
            .Select(x => x.TrackId)
            .Distinct()
            .ToArray();
        var tracksById = await LoadTrackMetadataAsync(trackIds, cancellationToken);
        var artistsByTrack = await LoadArtistsAsync(trackIds, cancellationToken);
        var candidates = recommendations
            .Where(x => tracksById.ContainsKey(x.TrackId))
            .Select(x =>
            {
                var track = tracksById[x.TrackId];
                var artists = artistsByTrack.GetValueOrDefault(x.TrackId, []);

                return new AutoplayCandidate(
                    new AutoplayTrackRecommendation(
                        x.TrackId,
                        track.Title,
                        track.Length,
                        track.Streams,
                        track.IsExplicit,
                        track.AlbumId,
                        track.AlbumTitle,
                        FormatAlbumCoverPath(track.AlbumCoverPath),
                        artists,
                        x.Score,
                        x.Reason,
                        x.MatchedTags,
                        x.ClusterKey,
                        x.SourceSignals),
                    artists.Any(artist => excludedArtistIds.Contains(artist.ArtistId)));
            })
            .ToArray();

        var selected = candidates
            .Where(x => !x.HasExcludedArtist)
            .Take(limit)
            .ToList();

        if (selected.Count < limit)
        {
            var selectedTrackIds = selected
                .Select(x => x.Recommendation.TrackId)
                .ToHashSet();

            selected.AddRange(candidates
                .Where(x => !selectedTrackIds.Contains(x.Recommendation.TrackId))
                .Take(limit - selected.Count));
        }

        return new GetAutoplayRecommendationsResult(
            seedTrackIds,
            effectiveExcludedTrackIds,
            selected
                .Select(x => x.Recommendation)
                .ToArray());
    }

    private async Task EnsureSeedTracksExist(
        IReadOnlyCollection<int> seedTrackIds,
        CancellationToken cancellationToken)
    {
        var existingSeedTrackIds = await _dbContext.Tracks
            .AsNoTracking()
            .Where(x => seedTrackIds.Contains(x.Id))
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        if (existingSeedTrackIds.Count != seedTrackIds.Count)
        {
            throw new KeyNotFoundException("One or more seed tracks were not found.");
        }
    }

    private async Task<IReadOnlySet<int>> LoadArtistIdsForTracksAsync(
        IReadOnlyCollection<int> trackIds,
        CancellationToken cancellationToken)
    {
        if (trackIds.Count == 0)
        {
            return new HashSet<int>();
        }

        var artistIds = await _dbContext.ArtistTracks
            .AsNoTracking()
            .Where(x => trackIds.Contains(x.TrackId))
            .Select(x => x.ArtistId)
            .Distinct()
            .ToListAsync(cancellationToken);

        return artistIds.ToHashSet();
    }

    private async Task<IReadOnlyDictionary<int, AutoplayTrackMetadataProjection>> LoadTrackMetadataAsync(
        IReadOnlyCollection<int> trackIds,
        CancellationToken cancellationToken)
    {
        var tracks = await _dbContext.Tracks
            .AsNoTracking()
            .Where(x => trackIds.Contains(x.Id))
            .Select(x => new AutoplayTrackMetadataProjection(
                x.Id,
                x.Title,
                x.Length,
                x.Streams,
                x.IsExplicit,
                x.AlbumId,
                x.Album != null ? x.Album.Title : null,
                x.Album != null ? x.Album.CoverPath : null))
            .ToListAsync(cancellationToken);

        return tracks.ToDictionary(x => x.TrackId);
    }

    private async Task<IReadOnlyDictionary<int, IReadOnlyList<AutoplayTrackArtist>>> LoadArtistsAsync(
        IReadOnlyCollection<int> trackIds,
        CancellationToken cancellationToken)
    {
        var artistRows = await _dbContext.ArtistTracks
            .AsNoTracking()
            .Where(x => trackIds.Contains(x.TrackId))
            .Select(x => new AutoplayTrackArtistProjection(
                x.TrackId,
                x.ArtistId,
                x.Artist != null ? x.Artist.Name : string.Empty,
                x.IsLead,
                x.Artist != null ? x.Artist.ProfilePhotoPath : string.Empty))
            .ToListAsync(cancellationToken);

        return artistRows
            .GroupBy(x => x.TrackId)
            .ToDictionary(
                x => x.Key,
                x => (IReadOnlyList<AutoplayTrackArtist>)x
                    .OrderByDescending(artist => artist.IsLead)
                    .ThenBy(artist => artist.Name)
                    .Select(artist => new AutoplayTrackArtist(
                        artist.ArtistId,
                        artist.Name,
                        artist.IsLead,
                        FormatArtistProfilePhotoPath(artist.ProfilePhotoPath)))
                    .ToArray());
    }

    private static IReadOnlyList<int> NormalizeTrackIds(IEnumerable<int> trackIds)
    {
        return trackIds
            .Where(x => x > 0)
            .Distinct()
            .ToArray();
    }

    private static string FormatAlbumCoverPath(string? coverPath)
    {
        return string.IsNullOrWhiteSpace(coverPath)
            ? "/media/Images/playlist_placeholder.png"
            : $"/media/Images/AlbumCovers/{coverPath}";
    }

    private static string FormatArtistProfilePhotoPath(string? profilePhotoPath)
    {
        return string.IsNullOrWhiteSpace(profilePhotoPath)
            ? "/media/Images/ArtistPfps/placeholder.png"
            : $"/media/Images/ArtistPfps/{profilePhotoPath}";
    }

    private sealed record AutoplayCandidate(
        AutoplayTrackRecommendation Recommendation,
        bool HasExcludedArtist);

    private sealed record AutoplayTrackMetadataProjection(
        int TrackId,
        string Title,
        int Length,
        int Streams,
        bool IsExplicit,
        int? AlbumId,
        string? AlbumTitle,
        string? AlbumCoverPath);

    private sealed record AutoplayTrackArtistProjection(
        int TrackId,
        int ArtistId,
        string Name,
        bool IsLead,
        string ProfilePhotoPath);
}
