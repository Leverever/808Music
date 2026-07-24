using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RS1_2024_25.API.Data;
using RS1_2024_25.API.Endpoints.TrackEndpoints;
using RS1_2024_25.API.Helper;
using RS1_2024_25.API.Helper.Api;

namespace RS1_2024_25.API.Endpoints.PlaylistEndpoints
{
   public class PlaylistTracksGetEndpoint(ApplicationDbContext db) : MyEndpointBaseAsync
    .WithRequest<PlaylistTracksGetRequest>
    .WithActionResult<MyPagedList<TrackGetResponse>>
{
    [Authorize]
    [HttpGet]
    public override async Task<ActionResult<MyPagedList<TrackGetResponse>>> HandleAsync([FromQuery] PlaylistTracksGetRequest request, CancellationToken cancellationToken = default)
    {
        var tracks = db.PlaylistTracks
            .Where(pt => pt.PlaylistId == request.PlaylistId)
            .OrderBy(pt => pt.TrackOrder)
            .Select(pt => pt.Track)
            .AsQueryable();

        await db.Artists.LoadAsync();
        await db.Albums.LoadAsync();

        if (request.FeaturedArtists != null)
        {
            foreach (int artistId in request.FeaturedArtists)
            {
                tracks = db.ArtistsTracks
                    .Where(at => at.ArtistId == artistId && !at.IsLead)
                    .Select(at => at.Track)
                    .Where(t => tracks.Contains(t));
            }
        }

        if (request.IsReleased != null)
        {
            tracks = tracks.Where(t => t.Album.ReleaseDate < DateTime.Now);
        }

        if (request.AlbumId != null)
        {
            tracks = tracks.Where(t => t.AlbumId == request.AlbumId);
        }

        if (request.LeadArtistId != null)
        {
            tracks = tracks.Where(t => t.Album.ArtistId == request.LeadArtistId);
        }

        if (!string.IsNullOrWhiteSpace(request.Title))
        {
            var searchTerm = request.Title.Trim().ToLower();
            tracks = tracks.Where(t =>
                t.Title.ToLower().Contains(searchTerm)
                || db.ArtistsTracks.Any(at =>
                    at.TrackId == t.Id && at.Artist.Name.ToLower().Contains(searchTerm)));
        }

        if (request.SortByStreams)
        {
            tracks = tracks.OrderByDescending(t => t.Streams);
        }
        else
        {
            tracks = tracks.OrderByDescending(t => t.Album!.ReleaseDate);
        }

        var filteredTracks = tracks.Select(t => new TrackGetResponse
        {
            Id = t.Id,
            isExplicit = t.isExplicit,
            Length = t.Length,
            Streams = t.Streams,
            Title = t.Title,
            CoverPath = "/media/Images/AlbumCovers/" + t.Album.CoverPath,
            Artists = db.ArtistsTracks.Where(at => at.TrackId == t.Id).Select(at => new ArtistTrackDto
            {
                Id = at.ArtistId,
                IsLead = at.IsLead,
                Name = at.Artist.Name,
                PfpPath = "/media/Images/ArtistPfps/" + at.Artist.ProfilePhotoPath,
            }).ToList(),
            TrackUserInfo = db.PlaylistTracks
                    .Where(tu => tu.TrackId == t.Id && tu.PlaylistId == request.PlaylistId)
                    .OrderByDescending(tu => tu.DateAdded) 
                    .Select(tu => new TrackUserInfoDto
                    {
                        AddedByUsername = tu.AddedByUser.Username,
                        AddedByUserProfilePhotoPath = tu.AddedByUser.pfpCoverPath
                    })
                    .FirstOrDefault() ,
            AlbumId = request.PlaylistId,
        });

        return await MyPagedList<TrackGetResponse>.CreateAsync(filteredTracks, request, cancellationToken);
    }
}


    public class PlaylistTracksGetRequest : MyPagingRequest
    {
        public int PlaylistId { get; set; }
        public int? AlbumId { get; set; }
        public int? LeadArtistId { get; set; }
        public List<int>? FeaturedArtists { get; set; }
        public string Title { get; set; } = string.Empty;
        public bool? IsReleased { get; set; }
        public bool SortByStreams { get; set; } = false;
    }
}
