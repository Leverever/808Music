using System.ComponentModel.DataAnnotations;

namespace _808Music.Domain.Catalog;

public sealed class TrackStream
{
    [Key]
    public int Id { get; set; }

    public int TrackId { get; set; }
    public DateTime StreamedAt { get; set; }
    public int? UserId { get; set; }
}
