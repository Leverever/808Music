using Microsoft.AspNetCore.Mvc;
using RS1_2024_25.API.Data;
using RS1_2024_25.API.Data.Models;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace RS1_2024_25.API.Endpoints.PlaylistEndpoints
{
    public class PlaylistCreateEndpoint : ControllerBase
    {
        private readonly ApplicationDbContext _db;

        public PlaylistCreateEndpoint(ApplicationDbContext db, Services.TokenProvider tokenProvider)
        {
            _db = db;
        }

        [HttpPost]
        [Route("api/playlists/create")]
      public async Task<ActionResult<PlaylistAddResponse>> HandleAsync([FromForm] PlaylistAddRequest request, CancellationToken cancellationToken = default)
{
    int userId;

    if (request.UserId.HasValue)
    {
        userId = request.UserId.Value;
    }
    else if (User?.Identity?.IsAuthenticated == true)
    {
        var userClaim = User.Claims.FirstOrDefault(c => c.Type == "userId")?.Value;
        if (string.IsNullOrEmpty(userClaim))
        {
            return Unauthorized(); 
        }
        userId = int.Parse(userClaim);
    }
    else
    {
        return Unauthorized();  
    }

    var user = await _db.MyAppUsers.FindAsync(userId);
    if (user == null)
    {
        return NotFound("Korisnik nije pronađen.");
    }

    var playlist = new Playlist
    {
        Title = request.Title,
        NumOfTracks = 0,
        IsPublic = request.IsPublic,
        CoverPath = "/Images/playlist_placeholder.png"
    };

    _db.Playlists.Add(playlist);
    await _db.SaveChangesAsync(cancellationToken);

    var userPlaylist = new UserPlaylist
    {
        MyAppUserId = userId,
        PlaylistId = playlist.Id,
        IsOwner = true,
    };
    _db.UserPlaylist.Add(userPlaylist);
    await _db.SaveChangesAsync(cancellationToken);

    if (request.CoverImage != null)
    {
        var fileName = Guid.NewGuid().ToString() + Path.GetExtension(request.CoverImage.FileName);
        var filePath = Path.Combine("wwwroot", "Images", "Playlists", fileName);

        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);

        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await request.CoverImage.CopyToAsync(stream, cancellationToken);
        }

        playlist.CoverPath = $"/Images/Playlists/{fileName}";
        _db.Playlists.Update(playlist);
        await _db.SaveChangesAsync(cancellationToken);
    }

    if (request.TrackIds != null && request.TrackIds.Any())
    {
        foreach (var trackId in request.TrackIds)
        {
            var trackPlaylist = new PlaylistTracks
            {
                PlaylistId = playlist.Id,
                TrackId = trackId
            };

            _db.PlaylistTracks.Add(trackPlaylist);
        }

        await _db.SaveChangesAsync(cancellationToken);

        playlist.NumOfTracks = request.TrackIds.Count;
        _db.Playlists.Update(playlist);
        await _db.SaveChangesAsync(cancellationToken);
    }

    return new PlaylistAddResponse
    {
        ID = playlist.Id,
        Title = playlist.Title,
        NumOfTracks = playlist.NumOfTracks,
        IsPublic = playlist.IsPublic,
        CoverPath = playlist.CoverPath,
        UserId = userId
    };
}

    }

    public class PlaylistAddRequest
    {
        public required string Title { get; set; }
        public bool IsPublic { get; set; }
        public IFormFile? CoverImage { get; set; }
        public List<int> TrackIds { get; set; } = new();
        public int? UserId { get; set; }
    }

    public class PlaylistAddResponse
    {
        public required int ID { get; set; }
        public required string Title { get; set; }
        public int NumOfTracks { get; set; }
        public bool IsPublic { get; set; }
        public string CoverPath { get; set; } = string.Empty;
        public int UserId { get; set; }
    }
}
