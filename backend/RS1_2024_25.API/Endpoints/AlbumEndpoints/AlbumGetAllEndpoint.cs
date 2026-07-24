using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RS1_2024_25.API.Data;
using RS1_2024_25.API.Data.Models;
using RS1_2024_25.API.Helper;
using RS1_2024_25.API.Helper.Api;
using RS1_2024_25.API.Services;
using Stripe;
using System.Collections.Immutable;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace RS1_2024_25.API.Endpoints.AlbumEndpoints
{
    public class AlbumGetAllEndpoint(ApplicationDbContext db, TokenProvider tp) : MyEndpointBaseAsync.WithRequest<AlbumGetAllRequest>.WithActionResult<MyPagedList<AlbumGetResponse>>
    {
        [HttpGet]
        [Authorize]
        public override async Task<ActionResult<MyPagedList<AlbumGetResponse>>> HandleAsync([FromQuery] AlbumGetAllRequest request, CancellationToken cancellationToken = default)
        {
            var albums = db.Albums.AsQueryable();
            var allAlbums = db.Albums.AsQueryable();
            IQueryable<AlbumGetAllResponse> albumsResponse;

            //filters
            if (request.FeaturedArtistId != null)
            {
                var featuredTrackIds = db.ArtistsTracks
                    .Where(at => at.ArtistId == request.FeaturedArtistId && !at.IsLead)
                    .Select(at => at.TrackId);
                var legacyAlbumIds = db.Tracks
                    .Where(track => featuredTrackIds.Contains(track.Id))
                    .Select(track => track.AlbumId);
                var associatedAlbumIds = db.AlbumTrackAssociations
                    .Where(association => featuredTrackIds.Contains(association.TrackId))
                    .Select(association => association.AlbumId);

                albums = albums.Where(album =>
                    legacyAlbumIds.Contains(album.Id) || associatedAlbumIds.Contains(album.Id));
            }


            if (request.ArtistId != null)
            {
                albums = albums.Where(a => a.ArtistId == request.ArtistId);
            }

            if (request.TypeId != null)
            {
                albums = albums.Where(a => a.AlbumTypeId == request.TypeId);
            }

            if (request.IsReleased != null)
            {
                albums = albums.Where(a => (DateTime.UtcNow > a.ReleaseDate && (bool)request.IsReleased) || (DateTime.UtcNow < a.ReleaseDate && !(bool)request.IsReleased));
            }

            if (request.Title != string.Empty)
            {
                albums = albums.Where(a => a.Title.ToLower().Contains(request.Title.ToLower()));
            }

            if (request.PeriodTo != null)
            {
                albums = albums.Where(a => request.PeriodTo.Value >= a.ReleaseDate);
            }

            if (request.PeriodFrom != null)
            {
                albums = albums.Where(a => request.PeriodFrom.Value <= a.ReleaseDate);
            }

            await db.AlbumTypes.LoadAsync();

            albumsResponse = albums.OrderByDescending(a => a.ReleaseDate).Select(a => new AlbumGetAllResponse
            {
                Id = a.Id,
                Artist = a.Artist.Name,
                ArtistId = a.ArtistId,
                CoverArt = a.CoverPath != "" ? $"/media/Images/AlbumCovers/{a.CoverPath}" : "/media/Images/playlist_placeholder.png",
                ReleaseDate = a.ReleaseDate,
                Title = a.Title,
                Type = a.AlbumType.Type,
                TrackCount = db.Tracks.Count(track =>
                    track.AlbumId == a.Id ||
                    db.AlbumTrackAssociations.Any(association =>
                        association.AlbumId == a.Id && association.TrackId == track.Id)),
                IsHighlighted = db.AlbumSpotlights.Where(s => s.ArtistId == a.ArtistId).Count() > 0 ?
                                db.AlbumSpotlights.Where(s => s.AlbumId == a.Id).FirstOrDefault() != null : 
                                albums.Where(s => s.ReleaseDate < DateTime.Now).OrderByDescending(s => s.ReleaseDate).FirstOrDefault() == null ? false :
                                albums.Where(s => s.ReleaseDate < DateTime.Now && db.Tracks.Any(track =>
                                    track.AlbumId == s.Id ||
                                    db.AlbumTrackAssociations.Any(association =>
                                        association.AlbumId == s.Id && association.TrackId == track.Id)))
                                    .OrderByDescending(s => s.ReleaseDate).FirstOrDefault().Id == a.Id,
            });

            if (request.IsReleased != null && request.IsReleased.Value && (request.ArtistMode == null || (request.ArtistMode != null && !request.ArtistMode.Value)))
            {
                albumsResponse = albumsResponse.Where(a => a.TrackCount > 0);
            }

            if (request.SortByPopularity)
            {
                albumsResponse = albumsResponse.OrderByDescending(a => db.Tracks
                    .Where(track =>
                        track.AlbumId == a.Id ||
                        db.AlbumTrackAssociations.Any(association =>
                            association.AlbumId == a.Id && association.TrackId == track.Id))
                    .Sum(track => track.Streams));
            }

            if(request.GetUnlisted != null && request.GetUnlisted.Value)
            {
                albumsResponse = albumsResponse.Where(a => a.TrackCount == 0);
            }

            var pagedList = await MyPagedList<AlbumGetAllResponse>.CreateAsync(albumsResponse, request, cancellationToken);
            return Ok(pagedList);
        }
    }

    public class AlbumGetAllRequest : MyPagingRequest
    {
        public int? ArtistId { get; set; }
        public int? TypeId { get; set; }
        public bool? IsReleased { get; set; }
        public string Title { get; set; } = string.Empty;
        public int? FeaturedArtistId { get; set; }
        public bool GetTrackCount { get; set; } = false;
        public DateTime? PeriodFrom { get; set; }
        public DateTime? PeriodTo { get; set; }
        public bool SortByPopularity { get; set; } = false;
        public bool? ArtistMode { get; set; }
        public bool? GetUnlisted { get; set; }
    }

    public class AlbumGetAllResponse {
        public int Id { get; set; }
        public string Title { get; set; }
        public string CoverArt {  get; set; }
        public DateTime ReleaseDate { get; set; }
        public string Artist { get; set; }
        public string Type { get; set; }
        public int TrackCount { get; set; } = -1;
        public int ArtistId { get; set; }
        public bool IsHighlighted { get; set; }
    }
}
