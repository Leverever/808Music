using Microsoft.AspNetCore.Mvc;
using RS1_2024_25.API.Data;
using RS1_2024_25.API.Data.Models;
using RS1_2024_25.API.Services.Interfaces;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace RS1_2024_25.API.Endpoints.PlaylistEndpoints
{
    public class PlaylistUpdateEndpoint : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly IMyFileHandler _fh;
        private readonly IConfiguration _cfg;

        public PlaylistUpdateEndpoint(ApplicationDbContext db, IMyFileHandler fh, IConfiguration cfg)
        {
            _db = db;
            _cfg = cfg; 
            _fh = fh;
        }

        [HttpPut]
        [Route("api/playlists-update/{id}")]
        public async Task<IActionResult> HandleAsync(int id, [FromForm] PlaylistUpdateRequest request, CancellationToken cancellationToken = default)
        {
            Console.WriteLine($"Received PUT request for playlist ID: {id}");
            var playlist = await _db.Playlists.FindAsync(id);
            if (playlist == null)
            {
                return NotFound($"Playlist with ID {id} not found.");
            }

            if (!string.IsNullOrEmpty(request.Title))
            {
                playlist.Title = request.Title;
            }

            playlist.IsPublic = request.IsPublic;

            if (request.CoverImage != null)
            {
                if (!string.IsNullOrEmpty(playlist.CoverPath))
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

                var fileName = Guid.NewGuid().ToString() + Path.GetExtension(request.CoverImage.FileName);
                var filePath = Path.Combine("wwwroot", "Images", "Playlists", fileName);

                Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await request.CoverImage.CopyToAsync(stream, cancellationToken);
                }

                playlist.CoverPath = $"/Images/Playlists/{fileName}";
            }

            if (request.TrackIds != null && request.TrackIds.Any())
            {
                var existingTracks = _db.PlaylistTracks.Where(pt => pt.PlaylistId == playlist.Id).ToList();
                _db.PlaylistTracks.RemoveRange(existingTracks);

                foreach (var trackId in request.TrackIds)
                {
                    var trackPlaylist = new PlaylistTracks
                    {
                        PlaylistId = playlist.Id,
                        TrackId = trackId
                    };

                    _db.PlaylistTracks.Add(trackPlaylist);
                }

                playlist.NumOfTracks = request.TrackIds.Count;
            }

            _db.Playlists.Update(playlist);
            await _db.SaveChangesAsync(cancellationToken);

            return Ok(new PlaylistUpdateResponse
            {
                ID = playlist.Id,
                Title = playlist.Title,
                NumOfTracks = playlist.NumOfTracks,
                IsPublic = playlist.IsPublic,
                CoverPath = playlist.CoverPath
            });
        }
    }

    public class PlaylistUpdateRequest
    {
        public string? Title { get; set; }
        public bool IsPublic { get; set; }
        public IFormFile? CoverImage { get; set; }
        public List<int> TrackIds { get; set; } = new();
    }

    public class PlaylistUpdateResponse
    {
        public int ID { get; set; }
        public string Title { get; set; }
        public int NumOfTracks { get; set; }
        public bool IsPublic { get; set; }
        public string CoverPath { get; set; } = string.Empty;
    }
}
