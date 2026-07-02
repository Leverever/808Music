using System.ComponentModel.DataAnnotations;

namespace RS1_2024_25.API.Controllers.V2.Requests;

public sealed class UpdateTrackMetadataRequest
{
    [Required]
    public string Title { get; set; } = string.Empty;

    public bool IsExplicit { get; set; }
}
