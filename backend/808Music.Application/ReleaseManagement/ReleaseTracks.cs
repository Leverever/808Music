using _808Music.Application.Common.Search;
using _808Music.Domain.Catalog;
using Microsoft.EntityFrameworkCore;

namespace _808Music.Application.Releases;

public sealed record CreateReleaseTrackCommand(
    int ReleaseId,
    int TrackId,
    int DiscNumber,
    int TrackNumber,
    string? TitleOverride,
    bool IsPrimaryRelease,
    bool AllowArtistMismatch = false);

public sealed record UpdateReleaseTrackCommand(
    int ReleaseId,
    int TrackId,
    int DiscNumber,
    int TrackNumber,
    string? TitleOverride,
    bool IsPrimaryRelease,
    bool AllowArtistMismatch = false);

public sealed record ReleaseTrackListQuery(
    int ReleaseId,
    int PageNumber = 1,
    int PageSize = 20,
    string? Title = null)
{
    public int NormalizedPage => Math.Max(PageNumber, 1);

    public int NormalizedPageSize => Math.Clamp(PageSize, 1, 500);
}

public sealed record ReleaseTrackArtistResponse(
    int Id,
    string Name,
    string ProfilePhotoPath,
    bool IsLead);

public sealed record ReleaseTrackResponse(
    int? AssociationId,
    int ReleaseId,
    int TrackId,
    string Title,
    string? TitleOverride,
    int DiscNumber,
    int TrackNumber,
    bool IsPrimaryRelease,
    bool IsExplicit,
    int Length,
    int Streams,
    string TrackPath,
    string CoverPath,
    bool IsLegacyAssociation,
    IReadOnlyList<ReleaseTrackArtistResponse> Artists);

public interface IReleaseTrackHandler
{
    Task<int?> GetReleaseArtistId(
        int releaseId,
        CancellationToken cancellationToken = default);

    Task<PagedResult<ReleaseTrackResponse>?> List(
        ReleaseTrackListQuery query,
        CancellationToken cancellationToken = default);

    Task<ReleaseTrackResponse?> Get(
        int releaseId,
        int trackId,
        CancellationToken cancellationToken = default);

    Task<ReleaseTrackResponse> Create(
        CreateReleaseTrackCommand command,
        CancellationToken cancellationToken = default);

    Task<ReleaseTrackResponse?> Update(
        UpdateReleaseTrackCommand command,
        CancellationToken cancellationToken = default);

    Task<bool> Delete(
        int releaseId,
        int trackId,
        CancellationToken cancellationToken = default);
}

public sealed class ReleaseTrackHandler : IReleaseTrackHandler
{
    private readonly IApplicationDbContext _dbContext;

    public ReleaseTrackHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<int?> GetReleaseArtistId(
        int releaseId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.Albums
            .AsNoTracking()
            .Where(x => x.Id == releaseId)
            .Select(x => (int?)x.ArtistId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<PagedResult<ReleaseTrackResponse>?> List(
        ReleaseTrackListQuery query,
        CancellationToken cancellationToken = default)
    {
        var release = await _dbContext.Albums
            .AsNoTracking()
            .Where(x => x.Id == query.ReleaseId)
            .Select(x => new { x.Id, x.CoverPath })
            .FirstOrDefaultAsync(cancellationToken);
        if (release is null)
        {
            return null;
        }

        var normalizedTitle = string.IsNullOrWhiteSpace(query.Title)
            ? null
            : query.Title.Trim();

        var totalCount = await (
            from track in _dbContext.Tracks.AsNoTracking()
            join releaseAssociation in _dbContext.AlbumTracks
                    .AsNoTracking()
                    .Where(x => x.AlbumId == query.ReleaseId)
                on track.Id equals releaseAssociation.TrackId into trackAssociations
            from association in trackAssociations.DefaultIfEmpty()
            where (association != null || track.AlbumId == query.ReleaseId) &&
                  (normalizedTitle == null ||
                   (association != null && association.TitleOverride != null
                       ? association.TitleOverride
                       : track.Title).Contains(normalizedTitle))
            select track.Id)
            .CountAsync(cancellationToken);

        var rows = await (
            from track in _dbContext.Tracks.AsNoTracking()
            join releaseAssociation in _dbContext.AlbumTracks
                    .AsNoTracking()
                    .Where(x => x.AlbumId == query.ReleaseId)
                on track.Id equals releaseAssociation.TrackId into trackAssociations
            from association in trackAssociations.DefaultIfEmpty()
            where (association != null || track.AlbumId == query.ReleaseId) &&
                  (normalizedTitle == null ||
                   (association != null && association.TitleOverride != null
                       ? association.TitleOverride
                       : track.Title).Contains(normalizedTitle))
            orderby association == null ? 1 : 0,
                association == null ? 1 : association.DiscNumber,
                association == null ? track.Id : association.TrackNumber,
                track.Id
            select new ReleaseTrackRow(
                association == null ? null : association.Id,
                track.Id,
                association != null && association.TitleOverride != null
                    ? association.TitleOverride
                    : track.Title,
                association == null ? null : association.TitleOverride,
                association == null ? 1 : association.DiscNumber,
                association == null ? track.Id : association.TrackNumber,
                association == null
                    ? track.AlbumId == query.ReleaseId
                    : association.IsPrimaryRelease,
                track.IsExplicit,
                track.Length,
                track.Streams,
                track.TrackPath,
                association == null))
            .Skip((query.NormalizedPage - 1) * query.NormalizedPageSize)
            .Take(query.NormalizedPageSize)
            .ToListAsync(cancellationToken);

        var artistsByTrack = await GetArtistsByTrackId(
            rows.Select(x => x.TrackId).ToArray(),
            cancellationToken);

        var responses = rows.Select(row => ToResponse(
                row,
                release.Id,
                release.CoverPath,
                artistsByTrack.GetValueOrDefault(row.TrackId) ?? []))
            .ToList();

        return new PagedResult<ReleaseTrackResponse>(
            responses,
            query.NormalizedPage,
            query.NormalizedPageSize,
            totalCount);
    }

    public async Task<ReleaseTrackResponse?> Get(
        int releaseId,
        int trackId,
        CancellationToken cancellationToken = default)
    {
        var release = await _dbContext.Albums
            .AsNoTracking()
            .Where(x => x.Id == releaseId)
            .Select(x => new { x.Id, x.CoverPath })
            .FirstOrDefaultAsync(cancellationToken);
        if (release is null)
        {
            return null;
        }

        var row = await (
            from track in _dbContext.Tracks.AsNoTracking()
            join releaseAssociation in _dbContext.AlbumTracks
                    .AsNoTracking()
                    .Where(x => x.AlbumId == releaseId)
                on track.Id equals releaseAssociation.TrackId into trackAssociations
            from association in trackAssociations.DefaultIfEmpty()
            where track.Id == trackId &&
                  (association != null || track.AlbumId == releaseId)
            select new ReleaseTrackRow(
                association == null ? null : association.Id,
                track.Id,
                association != null && association.TitleOverride != null
                    ? association.TitleOverride
                    : track.Title,
                association == null ? null : association.TitleOverride,
                association == null ? 1 : association.DiscNumber,
                association == null ? track.Id : association.TrackNumber,
                association == null
                    ? track.AlbumId == releaseId
                    : association.IsPrimaryRelease,
                track.IsExplicit,
                track.Length,
                track.Streams,
                track.TrackPath,
                association == null))
            .FirstOrDefaultAsync(cancellationToken);
        if (row is null)
        {
            return null;
        }

        var artistsByTrack = await GetArtistsByTrackId([trackId], cancellationToken);
        return ToResponse(
            row,
            release.Id,
            release.CoverPath,
            artistsByTrack.GetValueOrDefault(trackId) ?? []);
    }

    public async Task<ReleaseTrackResponse> Create(
        CreateReleaseTrackCommand command,
        CancellationToken cancellationToken = default)
    {
        ReleaseTrackAssociationValidation.ValidatePosition(command.DiscNumber, command.TrackNumber);

        var release = await _dbContext.Albums.FindAsync([command.ReleaseId], cancellationToken)
            ?? throw new KeyNotFoundException("Release was not found.");
        var track = await _dbContext.Tracks.FindAsync([command.TrackId], cancellationToken)
            ?? throw new KeyNotFoundException("Track was not found.");

        if (!command.AllowArtistMismatch)
        {
            await EnsureTrackBelongsToReleaseArtist(command.TrackId, release.ArtistId, cancellationToken);
        }

        var existingAssociations = await _dbContext.AlbumTracks
            .Where(x => x.TrackId == command.TrackId)
            .ToListAsync(cancellationToken);

        if (existingAssociations.Any(x => x.AlbumId == command.ReleaseId))
        {
            throw new InvalidOperationException("The track is already associated with this release.");
        }

        await EnsurePositionAvailable(
            command.ReleaseId,
            command.DiscNumber,
            command.TrackNumber,
            excludedTrackId: null,
            cancellationToken);

        var association = new AlbumTrack
        {
            AlbumId = command.ReleaseId,
            TrackId = command.TrackId,
            DiscNumber = command.DiscNumber,
            TrackNumber = command.TrackNumber,
            TitleOverride = ReleaseTrackAssociationValidation.NormalizeTitleOverride(command.TitleOverride),
            IsPrimaryRelease = command.IsPrimaryRelease
        };

        if (association.IsPrimaryRelease)
        {
            ClearPrimaryRelease(existingAssociations);
            track.AlbumId = release.Id;
        }

        release.NumOfTracks = await _dbContext.AlbumTracks
            .CountAsync(x => x.AlbumId == release.Id, cancellationToken) + 1;

        await _dbContext.AlbumTracks.AddAsync(association, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return await ToResponse(association, track, release, cancellationToken);
    }

    public async Task<ReleaseTrackResponse?> Update(
        UpdateReleaseTrackCommand command,
        CancellationToken cancellationToken = default)
    {
        ReleaseTrackAssociationValidation.ValidatePosition(command.DiscNumber, command.TrackNumber);

        var release = await _dbContext.Albums.FindAsync([command.ReleaseId], cancellationToken);
        if (release is null)
        {
            return null;
        }

        var association = await _dbContext.AlbumTracks
            .FirstOrDefaultAsync(
                x => x.AlbumId == command.ReleaseId && x.TrackId == command.TrackId,
                cancellationToken);
        if (association is null)
        {
            return null;
        }

        var track = await _dbContext.Tracks.FindAsync([command.TrackId], cancellationToken);
        if (track is null)
        {
            return null;
        }

        if (!command.AllowArtistMismatch)
        {
            await EnsureTrackBelongsToReleaseArtist(command.TrackId, release.ArtistId, cancellationToken);
        }
        await EnsurePositionAvailable(
            command.ReleaseId,
            command.DiscNumber,
            command.TrackNumber,
            command.TrackId,
            cancellationToken);

        var trackAssociations = await _dbContext.AlbumTracks
            .Where(x => x.TrackId == command.TrackId)
            .ToListAsync(cancellationToken);

        association.DiscNumber = command.DiscNumber;
        association.TrackNumber = command.TrackNumber;
        association.TitleOverride = ReleaseTrackAssociationValidation.NormalizeTitleOverride(command.TitleOverride);
        association.IsPrimaryRelease = command.IsPrimaryRelease;

        if (association.IsPrimaryRelease)
        {
            ClearPrimaryRelease(trackAssociations, association.Id);
            track.AlbumId = release.Id;
        }
        else if (track.AlbumId == release.Id)
        {
            track.AlbumId = trackAssociations
                .FirstOrDefault(x => x.Id != association.Id && x.IsPrimaryRelease)
                ?.AlbumId;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return await ToResponse(association, track, release, cancellationToken);
    }

    public async Task<bool> Delete(
        int releaseId,
        int trackId,
        CancellationToken cancellationToken = default)
    {
        var release = await _dbContext.Albums.FindAsync([releaseId], cancellationToken);
        if (release is null)
        {
            return false;
        }

        var association = await _dbContext.AlbumTracks
            .FirstOrDefaultAsync(
                x => x.AlbumId == releaseId && x.TrackId == trackId,
                cancellationToken);
        if (association is null)
        {
            return false;
        }

        var track = await _dbContext.Tracks.FindAsync([trackId], cancellationToken);
        if (track is not null && track.AlbumId == releaseId)
        {
            track.AlbumId = await _dbContext.AlbumTracks
                .AsNoTracking()
                .Where(x => x.TrackId == trackId && x.Id != association.Id && x.IsPrimaryRelease)
                .Select(x => (int?)x.AlbumId)
                .FirstOrDefaultAsync(cancellationToken);
        }

        release.NumOfTracks = Math.Max(
            0,
            await _dbContext.AlbumTracks.CountAsync(x => x.AlbumId == releaseId, cancellationToken) - 1);

        _dbContext.AlbumTracks.Remove(association);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }

    private async Task EnsureTrackBelongsToReleaseArtist(
        int trackId,
        int releaseArtistId,
        CancellationToken cancellationToken)
    {
        var belongsToArtist = await _dbContext.ArtistTracks
            .AnyAsync(
                x => x.TrackId == trackId && x.ArtistId == releaseArtistId && x.IsLead,
                cancellationToken);

        if (!belongsToArtist)
        {
            throw new InvalidOperationException("The track's lead artist does not match the release artist.");
        }
    }

    private async Task EnsurePositionAvailable(
        int releaseId,
        int discNumber,
        int trackNumber,
        int? excludedTrackId,
        CancellationToken cancellationToken)
    {
        var positionTaken = await _dbContext.AlbumTracks.AnyAsync(
            x => x.AlbumId == releaseId &&
                 x.DiscNumber == discNumber &&
                 x.TrackNumber == trackNumber &&
                 (!excludedTrackId.HasValue || x.TrackId != excludedTrackId.Value),
            cancellationToken);

        if (positionTaken)
        {
            throw new InvalidOperationException("The release already contains a track at this disc and track number.");
        }
    }

    private static void ClearPrimaryRelease(
        IEnumerable<AlbumTrack> associations,
        int? excludedAssociationId = null)
    {
        foreach (var association in associations.Where(
                     x => x.IsPrimaryRelease &&
                          (!excludedAssociationId.HasValue || x.Id != excludedAssociationId.Value)))
        {
            association.IsPrimaryRelease = false;
        }
    }

    private async Task<ReleaseTrackResponse> ToResponse(
        AlbumTrack association,
        Track track,
        Album release,
        CancellationToken cancellationToken)
    {
        var artistsByTrack = await GetArtistsByTrackId([track.Id], cancellationToken);

        return new ReleaseTrackResponse(
            association.Id,
            association.AlbumId,
            association.TrackId,
            association.TitleOverride ?? track.Title,
            association.TitleOverride,
            association.DiscNumber,
            association.TrackNumber,
            association.IsPrimaryRelease,
            track.IsExplicit,
            track.Length,
            track.Streams,
            track.TrackPath,
            release.CoverPath,
            IsLegacyAssociation: false,
            artistsByTrack.GetValueOrDefault(track.Id) ?? []);
    }

    private async Task<IReadOnlyDictionary<int, IReadOnlyList<ReleaseTrackArtistResponse>>> GetArtistsByTrackId(
        int[] trackIds,
        CancellationToken cancellationToken)
    {
        if (trackIds.Length == 0)
        {
            return new Dictionary<int, IReadOnlyList<ReleaseTrackArtistResponse>>();
        }

        var credits = await _dbContext.ArtistTracks
            .AsNoTracking()
            .Where(x => trackIds.Contains(x.TrackId))
            .Select(x => new ReleaseTrackArtistRow(
                x.TrackId,
                x.ArtistId,
                x.Artist!.Name,
                x.Artist.ProfilePhotoPath,
                x.IsLead))
            .ToListAsync(cancellationToken);

        return credits
            .GroupBy(x => x.TrackId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<ReleaseTrackArtistResponse>)group
                    .OrderByDescending(x => x.IsLead)
                    .ThenBy(x => x.Name)
                    .Select(x => new ReleaseTrackArtistResponse(
                        x.ArtistId,
                        x.Name,
                        x.ProfilePhotoPath,
                        x.IsLead))
                    .ToList());
    }

    private static ReleaseTrackResponse ToResponse(
        ReleaseTrackRow row,
        int releaseId,
        string coverPath,
        IReadOnlyList<ReleaseTrackArtistResponse> artists)
    {
        return new ReleaseTrackResponse(
            row.AssociationId,
            releaseId,
            row.TrackId,
            row.Title,
            row.TitleOverride,
            row.DiscNumber,
            row.TrackNumber,
            row.IsPrimaryRelease,
            row.IsExplicit,
            row.Length,
            row.Streams,
            row.TrackPath,
            coverPath,
            row.IsLegacyAssociation,
            artists);
    }

    private sealed record ReleaseTrackRow(
        int? AssociationId,
        int TrackId,
        string Title,
        string? TitleOverride,
        int DiscNumber,
        int TrackNumber,
        bool IsPrimaryRelease,
        bool IsExplicit,
        int Length,
        int Streams,
        string TrackPath,
        bool IsLegacyAssociation);

    private sealed record ReleaseTrackArtistRow(
        int TrackId,
        int ArtistId,
        string Name,
        string ProfilePhotoPath,
        bool IsLead);
}
