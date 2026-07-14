using System.ComponentModel.DataAnnotations;

namespace RS1_2024_25.API.Controllers.V2.Requests;

public sealed class ReorderReleaseTracksRequest
{
    [Required]
    [MinLength(1)]
    public List<ReleaseTrackPositionRequest> Tracks { get; set; } = [];
}

public sealed class ReleaseTrackPositionRequest
{
    [Range(1, int.MaxValue)]
    public int TrackId { get; set; }

    [Range(1, int.MaxValue)]
    public int DiscNumber { get; set; }

    [Range(1, int.MaxValue)]
    public int TrackNumber { get; set; }
}
