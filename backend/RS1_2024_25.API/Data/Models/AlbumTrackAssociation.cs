using System.ComponentModel.DataAnnotations;

namespace RS1_2024_25.API.Data.Models;

public sealed class AlbumTrackAssociation
{
    [Key]
    public int Id { get; set; }

    public int AlbumId { get; set; }

    public int TrackId { get; set; }

    public int DiscNumber { get; set; }

    public int TrackNumber { get; set; }

    public string? TitleOverride { get; set; }

    public bool IsPrimaryRelease { get; set; }
}
