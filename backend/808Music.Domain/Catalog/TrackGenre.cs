using _808Music.Domain.Static;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace _808Music.Domain.Catalog
{
    public class TrackGenre
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey(nameof(Track))]
        public int TrackId { get; set; }
        public Track? Track { get; set; }

        [ForeignKey(nameof(Genre))]
        public int GenreId { get; set; }
        public Genre? Genre { get; set; }
    }
}
