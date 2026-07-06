using _808Music.Domain.Catalog;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace _808Music.Application
{
    public interface IApplicationDbContext
    {
        DbSet<Track> Tracks { get; }
        DbSet<Album> Albums { get; }
        DbSet<AlbumTrack> AlbumTracks { get; }
        DbSet<ArtistTrack> ArtistTracks { get; }
        DbSet<TrackGenre> TrackGenres { get; }
        DbSet<TrackAudioAnalysis> TrackAudioAnalyses { get; }
        DbSet<TrackAudioTag> TrackAudioTags { get; }
        DbSet<TrackStemSet> TrackStemSets { get; }
        DbSet<TrackStem> TrackStems { get; }

        Task<int> SaveChangesAsync(CancellationToken cancellationToken);
    }
}
