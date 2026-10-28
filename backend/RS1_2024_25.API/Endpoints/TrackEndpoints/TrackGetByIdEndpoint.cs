using RS1_2024_25.API.Data.Models;
using RS1_2024_25.API.Helper.Api;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using RS1_2024_25.API.Endpoints.ArtistEndpoints;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using RS1_2024_25.API.Data;
using Microsoft.EntityFrameworkCore;

namespace RS1_2024_25.API.Endpoints.TrackEndpoints
{
    public class TrackGetByIdEndpoint(ApplicationDbContext db) : MyEndpointBaseAsync.WithRequest<int>.WithActionResult<TrackGetResponse>
    {
        [HttpGet("{id}")]
        public override async Task<ActionResult<TrackGetResponse>> HandleAsync(int id, CancellationToken cancellationToken = default)
        {
            Track? track = await db.Tracks.Where(t => t.Id == id).FirstOrDefaultAsync(cancellationToken);
            if(track == null)
            {
                BadRequest("Track not found");
            }
            await db.Albums.Where(a => a.Id == track.AlbumId).LoadAsync(cancellationToken);
            List<ArtistTrackDto> artists = await db.ArtistsTracks.Where(at => at.TrackId == id).Select(at => new ArtistTrackDto
            {
                Id = at.ArtistId,
                Name = at.Artist!.Name,
                IsLead = at.IsLead,
                PfpPath = "/media/Images/ArtistPfps/" + at.Artist.ProfilePhotoPath
            }).ToListAsync();

            var response = new TrackGetResponse
            {
                Id = track.Id,
                Title = track.Title,
                Artists = artists,
                isExplicit = track.isExplicit,
                Length = track.Length,
                Streams = track.Streams,
                CoverPath = "/media/Images/AlbumCovers/" + track.Album.CoverPath,
                AlbumId = track.Album.Id
            };

            await TrackArtistResponseEnricher.AddAlbumArtistFallbacksAsync(db, [response], cancellationToken);

            return Ok(response);
        }
    }

    public class TrackGetResponse
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public int Length { get; set; }
        public int Streams { get; set; }
        public bool isExplicit { get; set; }
        public string CoverPath { get; set; } = string.Empty;
        public List<ArtistTrackDto> Artists { get; set; }
        public int AlbumId { get; set; }
        public TrackUserInfoDto TrackUserInfo { get; set; }

    }

    public class ArtistTrackDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string PfpPath { get; set; } = string.Empty;
        public bool IsLead { get; set; }
    }
    public class TrackUserInfoDto
    {
        public string AddedByUsername { get; set; }
        public string AddedByUserProfilePhotoPath { get; set; }
    }
}
