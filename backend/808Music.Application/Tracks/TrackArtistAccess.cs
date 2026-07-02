using Microsoft.EntityFrameworkCore;

namespace _808Music.Application.Tracks;

public interface ITrackArtistAccessQuery
{
    Task<int?> GetLeadArtistId(
        int trackId,
        CancellationToken cancellationToken = default);
}

public sealed class TrackArtistAccessQuery : ITrackArtistAccessQuery
{
    private readonly IApplicationDbContext _dbContext;

    public TrackArtistAccessQuery(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<int?> GetLeadArtistId(
        int trackId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.ArtistTracks
            .Where(x => x.TrackId == trackId && x.IsLead)
            .Select(x => (int?)x.ArtistId)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
