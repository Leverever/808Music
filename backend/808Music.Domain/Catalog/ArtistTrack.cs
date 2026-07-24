using _808Music.Domain.Artists;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace _808Music.Domain.Catalog;

public class ArtistTrack
{
    [Key]
    public int Id { get; set; }

    public bool IsLead { get; set; }
    public bool ShowOnProfile { get; set; }

    public int ArtistId { get; set; }

    [ForeignKey(nameof(ArtistId))]
    public Artist? Artist { get; set; }

    public int TrackId { get; set; }

    [ForeignKey(nameof(TrackId))]
    public Track? Track { get; set; }
}
