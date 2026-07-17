using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RS1_2024_25.API.Data;
using Microsoft.Extensions.Configuration;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using RS1_2024_25.API.Services.Interfaces;
using RS1_2024_25.API.Data.Models;

namespace RS1_2024_25.API.Endpoints.PlaylistEndpoints
{
    [ApiController]
    [Route("api/playlists")]
    public class PlaylistController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly IConfiguration _cfg;
        private readonly IMyFileHandler _fh;

        public PlaylistController(ApplicationDbContext db, IConfiguration cfg, IMyFileHandler fh)
        {
            _db = db;
            _cfg = cfg;
            _fh = fh;
        }

        [HttpGet]
        public async Task<IActionResult> GetAllPlaylistsAsync(
            [FromQuery] string searchString = "",
            [FromQuery] int returnAmount = 100,
            [FromQuery] bool publicOnly = false,
            CancellationToken cancellationToken = default)
        {
            var query = _db.Playlists.AsNoTracking().AsQueryable();

            if (publicOnly)
            {
                query = query.Where(playlist => playlist.IsPublic);
            }

            if (!string.IsNullOrWhiteSpace(searchString))
            {
                var normalizedSearch = searchString.Trim().ToLower();
                query = query.Where(playlist =>
                    playlist.Title.ToLower().Contains(normalizedSearch)
                    || playlist.UserPlaylists.Any(userPlaylist =>
                        userPlaylist.IsOwner
                        && userPlaylist.User.Username.ToLower().Contains(normalizedSearch)));
            }

            var resultLimit = Math.Clamp(returnAmount, 1, 100);
            var playlists = await query
                .OrderByDescending(playlist => playlist.NumOfTracks)
                .ThenBy(playlist => playlist.Title)
                .Take(resultLimit)
                .Select(p => new
                {
                    p.Id,
                    p.Title,
                    p.NumOfTracks,
                    p.IsPublic,
                    p.CoverPath,
                    Username = p.UserPlaylists
                        .Where(userPlaylist => userPlaylist.IsOwner)
                        .Select(userPlaylist => userPlaylist.User.Username)
                        .FirstOrDefault() ?? "808 User",
                    OwnerUsername = p.UserPlaylists
                        .Where(userPlaylist => userPlaylist.IsOwner)
                        .Select(userPlaylist => userPlaylist.User.Username)
                        .FirstOrDefault() ?? "808 User",
                    UserId = p.UserPlaylists
                        .Where(userPlaylist => userPlaylist.IsOwner)
                        .Select(userPlaylist => userPlaylist.MyAppUserId)
                        .FirstOrDefault(),
                    IsLikedSongs = p.isLikePlaylist,
                    p.IsCollaborative
                })
                .ToListAsync(cancellationToken);

            return Ok(playlists);
        }
        [HttpGet("{id}")]
        public async Task<IActionResult> GetPlaylistByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            var playlist = await _db.Playlists
           .Where(p => p.Id == id)
           .Select(p => new
           {
               p.Id,
               p.Title,
               p.NumOfTracks,
               p.IsPublic,
               p.CoverPath,
               p.IsCollaborative,
               p.isLikePlaylist,
               Users = p.UserPlaylists.Select(up => new
               {
                   UserId = up.MyAppUserId,
                   Username = up.User.Username,
                   ProfilePicture = up.User.pfpCoverPath, 
                   IsOwner = up.IsOwner         
               })
               .OrderByDescending(up => up.IsOwner)
               .ToList()
           })
           .FirstOrDefaultAsync(cancellationToken);


            if (playlist == null)
            {
                return NotFound($"Playlist with ID {id} not found.");
            }

            return Ok(playlist);
        }

        [HttpDelete]
        [Route("playlists-delete/{id}")]
        public async Task<IActionResult> DeletePlaylistAsync(int id, CancellationToken cancellationToken = default)
        {
            var playlist = await _db.Playlists.FindAsync(id);

            if (playlist == null)
                return NotFound($"Playlist with ID {id} not found.");

            var userPlaylists = _db.UserPlaylist.Where(up => up.PlaylistId == id);
            _db.UserPlaylist.RemoveRange(userPlaylists);

            var relatedTracks = _db.PlaylistTracks.Where(pt => pt.PlaylistId == id);
            _db.PlaylistTracks.RemoveRange(relatedTracks);

            if (!string.IsNullOrEmpty(playlist.CoverPath) && playlist.CoverPath != "/Images/playlist_placeholder.png")
            {
                try
                {
                    _fh.DeleteFile(_cfg["StaticFilePaths:CoverImages"] + playlist.CoverPath);
                }
                catch (Exception ex)
                {
                    return StatusCode(500, $"Error deleting cover image: {ex.Message}");
                }
            }

            _db.Playlists.Remove(playlist);
            await _db.SaveChangesAsync(cancellationToken);

            return Ok();
        }

        [HttpDelete("{playlistId}/tracks/{trackId}")]
        public async Task<IActionResult> RemoveTrackFromPlaylistAsync(int playlistId, int trackId, CancellationToken cancellationToken = default)
        {
            var playlistTrack = await _db.PlaylistTracks
                .FirstOrDefaultAsync(pt => pt.PlaylistId == playlistId && pt.TrackId == trackId, cancellationToken);
            var playlist = await _db.Playlists.FindAsync(playlistId);
            if (playlistTrack == null)
            {
                return NotFound($"Track with ID {trackId} not found in playlist with ID {playlistId}.");
            }

            _db.PlaylistTracks.Remove(playlistTrack);
            
            playlist.NumOfTracks--;
            _db.Playlists.Update(playlist);
            try
            {
                await _db.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error removing track from playlist: {ex.Message}");

            }

            return NoContent();
        }


    }
}
