using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RS1_2024_25.API.Data;
using RS1_2024_25.API.Data.Models;
using RS1_2024_25.API.Helper;
using RS1_2024_25.API.Helper.Api;
using RS1_2024_25.API.Services;
using RS1_2024_25.API.Services.Interfaces;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text.RegularExpressions;

namespace RS1_2024_25.API.Endpoints.AlbumEndpoints
{
    public class AlbumInsertOrUpdateEndpoint(ApplicationDbContext db, IMyFileHandler fh, TokenProvider tp) : MyEndpointBaseAsync.WithRequest<AlbumInsertRequest>.WithActionResult<AlbumInsertResponse>
    {
        [Authorize]
        [HttpPost]
        public override async Task<ActionResult<AlbumInsertResponse>> HandleAsync([FromForm] AlbumInsertRequest request, CancellationToken cancellationToken = default)
        {
            /*
            if (!request.HandleValidation())
            {
                return BadRequest("Data not valid");
            }
            */

            var jwt = tp.GetDecodedJwt(Request);
            bool isAdmin = jwt.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role)!.Value == "Admin";
            int userId = int.Parse(jwt.Subject);
            //bool allowedToCreate = (await db.UserArtists.Where(ua => ua.ArtistId == request.ArtistId
            //                        && ua.MyAppUserId == userId
            //                        && (ua.Role.RoleName == "Owner" || ua.Role.RoleName == "General Manager" || ua.Role.RoleName == "Streaming Manager")).FirstOrDefaultAsync()) != null;

            bool allowedToCreate = tp.AuthorizeUserArtist(Request, request.ArtistId, ["Owner", "General Manager", "Streaming Manager"]);

            if (!isAdmin && !allowedToCreate)
            {
                return Unauthorized();
            }

            Album a;
            if (request.Id != null)
            {
                a = await db.Albums.FindAsync(request.Id, cancellationToken);
            }
            else
            {
                a = new Album();
                await db.Albums.AddAsync(a);
            }

            a.Title = request.Title;
            a.Distributor = request.Distributor;
            a.ReleaseDate = request.ReleaseDate;
            a.AlbumTypeId = request.AlbumTypeId;
            a.IsActive = request.ReleaseDate < DateTime.Now;
            a.ArtistId = request.ArtistId;

            if (request.CoverImage != null)
            {
                var path = await fh.UploadFileAsync(
                    Path.Combine("wwwroot", "Images", "AlbumCovers"),
                    request.CoverImage,
                    0,
                    cancellationToken);
                if (path == string.Empty)
                {
                    return BadRequest("Issue with file...");
                }
                a.CoverPath = path;
            }

            await db.SaveChangesAsync();

            //Add notification to db
            if(request.Id == null && request.ReleaseDate < DateTime.Now)
            {
                Notification notification = new Notification
                {
                    ArtistId = a.ArtistId,
                    ContentId = a.Id,
                    CreatedAt = DateTime.Now,
                    Sent = false,
                    Type = nameof(Album),
                };

                await db.Notifications.AddAsync(notification, cancellationToken);
                await db.SaveChangesAsync(cancellationToken);
            }

            var response = new AlbumInsertResponse
            {
                Id = a.Id,
                Title = a.Title,
                IsActive = a.IsActive,
                ReleaseDate = a.ReleaseDate,
                ByArtist = (await db.Artists.FindAsync(a.ArtistId, cancellationToken))?.Name ?? "Unknown",
            };

            return Ok(response);
        }
    }

    public class AlbumInsertRequest : IValidatable
    {
        public int? Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Distributor { get; set; } = string.Empty;
        public DateTime ReleaseDate { get; set; }
        public int AlbumTypeId { get; set; } = 4;
        public IFormFile? CoverImage { get; set; }
        public int ArtistId { get; set; }

        public bool HandleValidation()
        {
            if (Title.Length < 3)
                return false;
            if (Distributor.Length < 3) 
                return false;
            if(AlbumTypeId < 1)
                return false;
            return true;
        }
    }

    public class AlbumInsertResponse
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public bool IsActive { get; set; }
        public DateTime ReleaseDate { get; set; }
        public string ByArtist { get; set; }
    }
}
