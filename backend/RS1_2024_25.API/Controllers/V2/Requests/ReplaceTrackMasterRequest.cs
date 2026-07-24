using System.ComponentModel.DataAnnotations;

namespace RS1_2024_25.API.Controllers.V2.Requests;

public sealed class ReplaceTrackMasterRequest
{
    [Required]
    public IFormFile? MasterFile { get; set; }
}
