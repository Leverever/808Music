using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace _808Music.Domain.Catalog
{
    public class Track
    {
        [Key]
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public int Length { get; set; }
        public int Streams { get; set; }
        public bool IsExplicit { get; set; }
        public string TrackPath { get; set; } = string.Empty;
        public int AlbumId { get; set; }

        [ForeignKey(nameof(AlbumId))]
        public Album? Album { get; set; }
        public ICollection<TrackStemSet> StemSets { get; set; } = new List<TrackStemSet>();
        //public ICollection<TrackStream> TrackStreams { get; set; } = new List<TrackStream>();
        //public ICollection<PlaylistTracks> PlaylistTracks { get; set; } = new List<PlaylistTracks>();
    }
}
