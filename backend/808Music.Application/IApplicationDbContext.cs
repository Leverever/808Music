using _808Music.Domain.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using System;
using System.Collections.Generic;
using System.Text;

namespace _808Music.Application
{
    public interface IApplicationDbContext
    {
        DbSet<Track> Tracks { get; }
        DbSet<_808Music.Domain.Artists.Artist> Artists { get; }
        DbSet<Album> Albums { get; }
        DbSet<AlbumTrack> AlbumTracks { get; }
        DbSet<ArtistTrack> ArtistTracks { get; }
        DbSet<TrackGenre> TrackGenres { get; }
        DbSet<TrackAudioAnalysis> TrackAudioAnalyses { get; }
        DbSet<TrackAudioTag> TrackAudioTags { get; }
        DbSet<TrackMasterMigration> TrackMasterMigrations { get; }
        DbSet<UserTrackInteraction> UserTrackInteractions { get; }
        DbSet<UserMusicProfileCache> UserMusicProfileCaches { get; }
        DbSet<PersonalizedPlaylistTheme> PersonalizedPlaylistThemes { get; }
        DbSet<PersonalizedPlaylistThemeLabel> PersonalizedPlaylistThemeLabels { get; }
        DbSet<GeneratedPersonalizedPlaylist> GeneratedPersonalizedPlaylists { get; }
        DbSet<GeneratedPersonalizedPlaylistTrack> GeneratedPersonalizedPlaylistTracks { get; }
        DbSet<AudioClusterRun> AudioClusterRuns { get; }
        DbSet<AudioCluster> AudioClusters { get; }
        DbSet<TrackClusterAssignment> TrackClusterAssignments { get; }
        DbSet<TrackStemSet> TrackStemSets { get; }
        DbSet<TrackStem> TrackStems { get; }
        DbSet<TrackStream> TrackStreams { get; }

        DatabaseFacade Database { get; }

        Task<int> SaveChangesAsync(CancellationToken cancellationToken);
    }
}
