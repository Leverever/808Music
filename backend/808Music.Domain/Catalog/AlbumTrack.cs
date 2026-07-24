using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace _808Music.Domain.Catalog
{
    public class AlbumTrack
    {
        [Key]
        public int Id { get; set; }

        public int AlbumId { get; set; }

        [ForeignKey(nameof(AlbumId))]
        public Album? Album { get; set; } = null!;
        public int TrackId { get; set; }

        [ForeignKey(nameof(TrackId))]
        public Track? Track { get; set; } = null!;

        public int DiscNumber { get; set; } = 1;
        public int TrackNumber { get; set; }

        public string? TitleOverride { get; set; }
        public bool IsPrimaryRelease { get; set; }
    }
}
