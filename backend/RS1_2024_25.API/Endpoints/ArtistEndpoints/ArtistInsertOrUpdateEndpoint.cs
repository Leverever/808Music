using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RS1_2024_25.API.Data;
using RS1_2024_25.API.Data.Models;
using RS1_2024_25.API.Helper.Api;
using RS1_2024_25.API.Services;
using RS1_2024_25.API.Services.Interfaces;
using System.IdentityModel.Tokens.Jwt;

namespace RS1_2024_25.API.Endpoints.ArtistEndpoints
{
    public class ArtistInsertOrUpdateEndpoint(ApplicationDbContext db, IMyFileHandler fh, TokenProvider tp) : MyEndpointBaseAsync.WithRequest<ArtistInsertRequest>.WithActionResult<ArtistInsertResponse>
    {
        [Authorize]
        [HttpPost]
        public override async Task<ActionResult<ArtistInsertResponse>> HandleAsync([FromForm] ArtistInsertRequest request, CancellationToken cancellationToken = default)
        {
            int userId = int.Parse(tp.GetJwtSub(Request));
            bool authorized = true;

            if(request.Id != null)
            { 
                authorized = tp.AuthorizeUserArtist(Request, request.Id.Value, ["Owner", "General Manager", "Shop Manager", "Streaming Manager"]);
            }

            Artist? check = await db.Artists.FirstOrDefaultAsync(a => a.Name.ToLower() == request.Name.ToLower(), cancellationToken);
            if(check != null && !authorized)
            {
                return BadRequest("Artist with this name already exists");
            }

            string profilePhotoPath = string.Empty;
            string profileBackgroundPath = string.Empty;
            Artist? a = null;

            if (request.Id != null)
            {
                a = await db.Artists.FirstOrDefaultAsync(a => a.Id == request.Id);
                var userArtist = await db.UserArtists.FirstOrDefaultAsync(u => u.IsUserOwner && u.MyAppUserId == userId);
                if(userArtist == null)
                {
                    return Unauthorized();
                }
            }
            else
            {
                a = new Artist();
                await db.Artists.AddAsync(a, cancellationToken);
            }

            if(request.ProfilePhoto != null)
            {
                profilePhotoPath = await fh.UploadFileAsync(
                    Path.Combine("wwwroot", "Images", "ArtistPfps"),
                    request.ProfilePhoto!,
                    0,
                    cancellationToken);
                if(profilePhotoPath == string.Empty)
                {
                    return BadRequest("Issue with files");
                }
            }
            if (request.ProfileBackground != null)
            {
                profileBackgroundPath = await fh.UploadFileAsync(
                    Path.Combine("wwwroot", "Images", "ArtistBgs"),
                    request.ProfileBackground!,
                    0,
                    cancellationToken);
                if (profileBackgroundPath == string.Empty)
                {
                    return BadRequest("Issue with files");
                }
            }

            a!.Name = request.Name;
            a!.Bio = request.Bio;
            if(profilePhotoPath != string.Empty)
            {
                a!.ProfilePhotoPath = profilePhotoPath;
            }
            if(profileBackgroundPath != string.Empty)
            {
                a!.ProfileBackgroundPath = profileBackgroundPath;
            }

            await db.SaveChangesAsync(cancellationToken);

            if(request.Id == null)
            {
                int roleId = await db.UserArtistRoles.Where(uar => uar.RoleName == "Owner" ).Select(uar => uar.Id).FirstOrDefaultAsync();

                UserArtist ua = new UserArtist
                {
                    ArtistId = a.Id,
                    IsUserOwner = true,
                    MyAppUserId = userId,
                    RoleId = roleId,
                };
                await db.UserArtists.AddAsync(ua);
                await db.SaveChangesAsync(cancellationToken);
            }

            var response = new ArtistInsertResponse
            {
                Name = a.Name,
                Bio = a.Bio,
                ProfilePhotoPath = $"/media/Images/ArtistPfps/{profilePhotoPath}",
                ProfileBackgroundPath = $"/media/Images/ArtistBgs/{profilePhotoPath}"
            };

            return Ok(response);
        }
    }

    public class ArtistInsertRequest
    {
        public int? Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Bio { get; set; } = string.Empty;
        public IFormFile? ProfilePhoto { get; set; }
        public IFormFile? ProfileBackground { get; set; }
    }

    public class ArtistInsertResponse
    {
        public string Name { get; set; } = string.Empty;
        public string Bio { get; set; } = string.Empty;
        public string ProfilePhotoPath { get; set; } = string.Empty;
        public string ProfileBackgroundPath { get; set; } = string.Empty;
    }
}
