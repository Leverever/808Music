using _808Music.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace _808Music.Application.Recommendations;

public sealed record GetTrackRadioQuery(
    int UserId,
    int TrackId,
    int Limit);

public sealed record GetTrackRadioResult(
    int SeedTrackId,
    IReadOnlyList<TrackRadioRecommendation> Tracks);

public sealed record TrackRadioRecommendation(
    int TrackId,
    string Title,
    int Length,
    int Streams,
    bool IsExplicit,
    int? AlbumId,
    string? AlbumTitle,
    string CoverPath,
    IReadOnlyList<TrackRadioArtist> Artists,
    double Score,
    string Reason,
    IReadOnlyList<string> MatchedTags,
    string? ClusterKey,
    IReadOnlyDictionary<string, double> SourceSignals);

public sealed record TrackRadioArtist(
    int ArtistId,
    string Name,
    bool IsLead,
    string ProfilePhotoPath);

public interface IGetTrackRadioHandler
{
    Task<GetTrackRadioResult> Handle(
        GetTrackRadioQuery query,
        CancellationToken cancellationToken = default);
}

public sealed class GetTrackRadioHandler : IGetTrackRadioHandler
{
    private const int DefaultLimit = 50;
    private const int MaxLimit = 100;

    private readonly IApplicationDbContext _dbContext;
    private readonly IPersonalizedRecommendationService _recommendationService;

    public GetTrackRadioHandler(
        IApplicationDbContext dbContext,
        IPersonalizedRecommendationService recommendationService)
    {
        _dbContext = dbContext;
        _recommendationService = recommendationService;
    }

    public async Task<GetTrackRadioResult> Handle(
        GetTrackRadioQuery query,
        CancellationToken cancellationToken = default)
    {
        if (query.UserId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(query.UserId), "User id must be positive.");
        }

        if (query.TrackId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(query.TrackId), "Track id must be positive.");
        }

        var seedTrackExists = await _dbContext.Tracks
            .AsNoTracking()
            .AnyAsync(x => x.Id == query.TrackId, cancellationToken);

        if (!seedTrackExists)
        {
            throw new KeyNotFoundException("Track was not found.");
        }

        var limit = Math.Clamp(
            query.Limit <= 0 ? DefaultLimit : query.Limit,
            1,
            MaxLimit);

        var recommendations = await _recommendationService.GetRecommendationsAsync(
            new PersonalizedRecommendationRequest(
                query.UserId,
                PersonalizedRecommendationIntent.SongRadio,
                [query.TrackId],
                ThemeKey: null,
                limit,
                []),
            cancellationToken);

        if (recommendations.Count == 0)
        {
            return new GetTrackRadioResult(query.TrackId, []);
        }

        var trackIds = recommendations
            .Select(x => x.TrackId)
            .Distinct()
            .ToArray();
        var tracksById = await LoadTrackMetadataAsync(trackIds, cancellationToken);
        var artistsByTrack = await LoadArtistsAsync(trackIds, cancellationToken);

        var orderedRecommendations = recommendations
            .Where(x => tracksById.ContainsKey(x.TrackId))
            .Select(x =>
            {
                var track = tracksById[x.TrackId];

                return new TrackRadioRecommendation(
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

        return new GetTrackRadioResult(query.TrackId, orderedRecommendations);
    }

    private async Task<IReadOnlyDictionary<int, TrackRadioMetadataProjection>> LoadTrackMetadataAsync(
        IReadOnlyCollection<int> trackIds,
        CancellationToken cancellationToken)
    {
        var tracks = await _dbContext.Tracks
            .AsNoTracking()
            .Where(x => trackIds.Contains(x.Id))
            .Select(x => new TrackRadioMetadataProjection(
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

    private async Task<IReadOnlyDictionary<int, IReadOnlyList<TrackRadioArtist>>> LoadArtistsAsync(
        IReadOnlyCollection<int> trackIds,
        CancellationToken cancellationToken)
    {
        var artistRows = await _dbContext.ArtistTracks
            .AsNoTracking()
            .Where(x => trackIds.Contains(x.TrackId))
            .Select(x => new TrackRadioArtistProjection(
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
                x => (IReadOnlyList<TrackRadioArtist>)x
                    .OrderByDescending(artist => artist.IsLead)
                    .ThenBy(artist => artist.Name)
                    .Select(artist => new TrackRadioArtist(
                        artist.ArtistId,
                        artist.Name,
                        artist.IsLead,
                        FormatArtistProfilePhotoPath(artist.ProfilePhotoPath)))
                    .ToArray());
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

    private sealed record TrackRadioMetadataProjection(
        int TrackId,
        string Title,
        int Length,
        int Streams,
        bool IsExplicit,
        int? AlbumId,
        string? AlbumTitle,
        string? AlbumCoverPath);

    private sealed record TrackRadioArtistProjection(
        int TrackId,
        int ArtistId,
        string Name,
        bool IsLead,
        string ProfilePhotoPath);
}
