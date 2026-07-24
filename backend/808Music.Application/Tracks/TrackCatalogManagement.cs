using _808Music.Application.Common.Search;
using _808Music.Application.Releases;
using _808Music.Domain.Catalog;
using Microsoft.EntityFrameworkCore;

namespace _808Music.Application.Tracks;

public sealed record TrackCatalogQuery(
    int ArtistId,
    int PageNumber = 1,
    int PageSize = 20,
    string? Title = null,
    string? PrimaryReleaseTitle = null,
    int? MinStreams = null,
    int? MaxStreams = null,
    int? MinDurationSeconds = null,
    int? MaxDurationSeconds = null,
    string? SortBy = null,
    string? SortDirection = null)
{
    public int NormalizedPage => Math.Max(1, PageNumber);
    public int NormalizedPageSize => Math.Clamp(PageSize, 1, 100);
}

public sealed record TrackArtistResponse(
    int ArtistTrackId,
    int ArtistId,
    string Name,
    string ProfilePhotoPath,
    bool IsLead,
    bool ShowOnProfile);

public sealed record TrackReleaseResponse(
    int? AssociationId,
    int ReleaseId,
    string Title,
    string CoverPath,
    DateTime ReleaseDate,
    string ReleaseType,
    int DiscNumber,
    int TrackNumber,
    string? TitleOverride,
    bool IsPrimaryRelease,
    bool IsLegacyAssociation);

public sealed record TrackReleaseSummaryResponse(
    int ReleaseId,
    string Title,
    string CoverPath);

public sealed record TrackCatalogItemResponse(
    int Id,
    string Title,
    bool IsExplicit,
    int LengthSeconds,
    int Streams,
    IReadOnlyList<TrackArtistResponse> FeaturedArtists,
    int ReleaseCount,
    TrackReleaseSummaryResponse? PrimaryRelease);

public sealed record TrackAudioTagResponse(
    string Namespace,
    string Label,
    decimal Score);

public sealed record TrackAudioAnalysisResponse(
    string Status,
    string? ErrorMessage,
    IReadOnlyList<TrackAudioTagResponse> Tags);

public sealed record TrackDetailsResponse(
    int Id,
    string Title,
    bool IsExplicit,
    int LengthSeconds,
    int Streams,
    TrackArtistResponse LeadArtist,
    IReadOnlyList<TrackArtistResponse> FeaturedArtists,
    IReadOnlyList<TrackReleaseResponse> Releases,
    TrackAudioAnalysisResponse? Analysis);

public sealed record FeaturedArtistInput(int ArtistId, bool ShowOnProfile);

public sealed record TrackReleaseInput(
    int ReleaseId,
    int DiscNumber,
    int TrackNumber,
    string? TitleOverride,
    bool IsPrimaryRelease);

public sealed record ArtistSearchResponse(
    int Id,
    string Name,
    string ProfilePhotoPath);

public sealed record ReleaseSearchQuery(
    int ArtistId,
    int? ExcludeTrackId,
    int PageNumber = 1,
    int PageSize = 10,
    string? Title = null)
{
    public int NormalizedPage => Math.Max(1, PageNumber);
    public int NormalizedPageSize => Math.Clamp(PageSize, 1, 50);
}

public sealed record ReleaseSearchResponse(
    int Id,
    string Title,
    string CoverPath,
    DateTime ReleaseDate,
    string ReleaseType);

public interface ITrackCatalogHandler
{
    Task<PagedResult<TrackCatalogItemResponse>> List(
        TrackCatalogQuery query,
        CancellationToken cancellationToken = default);

    Task<TrackDetailsResponse?> GetDetails(
        int trackId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TrackArtistResponse>?> ReplaceFeaturedArtists(
        int trackId,
        IReadOnlyList<FeaturedArtistInput> artists,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TrackReleaseResponse>?> ReplaceReleases(
        int trackId,
        IReadOnlyList<TrackReleaseInput> releases,
        CancellationToken cancellationToken = default);
}

public interface ITrackCatalogSearchHandler
{
    Task<bool> ArtistExists(
        int artistId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ArtistSearchResponse>> SearchArtists(
        string? query,
        int? excludeArtistId,
        int limit,
        CancellationToken cancellationToken = default);

    Task<PagedResult<ReleaseSearchResponse>> SearchReleases(
        ReleaseSearchQuery query,
        CancellationToken cancellationToken = default);
}

public sealed class TrackCatalogHandler : ITrackCatalogHandler
{
    private readonly IApplicationDbContext _dbContext;

    public TrackCatalogHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PagedResult<TrackCatalogItemResponse>> List(
        TrackCatalogQuery query,
        CancellationToken cancellationToken = default)
    {
        var normalizedTitle = NormalizeOptionalText(query.Title);
        var normalizedReleaseTitle = NormalizeOptionalText(query.PrimaryReleaseTitle);

        var tracksQuery =
            from credit in _dbContext.ArtistTracks.AsNoTracking()
            join track in _dbContext.Tracks.AsNoTracking() on credit.TrackId equals track.Id
            where credit.ArtistId == query.ArtistId && credit.IsLead
            where normalizedTitle == null || track.Title.Contains(normalizedTitle)
            where normalizedReleaseTitle == null ||
                  _dbContext.AlbumTracks.Any(association =>
                      association.TrackId == track.Id &&
                      association.IsPrimaryRelease &&
                      association.Album!.Title.Contains(normalizedReleaseTitle)) ||
                  _dbContext.Albums.Any(release =>
                      track.AlbumId.HasValue &&
                      release.Id == track.AlbumId.Value &&
                      release.Title.Contains(normalizedReleaseTitle))
            where !query.MinStreams.HasValue || track.Streams >= query.MinStreams.Value
            where !query.MaxStreams.HasValue || track.Streams <= query.MaxStreams.Value
            where !query.MinDurationSeconds.HasValue || track.Length >= query.MinDurationSeconds.Value
            where !query.MaxDurationSeconds.HasValue || track.Length <= query.MaxDurationSeconds.Value
            select track;

        var totalCount = await tracksQuery.CountAsync(cancellationToken);
        var rows = await ApplyCatalogSort(tracksQuery, query)
            .Skip((query.NormalizedPage - 1) * query.NormalizedPageSize)
            .Take(query.NormalizedPageSize)
            .Select(track => new TrackRow(
                track.Id,
                track.Title,
                track.IsExplicit,
                track.Length,
                track.Streams,
                track.AlbumId))
            .ToListAsync(cancellationToken);

        var trackIds = rows.Select(x => x.Id).ToArray();
        var featuredArtists = await GetFeaturedArtists(trackIds, cancellationToken);
        var releases = await GetReleaseMap(rows, cancellationToken);

        var items = rows.Select(row =>
        {
            var trackReleases = releases.GetValueOrDefault(row.Id) ?? [];
            var primary = trackReleases.FirstOrDefault(x => x.IsPrimaryRelease);

            return new TrackCatalogItemResponse(
                row.Id,
                row.Title,
                row.IsExplicit,
                row.LengthSeconds,
                row.Streams,
                featuredArtists.GetValueOrDefault(row.Id) ?? [],
                trackReleases.Count,
                primary is null
                    ? null
                    : new TrackReleaseSummaryResponse(
                        primary.ReleaseId,
                        primary.Title,
                        primary.CoverPath));
        }).ToList();

        return new PagedResult<TrackCatalogItemResponse>(
            items,
            query.NormalizedPage,
            query.NormalizedPageSize,
            totalCount);
    }

    public async Task<TrackDetailsResponse?> GetDetails(
        int trackId,
        CancellationToken cancellationToken = default)
    {
        var row = await _dbContext.Tracks
            .AsNoTracking()
            .Where(x => x.Id == trackId)
            .Select(x => new TrackRow(
                x.Id,
                x.Title,
                x.IsExplicit,
                x.Length,
                x.Streams,
                x.AlbumId))
            .FirstOrDefaultAsync(cancellationToken);
        if (row is null)
        {
            return null;
        }

        var leadArtist = await _dbContext.ArtistTracks
            .AsNoTracking()
            .Where(x => x.TrackId == trackId && x.IsLead)
            .Select(x => new TrackArtistResponse(
                x.Id,
                x.ArtistId,
                x.Artist!.Name,
                x.Artist.ProfilePhotoPath,
                true,
                x.ShowOnProfile))
            .FirstOrDefaultAsync(cancellationToken);
        if (leadArtist is null)
        {
            return null;
        }

        var featured = await GetFeaturedArtists([trackId], cancellationToken);
        var releaseMap = await GetReleaseMap([row], cancellationToken);

        var analysis = await _dbContext.TrackAudioAnalyses
            .AsNoTracking()
            .Include(x => x.Tags)
            .Where(x => x.TrackId == trackId)
            .OrderByDescending(x => x.IsActive)
            .ThenByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        TrackAudioAnalysisResponse? analysisResponse = null;
        if (analysis is not null)
        {
            analysisResponse = new TrackAudioAnalysisResponse(
                analysis.Status.ToString(),
                analysis.ErrorMessage,
                analysis.Tags
                    .OrderByDescending(x => x.Score)
                    .ThenBy(x => x.Label)
                    .Select(x => new TrackAudioTagResponse(x.Namespace, x.Label, x.Score))
                    .ToList());
        }

        return new TrackDetailsResponse(
            row.Id,
            row.Title,
            row.IsExplicit,
            row.LengthSeconds,
            row.Streams,
            leadArtist,
            featured.GetValueOrDefault(trackId) ?? [],
            releaseMap.GetValueOrDefault(trackId) ?? [],
            analysisResponse);
    }

    public async Task<IReadOnlyList<TrackArtistResponse>?> ReplaceFeaturedArtists(
        int trackId,
        IReadOnlyList<FeaturedArtistInput> artists,
        CancellationToken cancellationToken = default)
    {
        var credits = await _dbContext.ArtistTracks
            .Where(x => x.TrackId == trackId)
            .ToListAsync(cancellationToken);
        if (credits.Count == 0)
        {
            return null;
        }

        var leadArtistId = credits.FirstOrDefault(x => x.IsLead)?.ArtistId
            ?? throw new InvalidOperationException("Track has no lead artist.");

        var duplicateIds = artists
            .GroupBy(x => x.ArtistId)
            .Where(x => x.Count() > 1)
            .Select(x => x.Key)
            .ToArray();
        if (duplicateIds.Length != 0)
        {
            throw new InvalidOperationException("Featured artists cannot contain duplicates.");
        }

        if (artists.Any(x => x.ArtistId <= 0 || x.ArtistId == leadArtistId))
        {
            throw new InvalidOperationException("The lead artist cannot be a featured artist.");
        }

        var requestedIds = artists.Select(x => x.ArtistId).ToArray();
        var existingArtistCount = await _dbContext.Artists
            .CountAsync(x => requestedIds.Contains(x.Id), cancellationToken);
        if (existingArtistCount != requestedIds.Length)
        {
            throw new KeyNotFoundException("One or more featured artists were not found.");
        }

        var requestedById = artists.ToDictionary(x => x.ArtistId);
        var currentFeatured = credits.Where(x => !x.IsLead).ToList();

        foreach (var credit in currentFeatured)
        {
            if (!requestedById.TryGetValue(credit.ArtistId, out var requested))
            {
                _dbContext.ArtistTracks.Remove(credit);
                continue;
            }

            credit.ShowOnProfile = requested.ShowOnProfile;
            requestedById.Remove(credit.ArtistId);
        }

        foreach (var requested in requestedById.Values)
        {
            await _dbContext.ArtistTracks.AddAsync(new ArtistTrack
            {
                ArtistId = requested.ArtistId,
                TrackId = trackId,
                IsLead = false,
                ShowOnProfile = requested.ShowOnProfile
            }, cancellationToken);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        var result = await GetFeaturedArtists([trackId], cancellationToken);
        return result.GetValueOrDefault(trackId) ?? [];
    }

    public async Task<IReadOnlyList<TrackReleaseResponse>?> ReplaceReleases(
        int trackId,
        IReadOnlyList<TrackReleaseInput> releases,
        CancellationToken cancellationToken = default)
    {
        var track = await _dbContext.Tracks
            .FirstOrDefaultAsync(x => x.Id == trackId, cancellationToken);
        if (track is null)
        {
            return null;
        }

        ValidateReleaseInputs(releases);
        var legacyPrimaryReleaseId = track.AlbumId;

        var leadArtistId = await _dbContext.ArtistTracks
            .Where(x => x.TrackId == trackId && x.IsLead)
            .Select(x => (int?)x.ArtistId)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("Track has no lead artist.");

        var requestedReleaseIds = releases.Select(x => x.ReleaseId).ToArray();
        var requestedReleases = await _dbContext.Albums
            .Where(x => requestedReleaseIds.Contains(x.Id))
            .ToListAsync(cancellationToken);
        if (requestedReleases.Count != requestedReleaseIds.Length)
        {
            throw new KeyNotFoundException("One or more releases were not found.");
        }

        if (requestedReleases.Any(x => x.ArtistId != leadArtistId))
        {
            throw new InvalidOperationException("Every release must belong to the track's lead artist.");
        }

        foreach (var requested in releases)
        {
            var positionTaken = await _dbContext.AlbumTracks.AnyAsync(
                x => x.AlbumId == requested.ReleaseId &&
                     x.TrackId != trackId &&
                     x.DiscNumber == requested.DiscNumber &&
                     x.TrackNumber == requested.TrackNumber,
                cancellationToken);

            if (!positionTaken && requested.DiscNumber == 1)
            {
                positionTaken = await _dbContext.Tracks.AnyAsync(
                    candidate => candidate.Id != trackId &&
                                 candidate.AlbumId == requested.ReleaseId &&
                                 candidate.Id == requested.TrackNumber &&
                                 !_dbContext.AlbumTracks.Any(
                                     association => association.AlbumId == requested.ReleaseId &&
                                                    association.TrackId == candidate.Id),
                    cancellationToken);
            }

            if (positionTaken)
            {
                throw new InvalidOperationException(
                    $"Release {requested.ReleaseId} already contains a track at disc {requested.DiscNumber}, track {requested.TrackNumber}.");
            }
        }

        var currentAssociations = await _dbContext.AlbumTracks
            .Where(x => x.TrackId == trackId)
            .ToListAsync(cancellationToken);
        var currentByReleaseId = currentAssociations.ToDictionary(x => x.AlbumId);

        foreach (var association in currentAssociations)
        {
            if (!requestedReleaseIds.Contains(association.AlbumId))
            {
                _dbContext.AlbumTracks.Remove(association);
            }
        }

        foreach (var requested in releases)
        {
            if (!currentByReleaseId.TryGetValue(requested.ReleaseId, out var association))
            {
                association = new AlbumTrack
                {
                    AlbumId = requested.ReleaseId,
                    TrackId = trackId
                };
                await _dbContext.AlbumTracks.AddAsync(association, cancellationToken);
            }

            association.DiscNumber = requested.DiscNumber;
            association.TrackNumber = requested.TrackNumber;
            association.TitleOverride = ReleaseTrackAssociationValidation.NormalizeTitleOverride(requested.TitleOverride);
            association.IsPrimaryRelease = requested.IsPrimaryRelease;
        }

        track.AlbumId = releases.FirstOrDefault(x => x.IsPrimaryRelease)?.ReleaseId;

        var touchedReleaseIds = currentAssociations
            .Select(x => x.AlbumId)
            .Append(legacyPrimaryReleaseId ?? 0)
            .Concat(requestedReleaseIds)
            .Where(x => x > 0)
            .Distinct()
            .ToArray();

        var touchedReleases = await _dbContext.Albums
            .Where(x => touchedReleaseIds.Contains(x.Id))
            .ToListAsync(cancellationToken);

        foreach (var release in touchedReleases)
        {
            var associatedOtherTracks = await _dbContext.AlbumTracks.CountAsync(
                x => x.AlbumId == release.Id && x.TrackId != trackId,
                cancellationToken);
            var legacyOtherTracks = await _dbContext.Tracks.CountAsync(
                candidate => candidate.Id != trackId &&
                             candidate.AlbumId == release.Id &&
                             !_dbContext.AlbumTracks.Any(
                                 association => association.AlbumId == release.Id &&
                                                association.TrackId == candidate.Id),
                cancellationToken);

            release.NumOfTracks = associatedOtherTracks + legacyOtherTracks +
                (requestedReleaseIds.Contains(release.Id) ? 1 : 0);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        var refreshedRow = new TrackRow(
            track.Id,
            track.Title,
            track.IsExplicit,
            track.Length,
            track.Streams,
            track.AlbumId);
        var result = await GetReleaseMap([refreshedRow], cancellationToken);
        return result.GetValueOrDefault(trackId) ?? [];
    }

    private async Task<IReadOnlyDictionary<int, IReadOnlyList<TrackArtistResponse>>> GetFeaturedArtists(
        int[] trackIds,
        CancellationToken cancellationToken)
    {
        if (trackIds.Length == 0)
        {
            return new Dictionary<int, IReadOnlyList<TrackArtistResponse>>();
        }

        var rows = await _dbContext.ArtistTracks
            .AsNoTracking()
            .Where(x => trackIds.Contains(x.TrackId) && !x.IsLead)
            .Select(x => new
            {
                x.TrackId,
                Artist = new TrackArtistResponse(
                    x.Id,
                    x.ArtistId,
                    x.Artist!.Name,
                    x.Artist.ProfilePhotoPath,
                    false,
                    x.ShowOnProfile)
            })
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(x => x.TrackId)
            .ToDictionary(
                x => x.Key,
                x => (IReadOnlyList<TrackArtistResponse>)x
                    .Select(row => row.Artist)
                    .OrderBy(row => row.Name)
                    .ToList());
    }

    private async Task<IReadOnlyDictionary<int, IReadOnlyList<TrackReleaseResponse>>> GetReleaseMap(
        IReadOnlyCollection<TrackRow> tracks,
        CancellationToken cancellationToken)
    {
        var trackIds = tracks.Select(x => x.Id).ToArray();
        if (trackIds.Length == 0)
        {
            return new Dictionary<int, IReadOnlyList<TrackReleaseResponse>>();
        }

        var associations = await _dbContext.AlbumTracks
            .AsNoTracking()
            .Where(x => trackIds.Contains(x.TrackId))
            .Select(x => new ReleaseRow(
                x.TrackId,
                x.Id,
                x.AlbumId,
                x.Album!.Title,
                x.Album.CoverPath,
                x.Album.ReleaseDate,
                x.Album.AlbumType!.Type,
                x.DiscNumber,
                x.TrackNumber,
                x.TitleOverride,
                x.IsPrimaryRelease,
                false))
            .ToListAsync(cancellationToken);

        var associatedKeys = associations
            .Select(x => (x.TrackId, x.ReleaseId))
            .ToHashSet();
        var legacyAlbumIds = tracks
            .Where(x => x.AlbumId.HasValue && !associatedKeys.Contains((x.Id, x.AlbumId.Value)))
            .Select(x => x.AlbumId!.Value)
            .Distinct()
            .ToArray();

        if (legacyAlbumIds.Length != 0)
        {
            var legacyAlbums = await _dbContext.Albums
                .AsNoTracking()
                .Where(x => legacyAlbumIds.Contains(x.Id))
                .Select(x => new
                {
                    x.Id,
                    x.Title,
                    x.CoverPath,
                    x.ReleaseDate,
                    ReleaseType = x.AlbumType!.Type
                })
                .ToDictionaryAsync(x => x.Id, cancellationToken);

            foreach (var track in tracks.Where(x => x.AlbumId.HasValue))
            {
                if (associatedKeys.Contains((track.Id, track.AlbumId!.Value)) ||
                    !legacyAlbums.TryGetValue(track.AlbumId.Value, out var album))
                {
                    continue;
                }

                associations.Add(new ReleaseRow(
                    track.Id,
                    null,
                    album.Id,
                    album.Title,
                    album.CoverPath,
                    album.ReleaseDate,
                    album.ReleaseType,
                    1,
                    track.Id,
                    null,
                    true,
                    true));
            }
        }

        return associations
            .GroupBy(x => x.TrackId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<TrackReleaseResponse>)group
                    .OrderByDescending(x => x.IsPrimaryRelease)
                    .ThenBy(x => x.ReleaseDate)
                    .Select(x => new TrackReleaseResponse(
                        x.AssociationId,
                        x.ReleaseId,
                        x.Title,
                        x.CoverPath,
                        x.ReleaseDate,
                        x.ReleaseType,
                        x.DiscNumber,
                        x.TrackNumber,
                        x.TitleOverride,
                        x.IsPrimaryRelease,
                        x.IsLegacyAssociation))
                    .ToList());
    }

    private static void ValidateReleaseInputs(IReadOnlyList<TrackReleaseInput> releases)
    {
        if (releases.GroupBy(x => x.ReleaseId).Any(x => x.Count() > 1))
        {
            throw new InvalidOperationException("A release can only be associated once.");
        }

        if (releases.Count(x => x.IsPrimaryRelease) > 1)
        {
            throw new InvalidOperationException("A track can have at most one primary release.");
        }

        if (releases.Any(x => x.ReleaseId <= 0))
        {
            throw new InvalidOperationException("Release ids must be greater than zero.");
        }

        foreach (var release in releases)
        {
            ReleaseTrackAssociationValidation.ValidatePosition(release.DiscNumber, release.TrackNumber);
            ReleaseTrackAssociationValidation.NormalizeTitleOverride(release.TitleOverride);
        }
    }

    private static string? NormalizeOptionalText(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private IQueryable<Track> ApplyCatalogSort(IQueryable<Track> tracks, TrackCatalogQuery query)
    {
        var sortBy = NormalizeOptionalText(query.SortBy)?.ToLowerInvariant();
        var descending = string.Equals(query.SortDirection, "desc", StringComparison.OrdinalIgnoreCase);

        return sortBy switch
        {
            "title" when descending => tracks.OrderByDescending(track => track.Title).ThenByDescending(track => track.Id),
            "title" => tracks.OrderBy(track => track.Title).ThenByDescending(track => track.Id),
            "primaryrelease" when descending => tracks
                .OrderByDescending(track =>
                    _dbContext.AlbumTracks
                        .Where(association => association.TrackId == track.Id && association.IsPrimaryRelease)
                        .Select(association => association.Album!.Title)
                        .FirstOrDefault() ??
                    _dbContext.Albums
                        .Where(release => track.AlbumId.HasValue && release.Id == track.AlbumId.Value)
                        .Select(release => release.Title)
                        .FirstOrDefault())
                .ThenByDescending(track => track.Id),
            "primaryrelease" => tracks
                .OrderBy(track =>
                    _dbContext.AlbumTracks
                        .Where(association => association.TrackId == track.Id && association.IsPrimaryRelease)
                        .Select(association => association.Album!.Title)
                        .FirstOrDefault() ??
                    _dbContext.Albums
                        .Where(release => track.AlbumId.HasValue && release.Id == track.AlbumId.Value)
                        .Select(release => release.Title)
                        .FirstOrDefault())
                .ThenByDescending(track => track.Id),
            "duration" when descending => tracks.OrderByDescending(track => track.Length).ThenByDescending(track => track.Id),
            "duration" => tracks.OrderBy(track => track.Length).ThenByDescending(track => track.Id),
            "streams" when descending => tracks.OrderByDescending(track => track.Streams).ThenByDescending(track => track.Id),
            "streams" => tracks.OrderBy(track => track.Streams).ThenByDescending(track => track.Id),
            "id" when !descending => tracks.OrderBy(track => track.Id),
            _ => tracks.OrderByDescending(track => track.Id)
        };
    }

    private sealed record TrackRow(
        int Id,
        string Title,
        bool IsExplicit,
        int LengthSeconds,
        int Streams,
        int? AlbumId);

    private sealed record ReleaseRow(
        int TrackId,
        int? AssociationId,
        int ReleaseId,
        string Title,
        string CoverPath,
        DateTime ReleaseDate,
        string ReleaseType,
        int DiscNumber,
        int TrackNumber,
        string? TitleOverride,
        bool IsPrimaryRelease,
        bool IsLegacyAssociation);
}

public sealed class TrackCatalogSearchHandler : ITrackCatalogSearchHandler
{
    private readonly IApplicationDbContext _dbContext;

    public TrackCatalogSearchHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<bool> ArtistExists(
        int artistId,
        CancellationToken cancellationToken = default) =>
        _dbContext.Artists.AsNoTracking().AnyAsync(x => x.Id == artistId, cancellationToken);

    public async Task<IReadOnlyList<ArtistSearchResponse>> SearchArtists(
        string? query,
        int? excludeArtistId,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var normalizedQuery = string.IsNullOrWhiteSpace(query) ? null : query.Trim();
        var normalizedLimit = Math.Clamp(limit, 1, 25);

        return await _dbContext.Artists
            .AsNoTracking()
            .Where(x => !excludeArtistId.HasValue || x.Id != excludeArtistId.Value)
            .Where(x => normalizedQuery == null || x.Name.Contains(normalizedQuery))
            .OrderBy(x => x.Name)
            .Take(normalizedLimit)
            .Select(x => new ArtistSearchResponse(x.Id, x.Name, x.ProfilePhotoPath))
            .ToListAsync(cancellationToken);
    }

    public async Task<PagedResult<ReleaseSearchResponse>> SearchReleases(
        ReleaseSearchQuery query,
        CancellationToken cancellationToken = default)
    {
        var normalizedTitle = string.IsNullOrWhiteSpace(query.Title) ? null : query.Title.Trim();

        var releases = _dbContext.Albums
            .AsNoTracking()
            .Where(x => x.ArtistId == query.ArtistId)
            .Where(x => normalizedTitle == null || x.Title.Contains(normalizedTitle));

        if (query.ExcludeTrackId.HasValue)
        {
            var trackId = query.ExcludeTrackId.Value;
            releases = releases.Where(release =>
                !_dbContext.AlbumTracks.Any(
                    association => association.AlbumId == release.Id && association.TrackId == trackId) &&
                !_dbContext.Tracks.Any(track => track.Id == trackId && track.AlbumId == release.Id));
        }

        var totalCount = await releases.CountAsync(cancellationToken);
        var items = await releases
            .OrderByDescending(x => x.ReleaseDate)
            .ThenBy(x => x.Title)
            .Skip((query.NormalizedPage - 1) * query.NormalizedPageSize)
            .Take(query.NormalizedPageSize)
            .Select(x => new ReleaseSearchResponse(
                x.Id,
                x.Title,
                x.CoverPath,
                x.ReleaseDate,
                x.AlbumType!.Type))
            .ToListAsync(cancellationToken);

        return new PagedResult<ReleaseSearchResponse>(
            items,
            query.NormalizedPage,
            query.NormalizedPageSize,
            totalCount);
    }
}

public sealed record TrackStatisticsPoint(DateTime Date, int Streams);

public sealed record TrackStatisticsResponse(
    int TrackId,
    int Days,
    DateTime From,
    DateTime To,
    int AllTimeStreams,
    long EstimatedAllTimeStreamedSeconds,
    int AllTimeUniqueListeners,
    int PeriodStreams,
    int PeriodUniqueListeners,
    IReadOnlyList<TrackStatisticsPoint> DailyStreams);

public interface ITrackStatisticsHandler
{
    Task<TrackStatisticsResponse?> Get(
        int trackId,
        int days,
        CancellationToken cancellationToken = default);
}

public sealed class TrackStatisticsHandler : ITrackStatisticsHandler
{
    private static readonly HashSet<int> AllowedRanges = [7, 30, 90, 365];
    private readonly IApplicationDbContext _dbContext;

    public TrackStatisticsHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<TrackStatisticsResponse?> Get(
        int trackId,
        int days,
        CancellationToken cancellationToken = default)
    {
        if (!AllowedRanges.Contains(days))
        {
            throw new InvalidOperationException("Statistics days must be one of 7, 30, 90, or 365.");
        }

        var track = await _dbContext.Tracks
            .AsNoTracking()
            .Where(x => x.Id == trackId)
            .Select(x => new { x.Id, x.Length, x.Streams })
            .FirstOrDefaultAsync(cancellationToken);
        if (track is null)
        {
            return null;
        }

        var today = DateTime.UtcNow.Date;
        var from = today.AddDays(-(days - 1));
        var toExclusive = today.AddDays(1);

        var allTimeUniqueListeners = await _dbContext.TrackStreams
            .AsNoTracking()
            .Where(x => x.TrackId == trackId && x.UserId.HasValue)
            .Select(x => x.UserId!.Value)
            .Distinct()
            .CountAsync(cancellationToken);

        var periodQuery = _dbContext.TrackStreams
            .AsNoTracking()
            .Where(x => x.TrackId == trackId && x.StreamedAt >= from && x.StreamedAt < toExclusive);

        var periodStreams = await periodQuery.CountAsync(cancellationToken);
        var periodUniqueListeners = await periodQuery
            .Where(x => x.UserId.HasValue)
            .Select(x => x.UserId!.Value)
            .Distinct()
            .CountAsync(cancellationToken);
        var grouped = await periodQuery
            .GroupBy(x => x.StreamedAt.Date)
            .Select(x => new { Date = x.Key, Streams = x.Count() })
            .ToDictionaryAsync(x => x.Date, x => x.Streams, cancellationToken);

        var points = Enumerable.Range(0, days)
            .Select(offset => from.AddDays(offset))
            .Select(date => new TrackStatisticsPoint(date, grouped.GetValueOrDefault(date)))
            .ToList();

        return new TrackStatisticsResponse(
            track.Id,
            days,
            from,
            today,
            track.Streams,
            (long)track.Length * track.Streams,
            allTimeUniqueListeners,
            periodStreams,
            periodUniqueListeners,
            points);
    }
}
