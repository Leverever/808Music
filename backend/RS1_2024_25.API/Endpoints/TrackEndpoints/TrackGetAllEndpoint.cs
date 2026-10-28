using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RS1_2024_25.API.Data;
using RS1_2024_25.API.Helper;
using RS1_2024_25.API.Helper.Api;

namespace RS1_2024_25.API.Endpoints.TrackEndpoints
{
    public class TrackGetAllEndpoint(ApplicationDbContext db, IConfiguration cfg) : MyEndpointBaseAsync
        .WithRequest<TrackGetAllRequest>
        .WithActionResult<MyPagedList<TrackGetResponse>>
    {
        [HttpGet]
        public override async Task<ActionResult<MyPagedList<TrackGetResponse>>> HandleAsync([FromQuery]TrackGetAllRequest request, CancellationToken cancellationToken = default)
        {
            var tracks = db.Tracks.AsQueryable();
            await db.Artists.LoadAsync();
            await db.Albums.LoadAsync();

            


            if (request.FeaturedArtists != null)
            {
                foreach(int artistId in request.FeaturedArtists)
                {
                    tracks = db.ArtistsTracks.Where(db => db.ArtistId == artistId && !db.IsLead).Select(at => at.Track)!;
                }
            }

            if (request.IsReleased != null)
            {
                tracks = tracks.Where(t => t.Album.ReleaseDate < DateTime.Now);
            }

            if(request.AlbumId != null)
            {
                tracks = tracks.Where(t => t.AlbumId == request.AlbumId);
            }

            if(request.LeadArtistId != null)
            {
                tracks = tracks.Where(t => t.Album.ArtistId == request.LeadArtistId);
            }

            if(request.Title != string.Empty)
            {
                tracks = tracks.Where(t => t.Title.ToLower().Contains(request.Title.ToLower()));
            }

            if(request.SortByStreams)
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
                AlbumId = t.AlbumId
            });

            var response = await MyPagedList<TrackGetResponse>.CreateAsync(filteredTracks, request, cancellationToken);
            await TrackArtistResponseEnricher.AddAlbumArtistFallbacksAsync(db, response.DataItems, cancellationToken);
            return response;
        }
    }

    public class TrackGetAllRequest : MyPagingRequest
    {
        public int? AlbumId { get; set; }
        public int? LeadArtistId { get; set; }
        public List<int>? FeaturedArtists { get; set; }
        public string Title { get; set; } = string.Empty;
        public bool? IsReleased { get; set; }
        public bool SortByStreams { get; set; } = false;
    }
}
