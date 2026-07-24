using System.ComponentModel.DataAnnotations;

namespace RS1_2024_25.API.Controllers.V2.Requests;

public sealed class UpdateReleaseTrackRequest
{
    [Range(1, int.MaxValue)]
    public int DiscNumber { get; set; } = 1;

    [Range(1, int.MaxValue)]
    public int TrackNumber { get; set; }

    [StringLength(200)]
    public string? TitleOverride { get; set; }

    public bool IsPrimaryRelease { get; set; }
}
