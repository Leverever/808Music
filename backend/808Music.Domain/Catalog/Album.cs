using _808Music.Domain.Artists;
using _808Music.Domain.Static;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace _808Music.Domain.Catalog
{
    public class Album
    {
        [Key]
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Distributor { get; set; } = string.Empty;
        public DateTime ReleaseDate { get; set; }
        public int AlbumTypeId { get; set; } = 4;

        [ForeignKey(nameof(AlbumTypeId))]
        public AlbumType? AlbumType { get; set; }
        public bool IsActive { get; set; }
        public int NumOfTracks { get; set; }
        public string CoverPath { get; set; } = string.Empty;
        public int ArtistId { get; set; }

        [ForeignKey(nameof(ArtistId))]
        public Artist? Artist { get; set; }
    }
}
