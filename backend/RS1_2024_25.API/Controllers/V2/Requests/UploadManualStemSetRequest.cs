using System.ComponentModel.DataAnnotations;

namespace RS1_2024_25.API.Controllers.V2.Requests;

public sealed class UploadManualStemSetRequest
{
    public string StemProfile { get; set; } = "four-stem";

    [Required]
    public IFormFile? Vocals { get; set; }

    public IFormFile? Drums { get; set; }

    public IFormFile? Bass { get; set; }

    public IFormFile? Other { get; set; }

    public IFormFile? Instrumental { get; set; }
}
