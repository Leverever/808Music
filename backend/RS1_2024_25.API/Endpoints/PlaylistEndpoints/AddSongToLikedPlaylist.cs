using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RS1_2024_25.API.Data;
using RS1_2024_25.API.Data.Models;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace RS1_2024_25.API.Endpoints.PlaylistEndpoints
{
    [ApiController]
    [Route("api/playlists")]
    public class AddTrackToLikedSongsEndpoint : ControllerBase
    {
        private readonly ApplicationDbContext _db;

        public AddTrackToLikedSongsEndpoint(ApplicationDbContext db)
        {
            _db = db;
        }

        [HttpPost("add-to-liked-songs")]
        public async Task<ActionResult> HandleAsync([FromBody] AddTrackToLikedSongsRequest request, CancellationToken cancellationToken = default)
        {
            int userId;
            if (request.UserId.HasValue)
            {
                userId = request.UserId.Value;
            }
            else if (User.Identity?.IsAuthenticated == true)
            {
                userId = int.Parse(User.Claims.FirstOrDefault(c => c.Type == "userId")?.Value!);
            }
            else
            {
                return Unauthorized();
            }

            var user = await _db.MyAppUsers.FindAsync(userId);
            if (user == null)
            {
                return NotFound("User Not Found.");
            }

            var likedSongsPlaylist = await _db.Playlists
                .Where(p => p.isLikePlaylist && p.UserPlaylists.Any(up => up.MyAppUserId == userId))
                .FirstOrDefaultAsync(cancellationToken);

            if (likedSongsPlaylist == null)
            {
                likedSongsPlaylist = new Playlist
                {
                    Title = "Liked songs",
                    IsPublic = false,
                    isLikePlaylist = true,
                    CoverPath = "/Images/liked_songs.png",
                    NumOfTracks = 0
                };

                _db.Playlists.Add(likedSongsPlaylist);
                await _db.SaveChangesAsync();

                var userPlaylist = new UserPlaylist
                {
                    MyAppUserId = user.ID,
                    PlaylistId = likedSongsPlaylist.Id,
                    IsOwner = true

                };

                _db.UserPlaylist.Add(userPlaylist);
                await _db.SaveChangesAsync();
            }

            var track = await _db.Tracks.FindAsync(request.TrackId);
            if (track == null)
            {
                return NotFound("Track not found.");
            }

            var existingTrackInPlaylist = await _db.PlaylistTracks
                .FirstOrDefaultAsync(pt => pt.PlaylistId == likedSongsPlaylist.Id && pt.TrackId == track.Id, cancellationToken);

            if (existingTrackInPlaylist != null)
            {
                _db.PlaylistTracks.Remove(existingTrackInPlaylist);
                likedSongsPlaylist.NumOfTracks--;
                _db.Playlists.Update(likedSongsPlaylist);
                await _db.SaveChangesAsync(cancellationToken);
                return Ok(new { Message = "Track removed." });

            }

            var playlistTrack = new PlaylistTracks
            {
                PlaylistId = likedSongsPlaylist.Id,
                TrackId = track.Id,
                TrackOrder = likedSongsPlaylist.NumOfTracks + 1,
                DateAdded = DateTime.UtcNow,
            };

            _db.PlaylistTracks.Add(playlistTrack);
            likedSongsPlaylist.NumOfTracks++;
            _db.Playlists.Update(likedSongsPlaylist);

            await _db.SaveChangesAsync(cancellationToken);

            return Ok(new { Message = "Track added to 'Liked songs' playlist." });
        }
    }

    public class AddTrackToLikedSongsRequest
    {
        public int TrackId { get; set; }
        public int? UserId { get; set; }
    }
}
