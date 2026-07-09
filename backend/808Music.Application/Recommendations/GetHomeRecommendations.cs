using _808Music.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace _808Music.Application.Recommendations;

public sealed record GetHomeRecommendationsQuery(
    int UserId,
    DateOnly RecommendationDate,
    int DailyPlaylistLimit = 6,
    int AlbumLimit = 10,
    int ArtistLimit = 10,
    int PlaylistLimit = 10,
    int TrackLimit = 20);

public sealed record GetHomeRecommendationsResult(
    DateOnly RecommendationDate,
    IReadOnlyList<HomeDailyPlaylistRecommendation> DailyPersonalizedPlaylists,
    IReadOnlyList<HomeAlbumRecommendation> RecommendedAlbums,
    IReadOnlyList<HomeArtistRecommendation> RecommendedArtists,
    IReadOnlyList<HomePlaylistRecommendation> RecommendedPlaylists,
    IReadOnlyList<HomeTrackRecommendation> RecommendedTracks);

public sealed record HomeDailyPlaylistRecommendation(
    Guid PlaylistId,
    string ThemeKey,
    string Name,
    string Description,
    string CoverPath,
    DateOnly PlaylistDate,
    DateTime CreatedAt,
    int TrackCount,
    double Score,
    string Reason);

public sealed record HomeAlbumRecommendation(
    int AlbumId,
    string Title,
    string CoverPath,
    int ArtistId,
    string ArtistName,
    int TrackCount,
    double Score,
    string Reason,
    IReadOnlyList<int> MatchedTrackIds);

public sealed record HomeArtistRecommendation(
    int ArtistId,
    string Name,
    string ProfilePhotoPath,
    double Score,
    string Reason,
    IReadOnlyList<int> MatchedTrackIds);

public sealed record HomePlaylistRecommendation(
    int PlaylistId,
    string Title,
    string CoverPath,
    bool IsPublic,
    bool IsCollaborative,
    int TrackCount,
    int? OwnerUserId,
    string? OwnerUsername,
    double Score,
    string Reason,
    IReadOnlyList<int> MatchedTrackIds);

public sealed record HomeTrackRecommendation(
    int TrackId,
    string Title,
    int Length,
    int Streams,
    bool IsExplicit,
    int? AlbumId,
    string? AlbumTitle,
    string CoverPath,
    IReadOnlyList<HomeTrackArtist> Artists,
    double Score,
    string Reason,
    IReadOnlyList<string> MatchedTags,
    string? ClusterKey,
    IReadOnlyDictionary<string, double> SourceSignals);

public sealed record HomeTrackArtist(
    int ArtistId,
    string Name,
    bool IsLead,
    string ProfilePhotoPath);

public interface IGetHomeRecommendationsHandler
{
    Task<GetHomeRecommendationsResult> Handle(
        GetHomeRecommendationsQuery query,
        CancellationToken cancellationToken = default);
}

public sealed class GetHomeRecommendationsHandler : IGetHomeRecommendationsHandler
{
    private const int DefaultRecommendationCandidateCount = 100;
    private const string DefaultPlaylistCoverPath = "/Images/playlist_placeholder.png";

    private readonly IApplicationDbContext _dbContext;
    private readonly IPersonalizedRecommendationService _recommendationService;
    private readonly ILegacyPlaylistRecommendationReader _legacyPlaylistRecommendationReader;

    public GetHomeRecommendationsHandler(
        IApplicationDbContext dbContext,
        IPersonalizedRecommendationService recommendationService,
        ILegacyPlaylistRecommendationReader legacyPlaylistRecommendationReader)
    {
        _dbContext = dbContext;
        _recommendationService = recommendationService;
        _legacyPlaylistRecommendationReader = legacyPlaylistRecommendationReader;
    }

    public async Task<GetHomeRecommendationsResult> Handle(
        GetHomeRecommendationsQuery query,
        CancellationToken cancellationToken = default)
    {
        if (query.UserId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(query.UserId), "User id must be positive.");
        }

        var dailyPlaylistLimit = ClampLimit(query.DailyPlaylistLimit, 6, 20);
        var albumLimit = ClampLimit(query.AlbumLimit, 10, 25);
        var artistLimit = ClampLimit(query.ArtistLimit, 10, 25);
        var playlistLimit = ClampLimit(query.PlaylistLimit, 10, 25);
        var trackLimit = ClampLimit(query.TrackLimit, 20, 50);

        var dailyPlaylists = await LoadDailyPlaylistsAsync(
            query.UserId,
            query.RecommendationDate,
            dailyPlaylistLimit,
            cancellationToken);

        var trackRecommendations = await _recommendationService.GetRecommendationsAsync(
            new PersonalizedRecommendationRequest(
                query.UserId,
                PersonalizedRecommendationIntent.General,
                [],
                ThemeKey: null,
                DefaultRecommendationCandidateCount,
                []),
            cancellationToken);

        if (trackRecommendations.Count == 0)
        {
            return new GetHomeRecommendationsResult(
                query.RecommendationDate,
                dailyPlaylists,
                [],
                [],
                [],
                []);
        }

        var trackIds = trackRecommendations
            .Select(x => x.TrackId)
            .Distinct()
            .ToArray();
        var tracksById = await LoadTrackMetadataAsync(trackIds, cancellationToken);
        var artistsByTrack = await LoadArtistsByTrackAsync(trackIds, cancellationToken);
        var knownRecommendations = trackRecommendations
            .Where(x => tracksById.ContainsKey(x.TrackId))
            .ToArray();

        var albums = await BuildAlbumRecommendationsAsync(
            knownRecommendations,
            tracksById,
            albumLimit,
            cancellationToken);
        var artists = BuildArtistRecommendations(
            knownRecommendations,
            artistsByTrack,
            artistLimit);
        var playlists = await BuildPlaylistRecommendationsAsync(
            query.UserId,
            knownRecommendations,
            playlistLimit,
            cancellationToken);
        var tracks = BuildTrackRecommendations(
            knownRecommendations,
            tracksById,
            artistsByTrack,
            trackLimit);

        return new GetHomeRecommendationsResult(
            query.RecommendationDate,
            dailyPlaylists,
            albums,
            artists,
            playlists,
            tracks);
    }

    private async Task<IReadOnlyList<HomeDailyPlaylistRecommendation>> LoadDailyPlaylistsAsync(
        int userId,
        DateOnly recommendationDate,
        int limit,
        CancellationToken cancellationToken)
    {
        var playlists = await _dbContext.GeneratedPersonalizedPlaylists
            .AsNoTracking()
            .Where(x => x.UserId == userId && x.PlaylistDate == recommendationDate)
            .OrderBy(x => x.Name)
            .Take(limit)
            .Select(x => new DailyPlaylistProjection(
                x.Id,
                x.ThemeKey,
                x.Name,
                x.Description,
                x.PlaylistDate,
                x.CreatedAt))
            .ToListAsync(cancellationToken);

        if (playlists.Count == 0)
        {
            return [];
        }

        var playlistIds = playlists
            .Select(x => x.PlaylistId)
            .ToArray();
        var trackCounts = await _dbContext.GeneratedPersonalizedPlaylistTracks
            .AsNoTracking()
            .Where(x => playlistIds.Contains(x.PlaylistId))
            .GroupBy(x => x.PlaylistId)
            .Select(x => new PlaylistTrackCountProjection(x.Key, x.Count()))
            .ToListAsync(cancellationToken);
        var trackCountsByPlaylist = trackCounts.ToDictionary(x => x.PlaylistId, x => x.TrackCount);

        return playlists
            .Select(x => new HomeDailyPlaylistRecommendation(
                x.PlaylistId,
                x.ThemeKey,
                x.Name,
                x.Description,
                DefaultPlaylistCoverPath,
                x.PlaylistDate,
                x.CreatedAt,
                trackCountsByPlaylist.GetValueOrDefault(x.PlaylistId),
                1,
                "Generated today from your recent listening."))
            .ToArray();
    }

    private async Task<IReadOnlyList<HomeAlbumRecommendation>> BuildAlbumRecommendationsAsync(
        IReadOnlyList<PersonalizedRecommendation> recommendations,
        IReadOnlyDictionary<int, TrackMetadataProjection> tracksById,
        int limit,
        CancellationToken cancellationToken)
    {
        var albumGroups = recommendations
            .Where(x => tracksById.TryGetValue(x.TrackId, out var track) && track.AlbumId is not null)
            .GroupBy(x => tracksById[x.TrackId].AlbumId!.Value)
            .ToArray();

        if (albumGroups.Length == 0)
        {
            return [];
        }

        var albumIds = albumGroups
            .Select(x => x.Key)
            .ToArray();
        var albums = await _dbContext.Albums
            .AsNoTracking()
            .Where(x => albumIds.Contains(x.Id))
            .Select(x => new AlbumProjection(
                x.Id,
                x.Title,
                x.CoverPath,
                x.ArtistId,
                x.Artist != null ? x.Artist.Name : string.Empty,
                x.NumOfTracks))
            .ToListAsync(cancellationToken);
        var albumsById = albums.ToDictionary(x => x.AlbumId);

        return albumGroups
            .Where(x => albumsById.ContainsKey(x.Key))
            .Select(x =>
            {
                var album = albumsById[x.Key];
                var matchedTrackIds = x
                    .Select(track => track.TrackId)
                    .Distinct()
                    .ToArray();
                var score = CalculateCollectionScore(
                    x.Select(track => track.Score),
                    matchedTrackIds.Length,
                    album.TrackCount);

                return new HomeAlbumRecommendation(
                    album.AlbumId,
                    album.Title,
                    FormatAlbumCoverPath(album.CoverPath),
                    album.ArtistId,
                    album.ArtistName,
                    album.TrackCount,
                    score,
                    BuildCollectionReason(matchedTrackIds.Length, "album"),
                    matchedTrackIds);
            })
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Title)
            .Take(limit)
            .ToArray();
    }

    private IReadOnlyList<HomeArtistRecommendation> BuildArtistRecommendations(
        IReadOnlyList<PersonalizedRecommendation> recommendations,
        IReadOnlyDictionary<int, IReadOnlyList<HomeTrackArtist>> artistsByTrack,
        int limit)
    {
        var recommendationsByTrack = recommendations.ToDictionary(x => x.TrackId);
        var artistCandidates = recommendations
            .SelectMany(recommendation =>
                artistsByTrack.GetValueOrDefault(recommendation.TrackId, [])
                    .Select(artist => new ArtistCandidate(
                        artist.ArtistId,
                        artist.Name,
                        artist.ProfilePhotoPath,
                        recommendation.TrackId,
                        recommendation.Score,
                        artist.IsLead)))
            .GroupBy(x => x.ArtistId)
            .Select(group =>
            {
                var first = group
                    .OrderByDescending(x => x.IsLead)
                    .ThenByDescending(x => x.TrackScore)
                    .First();
                var matchedTrackIds = group
                    .Select(x => x.TrackId)
                    .Distinct()
                    .Where(recommendationsByTrack.ContainsKey)
                    .ToArray();
                var score = CalculateCollectionScore(
                    matchedTrackIds.Select(trackId => recommendationsByTrack[trackId].Score),
                    matchedTrackIds.Length,
                    targetCount: 12);

                return new HomeArtistRecommendation(
                    first.ArtistId,
                    first.Name,
                    first.ProfilePhotoPath,
                    score,
                    $"{matchedTrackIds.Length} matching track{Pluralize(matchedTrackIds.Length)} from this artist fit your profile.",
                    matchedTrackIds);
            })
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Name)
            .Take(limit)
            .ToArray();

        return artistCandidates;
    }

    private async Task<IReadOnlyList<HomePlaylistRecommendation>> BuildPlaylistRecommendationsAsync(
        int userId,
        IReadOnlyList<PersonalizedRecommendation> recommendations,
        int limit,
        CancellationToken cancellationToken)
    {
        var playlistRecommendations = await _legacyPlaylistRecommendationReader.GetRecommendedPlaylistsAsync(
            new LegacyPlaylistRecommendationRequest(
                userId,
                recommendations
                    .Select(x => new LegacyPlaylistTrackSignal(x.TrackId, x.Score, x.Reason))
                    .ToArray(),
                limit),
            cancellationToken);

        return playlistRecommendations
            .Select(x => new HomePlaylistRecommendation(
                x.PlaylistId,
                x.Title,
                x.CoverPath,
                x.IsPublic,
                x.IsCollaborative,
                x.TrackCount,
                x.OwnerUserId,
                x.OwnerUsername,
                x.Score,
                x.Reason,
                x.MatchedTrackIds))
            .ToArray();
    }

    private IReadOnlyList<HomeTrackRecommendation> BuildTrackRecommendations(
        IReadOnlyList<PersonalizedRecommendation> recommendations,
        IReadOnlyDictionary<int, TrackMetadataProjection> tracksById,
        IReadOnlyDictionary<int, IReadOnlyList<HomeTrackArtist>> artistsByTrack,
        int limit)
    {
        return recommendations
            .Where(x => tracksById.ContainsKey(x.TrackId))
            .Take(limit)
            .Select(x =>
            {
                var track = tracksById[x.TrackId];

                return new HomeTrackRecommendation(
                    x.TrackId,
                    track.Title,
                    track.Length,
                    track.Streams,
                    track.IsExplicit,
                    track.AlbumId,
                    track.AlbumTitle,
                    FormatAlbumCoverPath(track.AlbumCoverPath),
                    artistsByTrack.GetValueOrDefault(x.TrackId, []),
                    x.Score,
                    x.Reason,
                    x.MatchedTags,
                    x.ClusterKey,
                    x.SourceSignals);
            })
            .ToArray();
    }

    private async Task<IReadOnlyDictionary<int, TrackMetadataProjection>> LoadTrackMetadataAsync(
        IReadOnlyCollection<int> trackIds,
        CancellationToken cancellationToken)
    {
        var tracks = await _dbContext.Tracks
            .AsNoTracking()
            .Where(x => trackIds.Contains(x.Id))
            .Select(x => new TrackMetadataProjection(
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

    private async Task<IReadOnlyDictionary<int, IReadOnlyList<HomeTrackArtist>>> LoadArtistsByTrackAsync(
        IReadOnlyCollection<int> trackIds,
        CancellationToken cancellationToken)
    {
        var artistRows = await _dbContext.ArtistTracks
            .AsNoTracking()
            .Where(x => trackIds.Contains(x.TrackId))
            .Select(x => new TrackArtistProjection(
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
                x => (IReadOnlyList<HomeTrackArtist>)x
                    .OrderByDescending(artist => artist.IsLead)
                    .ThenBy(artist => artist.Name)
                    .Select(artist => new HomeTrackArtist(
                        artist.ArtistId,
                        artist.Name,
                        artist.IsLead,
                        FormatArtistProfilePhotoPath(artist.ProfilePhotoPath)))
                    .ToArray());
    }

    private static int ClampLimit(int value, int defaultValue, int maxValue)
    {
        return Math.Clamp(value <= 0 ? defaultValue : value, 1, maxValue);
    }

    private static double CalculateCollectionScore(
        IEnumerable<double> scores,
        int matchedCount,
        int targetCount)
    {
        var topAverage = scores
            .OrderByDescending(x => x)
            .Take(8)
            .DefaultIfEmpty(0)
            .Average();
        var coverageBoost = Math.Min(
            0.15,
            targetCount <= 0
                ? matchedCount * 0.02
                : (double)matchedCount / targetCount * 0.15);

        return RoundScore(topAverage * 0.85 + coverageBoost);
    }

    private static string BuildCollectionReason(int matchedTrackCount, string collectionName)
    {
        return $"{matchedTrackCount} matching track{Pluralize(matchedTrackCount)} from this {collectionName} fit your profile.";
    }

    private static string Pluralize(int count)
    {
        return count == 1 ? string.Empty : "s";
    }

    private static double RoundScore(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            return 0;
        }

        return Math.Round(Math.Clamp(value, 0, 1), 8, MidpointRounding.AwayFromZero);
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

    private sealed record DailyPlaylistProjection(
        Guid PlaylistId,
        string ThemeKey,
        string Name,
        string Description,
        DateOnly PlaylistDate,
        DateTime CreatedAt);

    private sealed record PlaylistTrackCountProjection(
        Guid PlaylistId,
        int TrackCount);

    private sealed record TrackMetadataProjection(
        int TrackId,
        string Title,
        int Length,
        int Streams,
        bool IsExplicit,
        int? AlbumId,
        string? AlbumTitle,
        string? AlbumCoverPath);

    private sealed record AlbumProjection(
        int AlbumId,
        string Title,
        string? CoverPath,
        int ArtistId,
        string ArtistName,
        int TrackCount);

    private sealed record TrackArtistProjection(
        int TrackId,
        int ArtistId,
        string Name,
        bool IsLead,
        string ProfilePhotoPath);

    private sealed record ArtistCandidate(
        int ArtistId,
        string Name,
        string ProfilePhotoPath,
        int TrackId,
        double TrackScore,
        bool IsLead);
}
