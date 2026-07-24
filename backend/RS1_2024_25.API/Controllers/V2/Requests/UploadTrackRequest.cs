using System.ComponentModel.DataAnnotations;

namespace RS1_2024_25.API.Controllers.V2.Requests;

public sealed class UploadTrackRequest
{
    [Required]
    public int ArtistId { get; set; }

    [Required]
    [StringLength(200, MinimumLength = 3)]
    public string Title { get; set; } = string.Empty;

    public bool IsExplicit { get; set; }

    [Required]
    public IFormFile? MasterFile { get; set; }
}
