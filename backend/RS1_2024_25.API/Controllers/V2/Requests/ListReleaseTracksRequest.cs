using System.ComponentModel.DataAnnotations;

namespace RS1_2024_25.API.Controllers.V2.Requests;

public sealed class ListReleaseTracksRequest
{
    [Range(1, int.MaxValue)]
    public int PageNumber { get; set; } = 1;

    [Range(1, 500)]
    public int PageSize { get; set; } = 20;

    public string? Title { get; set; }
}
