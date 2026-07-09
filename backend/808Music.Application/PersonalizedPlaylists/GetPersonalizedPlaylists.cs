using Microsoft.EntityFrameworkCore;

namespace _808Music.Application.PersonalizedPlaylists;

public sealed record GetDailyPersonalizedPlaylistsQuery(
    int UserId,
    DateOnly PlaylistDate);

public sealed record GetDailyPersonalizedPlaylistsResult(
    DateOnly PlaylistDate,
    IReadOnlyList<GeneratedPersonalizedPlaylistSummary> Playlists);

public sealed record GeneratedPersonalizedPlaylistSummary(
    Guid Id,
    string ThemeKey,
    string Name,
    string Description,
    DateOnly PlaylistDate,
    DateTime CreatedAt,
    int TrackCount);

public sealed record GetPersonalizedPlaylistQuery(
    int UserId,
    Guid PlaylistId);

public sealed record GetPersonalizedPlaylistResult(
    Guid Id,
    string ThemeKey,
    string Name,
    string Description,
    DateOnly PlaylistDate,
    DateTime CreatedAt,
    IReadOnlyList<GeneratedPersonalizedPlaylistTrackItem> Tracks);

public sealed record GeneratedPersonalizedPlaylistTrackItem(
    int TrackId,
    string Title,
    int Length,
    int Streams,
    bool IsExplicit,
    int? AlbumId,
    string? AlbumTitle,
    string CoverPath,
    IReadOnlyList<GeneratedPersonalizedPlaylistTrackArtist> Artists,
    int Position,
    double Score,
    string Reason);

public sealed record GeneratedPersonalizedPlaylistTrackArtist(
    int ArtistId,
    string Name,
    bool IsLead,
    string ProfilePhotoPath);

public interface IGetDailyPersonalizedPlaylistsHandler
{
    Task<GetDailyPersonalizedPlaylistsResult> Handle(
        GetDailyPersonalizedPlaylistsQuery query,
        CancellationToken cancellationToken = default);
}

public interface IGetPersonalizedPlaylistHandler
{
    Task<GetPersonalizedPlaylistResult> Handle(
        GetPersonalizedPlaylistQuery query,
        CancellationToken cancellationToken = default);
}

public sealed class GetDailyPersonalizedPlaylistsHandler : IGetDailyPersonalizedPlaylistsHandler
{
    private readonly IApplicationDbContext _dbContext;

    public GetDailyPersonalizedPlaylistsHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<GetDailyPersonalizedPlaylistsResult> Handle(
        GetDailyPersonalizedPlaylistsQuery query,
        CancellationToken cancellationToken = default)
    {
        if (query.UserId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(query.UserId), "User id must be positive.");
        }

        var playlists = await _dbContext.GeneratedPersonalizedPlaylists
            .AsNoTracking()
            .Where(x => x.UserId == query.UserId && x.PlaylistDate == query.PlaylistDate)
            .OrderBy(x => x.Name)
            .Select(x => new GeneratedPersonalizedPlaylistProjection(
                x.Id,
                x.ThemeKey,
                x.Name,
                x.Description,
                x.PlaylistDate,
                x.CreatedAt))
            .ToListAsync(cancellationToken);

        if (playlists.Count == 0)
        {
            return new GetDailyPersonalizedPlaylistsResult(query.PlaylistDate, []);
        }

        var playlistIds = playlists
            .Select(x => x.Id)
            .ToArray();
        var trackCounts = await _dbContext.GeneratedPersonalizedPlaylistTracks
            .AsNoTracking()
            .Where(x => playlistIds.Contains(x.PlaylistId))
            .GroupBy(x => x.PlaylistId)
            .Select(x => new PlaylistTrackCountProjection(x.Key, x.Count()))
            .ToListAsync(cancellationToken);
        var trackCountsByPlaylist = trackCounts.ToDictionary(x => x.PlaylistId, x => x.TrackCount);

        return new GetDailyPersonalizedPlaylistsResult(
            query.PlaylistDate,
            playlists
                .Select(x => new GeneratedPersonalizedPlaylistSummary(
                    x.Id,
                    x.ThemeKey,
                    x.Name,
                    x.Description,
                    x.PlaylistDate,
                    x.CreatedAt,
                    trackCountsByPlaylist.GetValueOrDefault(x.Id)))
                .ToArray());
    }

    private sealed record GeneratedPersonalizedPlaylistProjection(
        Guid Id,
        string ThemeKey,
        string Name,
        string Description,
        DateOnly PlaylistDate,
        DateTime CreatedAt);

    private sealed record PlaylistTrackCountProjection(
        Guid PlaylistId,
        int TrackCount);
}

public sealed class GetPersonalizedPlaylistHandler : IGetPersonalizedPlaylistHandler
{
    private readonly IApplicationDbContext _dbContext;

    public GetPersonalizedPlaylistHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<GetPersonalizedPlaylistResult> Handle(
        GetPersonalizedPlaylistQuery query,
        CancellationToken cancellationToken = default)
    {
        if (query.UserId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(query.UserId), "User id must be positive.");
        }

        if (query.PlaylistId == Guid.Empty)
        {
            throw new ArgumentException("Playlist id is required.", nameof(query.PlaylistId));
        }

        var playlist = await _dbContext.GeneratedPersonalizedPlaylists
            .AsNoTracking()
            .Where(x => x.Id == query.PlaylistId && x.UserId == query.UserId)
            .Select(x => new GeneratedPersonalizedPlaylistProjection(
                x.Id,
                x.ThemeKey,
                x.Name,
                x.Description,
                x.PlaylistDate,
                x.CreatedAt))
            .FirstOrDefaultAsync(cancellationToken);

        if (playlist is null)
        {
            throw new KeyNotFoundException("Personalized playlist was not found.");
        }

        var playlistTrackRows = await _dbContext.GeneratedPersonalizedPlaylistTracks
            .AsNoTracking()
            .Where(x => x.PlaylistId == query.PlaylistId)
            .OrderBy(x => x.Position)
            .Select(x => new PlaylistTrackProjection(
                x.TrackId,
                x.Position,
                x.Score,
                x.Reason))
            .ToListAsync(cancellationToken);

        if (playlistTrackRows.Count == 0)
        {
            return new GetPersonalizedPlaylistResult(
                playlist.Id,
                playlist.ThemeKey,
                playlist.Name,
                playlist.Description,
                playlist.PlaylistDate,
                playlist.CreatedAt,
                []);
        }

        var trackIds = playlistTrackRows
            .Select(x => x.TrackId)
            .Distinct()
            .ToArray();
        var tracksById = await LoadTrackMetadataAsync(trackIds, cancellationToken);
        var artistsByTrack = await LoadArtistsAsync(trackIds, cancellationToken);

        var tracks = playlistTrackRows
            .Where(x => tracksById.ContainsKey(x.TrackId))
            .Select(x =>
            {
                var track = tracksById[x.TrackId];

                return new GeneratedPersonalizedPlaylistTrackItem(
                    x.TrackId,
                    track.Title,
                    track.Length,
                    track.Streams,
                    track.IsExplicit,
                    track.AlbumId,
                    track.AlbumTitle,
                    FormatAlbumCoverPath(track.AlbumCoverPath),
                    artistsByTrack.GetValueOrDefault(x.TrackId, []),
                    x.Position,
                    (double)x.Score,
                    x.Reason);
            })
            .ToArray();

        return new GetPersonalizedPlaylistResult(
            playlist.Id,
            playlist.ThemeKey,
            playlist.Name,
            playlist.Description,
            playlist.PlaylistDate,
            playlist.CreatedAt,
            tracks);
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

    private async Task<IReadOnlyDictionary<int, IReadOnlyList<GeneratedPersonalizedPlaylistTrackArtist>>> LoadArtistsAsync(
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
                x => (IReadOnlyList<GeneratedPersonalizedPlaylistTrackArtist>)x
                    .OrderByDescending(artist => artist.IsLead)
                    .ThenBy(artist => artist.Name)
                    .Select(artist => new GeneratedPersonalizedPlaylistTrackArtist(
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

    private sealed record GeneratedPersonalizedPlaylistProjection(
        Guid Id,
        string ThemeKey,
        string Name,
        string Description,
        DateOnly PlaylistDate,
        DateTime CreatedAt);

    private sealed record PlaylistTrackProjection(
        int TrackId,
        int Position,
        decimal Score,
        string Reason);

    private sealed record TrackMetadataProjection(
        int TrackId,
        string Title,
        int Length,
        int Streams,
        bool IsExplicit,
        int? AlbumId,
        string? AlbumTitle,
        string? AlbumCoverPath);

    private sealed record TrackArtistProjection(
        int TrackId,
        int ArtistId,
        string Name,
        bool IsLead,
        string ProfilePhotoPath);
}
