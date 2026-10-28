using Microsoft.EntityFrameworkCore;
using RS1_2024_25.API.Data;

namespace RS1_2024_25.API.Endpoints.TrackEndpoints;

public static class TrackArtistResponseEnricher
{
    public static async Task AddAlbumArtistFallbacksAsync(
        ApplicationDbContext db,
        IEnumerable<TrackGetResponse> tracks,
        CancellationToken cancellationToken = default)
    {
        var responsesById = tracks
            .Where(track => track.Artists is not { Count: > 0 })
            .GroupBy(track => track.Id)
            .ToDictionary(group => group.Key, group => group.ToArray());

        if (responsesById.Count == 0)
        {
            return;
        }

        var missingTrackIds = responsesById.Keys.ToArray();
        var fallbacks = await db.Tracks
            .AsNoTracking()
            .Where(track => missingTrackIds.Contains(track.Id) && track.Album != null && track.Album.Artist != null)
            .Select(track => new
            {
                TrackId = track.Id,
                Artist = new ArtistTrackDto
                {
                    Id = track.Album!.ArtistId,
                    IsLead = true,
                    Name = track.Album.Artist!.Name,
                    PfpPath = "/media/Images/ArtistPfps/" + track.Album.Artist.ProfilePhotoPath
                }
            })
            .ToListAsync(cancellationToken);

        foreach (var fallback in fallbacks)
        {
            foreach (var response in responsesById[fallback.TrackId])
            {
                response.Artists = [fallback.Artist];
            }
        }
    }
}
