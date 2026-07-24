using _808Music.Application;
using _808Music.Domain.Artists;
using _808Music.Domain.Catalog;
using _808Music.Domain.Static;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace _808Music.Infrastructure.Persistence;

public sealed class MusicDbContext(DbContextOptions<MusicDbContext> options)
    : DbContext(options), IApplicationDbContext
{
    public DbSet<Track> Tracks => Set<Track>();
    public DbSet<Album> Albums => Set<Album>();
    public DbSet<AlbumTrack> AlbumTracks => Set<AlbumTrack>();
    public DbSet<ArtistTrack> ArtistTracks => Set<ArtistTrack>();
    public DbSet<TrackGenre> TrackGenres => Set<TrackGenre>();
    public DbSet<TrackAudioAnalysis> TrackAudioAnalyses => Set<TrackAudioAnalysis>();
    public DbSet<TrackAudioTag> TrackAudioTags => Set<TrackAudioTag>();
    public DbSet<TrackMasterMigration> TrackMasterMigrations => Set<TrackMasterMigration>();
    public DbSet<UserTrackInteraction> UserTrackInteractions => Set<UserTrackInteraction>();
    public DbSet<UserMusicProfileCache> UserMusicProfileCaches => Set<UserMusicProfileCache>();
    public DbSet<PersonalizedPlaylistTheme> PersonalizedPlaylistThemes => Set<PersonalizedPlaylistTheme>();
    public DbSet<PersonalizedPlaylistThemeLabel> PersonalizedPlaylistThemeLabels => Set<PersonalizedPlaylistThemeLabel>();
    public DbSet<GeneratedPersonalizedPlaylist> GeneratedPersonalizedPlaylists => Set<GeneratedPersonalizedPlaylist>();
    public DbSet<GeneratedPersonalizedPlaylistTrack> GeneratedPersonalizedPlaylistTracks => Set<GeneratedPersonalizedPlaylistTrack>();
    public DbSet<AudioClusterRun> AudioClusterRuns => Set<AudioClusterRun>();
    public DbSet<AudioCluster> AudioClusters => Set<AudioCluster>();
    public DbSet<TrackClusterAssignment> TrackClusterAssignments => Set<TrackClusterAssignment>();
    public DbSet<TrackStemSet> TrackStemSets => Set<TrackStemSet>();
    public DbSet<TrackStem> TrackStems => Set<TrackStem>();
    public DbSet<TrackStream> TrackStreams => Set<TrackStream>();

    public DbSet<Artist> Artists => Set<Artist>();
    public DbSet<AlbumType> AlbumTypes => Set<AlbumType>();
    public DbSet<Genre> Genres => Set<Genre>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Track>(entity =>
        {
            entity.ToTable("Tracks");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Title)
                .HasMaxLength(200)
                .IsRequired();

            entity.Property(x => x.TrackPath)
                .HasMaxLength(500);

            entity.HasOne(x => x.Album)
                .WithMany()
                .HasForeignKey(x => x.AlbumId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<TrackMasterMigration>(entity =>
        {
            entity.ToTable("TrackMasterMigrations");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.LegacyRelativePath)
                .HasMaxLength(500)
                .IsRequired();

            entity.Property(x => x.TargetObjectKey)
                .HasMaxLength(500);

            entity.Property(x => x.SourceChecksumSha256)
                .HasMaxLength(64);

            entity.Property(x => x.ContentType)
                .HasMaxLength(100);

            entity.Property(x => x.LastError)
                .HasMaxLength(2_000);

            entity.HasOne<Track>()
                .WithMany()
                .HasForeignKey(x => x.TrackId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(x => x.TrackId)
                .IsUnique();

            entity.HasIndex(x => new { x.Status, x.UpdatedAt });

            entity.HasIndex(x => x.TargetObjectKey)
                .IsUnique()
                .HasFilter("[TargetObjectKey] IS NOT NULL");
        });

        modelBuilder.Entity<ArtistTrack>(entity =>
        {
            entity.ToTable("ArtistsTracks", table => table.ExcludeFromMigrations());
            entity.HasKey(x => x.Id);

            entity.HasOne(x => x.Artist)
                .WithMany()
                .HasForeignKey(x => x.ArtistId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(x => x.Track)
                .WithMany()
                .HasForeignKey(x => x.TrackId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(x => x.ArtistId);
            entity.HasIndex(x => x.TrackId);
        });

        modelBuilder.Entity<Album>(entity =>
        {
            entity.ToTable("Albums");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Title)
                .HasMaxLength(200)
                .IsRequired();

            entity.Property(x => x.Distributor)
                .HasMaxLength(200);

            entity.Property(x => x.CoverPath)
                .HasMaxLength(500);

            entity.HasOne(x => x.Artist)
                .WithMany()
                .HasForeignKey(x => x.ArtistId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(x => x.AlbumType)
                .WithMany()
                .HasForeignKey(x => x.AlbumTypeId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<AlbumTrack>(entity =>
        {
            entity.ToTable("AlbumTracks");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.TitleOverride)
                .HasMaxLength(200);

            entity.HasOne(x => x.Album)
                .WithMany()
                .HasForeignKey(x => x.AlbumId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.Track)
                .WithMany()
                .HasForeignKey(x => x.TrackId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasIndex(x => new { x.AlbumId, x.DiscNumber, x.TrackNumber })
                .IsUnique();

            entity.HasIndex(x => new { x.AlbumId, x.TrackId })
                .IsUnique();

            entity.HasIndex(x => x.TrackId)
                .IsUnique()
                .HasFilter("[IsPrimaryRelease] = 1");
        });

        modelBuilder.Entity<TrackGenre>(entity =>
        {
            entity.ToTable("TrackGenres");
            entity.HasKey(x => x.Id);

            entity.HasOne(x => x.Track)
                .WithMany()
                .HasForeignKey(x => x.TrackId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.Genre)
                .WithMany()
                .HasForeignKey(x => x.GenreId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<TrackStemSet>(entity =>
        {
            entity.ToTable("TrackStemSets");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.ModelName)
                .HasMaxLength(100);

            entity.Property(x => x.ModelVersion)
                .HasMaxLength(100);

            entity.Property(x => x.ProviderName)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(x => x.StemProfile)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(x => x.ErrorMessage)
                .HasMaxLength(1_000);

            entity.HasOne<Track>()
                .WithMany(x => x.StemSets)
                .HasForeignKey(x => x.TrackId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(x => x.Stems)
                .WithOne()
                .HasForeignKey(x => x.StemSetId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.Navigation(x => x.Stems)
                .UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<TrackAudioAnalysis>(entity =>
        {
            entity.ToTable("TrackAudioAnalyses");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.ProviderName)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(x => x.ModelName)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(x => x.ModelVersion)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(x => x.EmbeddingModel)
                .HasMaxLength(100);

            entity.Property(x => x.EmbeddingJson)
                .HasColumnType("nvarchar(max)");

            entity.Property(x => x.ErrorMessage)
                .HasMaxLength(1_000);

            entity.HasOne<Track>()
                .WithMany(x => x.AudioAnalyses)
                .HasForeignKey(x => x.TrackId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(x => x.Tags)
                .WithOne()
                .HasForeignKey(x => x.TrackAudioAnalysisId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(x => x.TrackId);
            entity.HasIndex(x => new { x.TrackId, x.IsActive });
            entity.Navigation(x => x.Tags)
                .UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<TrackAudioTag>(entity =>
        {
            entity.ToTable("TrackAudioTags");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Namespace)
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(x => x.Label)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(x => x.ModelName)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(x => x.Score)
                .HasColumnType("decimal(9,6)");

            entity.HasIndex(x => x.TrackAudioAnalysisId);
            entity.HasIndex(x => new { x.Namespace, x.Label });
        });

        modelBuilder.Entity<UserTrackInteraction>(entity =>
        {
            entity.ToTable("UserTrackInteractions");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.ContextType)
                .HasMaxLength(50);

            entity.Property(x => x.ClientEventId)
                .HasMaxLength(100);

            entity.Property(x => x.CompletionRatio)
                .HasColumnType("decimal(9,6)");

            entity.HasOne<Track>()
                .WithMany()
                .HasForeignKey(x => x.TrackId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(x => new { x.UserId, x.OccurredAt });
            entity.HasIndex(x => new { x.UserId, x.TrackId });
            entity.HasIndex(x => new { x.TrackId, x.InteractionType });
            entity.HasIndex(x => new { x.UserId, x.ClientEventId })
                .IsUnique()
                .HasFilter("[ClientEventId] IS NOT NULL");
        });

        modelBuilder.Entity<UserMusicProfileCache>(entity =>
        {
            entity.ToTable("UserMusicProfileCaches");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.ProfileDate)
                .HasColumnType("date");

            entity.Property(x => x.EmbeddingJson)
                .HasColumnType("nvarchar(max)")
                .IsRequired();

            entity.Property(x => x.TagAffinitiesJson)
                .HasColumnType("nvarchar(max)")
                .IsRequired();

            entity.Property(x => x.ClusterAffinitiesJson)
                .HasColumnType("nvarchar(max)")
                .IsRequired();

            entity.Property(x => x.RecentTrackIdsJson)
                .HasColumnType("nvarchar(max)")
                .IsRequired();

            entity.Property(x => x.FavoriteArtistIdsJson)
                .HasColumnType("nvarchar(max)")
                .IsRequired();

            entity.Property(x => x.FavoriteAlbumIdsJson)
                .HasColumnType("nvarchar(max)")
                .IsRequired();

            entity.HasIndex(x => new { x.UserId, x.ProfileDate })
                .IsUnique();

            entity.HasIndex(x => x.GeneratedAt);
        });

        modelBuilder.Entity<PersonalizedPlaylistTheme>(entity =>
        {
            entity.ToTable("PersonalizedPlaylistThemes");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.ThemeKey)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(x => x.Name)
                .HasMaxLength(200)
                .IsRequired();

            entity.Property(x => x.Description)
                .HasMaxLength(500)
                .IsRequired();

            entity.HasMany(x => x.Labels)
                .WithOne()
                .HasForeignKey(x => x.ThemeId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(x => x.ThemeKey)
                .IsUnique();

            entity.HasIndex(x => new { x.IsActive, x.SortOrder });

            entity.Navigation(x => x.Labels)
                .UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<PersonalizedPlaylistThemeLabel>(entity =>
        {
            entity.ToTable("PersonalizedPlaylistThemeLabels");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Label)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(x => x.TagNamespace)
                .HasMaxLength(50);

            entity.Property(x => x.Weight)
                .HasColumnType("decimal(9,4)");

            entity.HasIndex(x => new
                {
                    x.ThemeId,
                    x.Polarity,
                    x.Source,
                    x.TagNamespace,
                    x.Label
                })
                .IsUnique();

            entity.HasIndex(x => new
                {
                    x.ThemeId,
                    x.Polarity,
                    x.Source,
                    x.Label
                })
                .IsUnique()
                .HasFilter("[TagNamespace] IS NULL");
        });

        modelBuilder.Entity<GeneratedPersonalizedPlaylist>(entity =>
        {
            entity.ToTable("GeneratedPersonalizedPlaylists");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.ThemeKey)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(x => x.Name)
                .HasMaxLength(200)
                .IsRequired();

            entity.Property(x => x.Description)
                .HasMaxLength(500)
                .IsRequired();

            entity.Property(x => x.PlaylistDate)
                .HasColumnType("date");

            entity.HasOne<PersonalizedPlaylistTheme>()
                .WithMany()
                .HasForeignKey(x => x.ThemeId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasMany(x => x.Tracks)
                .WithOne()
                .HasForeignKey(x => x.PlaylistId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(x => new { x.UserId, x.ThemeKey, x.PlaylistDate })
                .IsUnique();

            entity.HasIndex(x => new { x.UserId, x.PlaylistDate });

            entity.HasIndex(x => x.ThemeId);

            entity.Navigation(x => x.Tracks)
                .UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<GeneratedPersonalizedPlaylistTrack>(entity =>
        {
            entity.ToTable("GeneratedPersonalizedPlaylistTracks");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Score)
                .HasColumnType("decimal(9,6)");

            entity.Property(x => x.Reason)
                .HasMaxLength(500)
                .IsRequired();

            entity.HasOne<Track>()
                .WithMany()
                .HasForeignKey(x => x.TrackId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasIndex(x => new { x.PlaylistId, x.Position })
                .IsUnique();

            entity.HasIndex(x => new { x.PlaylistId, x.TrackId })
                .IsUnique();

            entity.HasIndex(x => x.TrackId);
        });

        modelBuilder.Entity<AudioClusterRun>(entity =>
        {
            entity.ToTable("AudioClusterRuns");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.AlgorithmName)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(x => x.EmbeddingSource)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(x => x.ParametersJson)
                .HasColumnType("nvarchar(max)")
                .IsRequired();

            entity.Property(x => x.ErrorMessage)
                .HasMaxLength(1_000);

            entity.HasMany(x => x.Clusters)
                .WithOne()
                .HasForeignKey(x => x.ClusterRunId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(x => x.IsActive);
            entity.HasIndex(x => new { x.AlgorithmName, x.EmbeddingSource, x.IsActive });
            entity.Navigation(x => x.Clusters)
                .UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<AudioCluster>(entity =>
        {
            entity.ToTable("AudioClusters");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.ClusterKey)
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(x => x.Name)
                .HasMaxLength(200)
                .IsRequired();

            entity.Property(x => x.TopTagsJson)
                .HasColumnType("nvarchar(max)")
                .IsRequired();

            entity.HasMany(x => x.Assignments)
                .WithOne()
                .HasForeignKey(x => x.ClusterId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasIndex(x => x.ClusterRunId);
            entity.HasIndex(x => new { x.ClusterRunId, x.ClusterKey })
                .IsUnique();
            entity.Navigation(x => x.Assignments)
                .UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<TrackClusterAssignment>(entity =>
        {
            entity.ToTable("TrackClusterAssignments");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.ClusterKey)
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(x => x.DistanceToCenter)
                .HasColumnType("decimal(18,9)");

            entity.Property(x => x.MembershipScore)
                .HasColumnType("decimal(9,6)");

            entity.HasOne<Track>()
                .WithMany()
                .HasForeignKey(x => x.TrackId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne<AudioClusterRun>()
                .WithMany()
                .HasForeignKey(x => x.ClusterRunId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(x => x.ClusterRunId);
            entity.HasIndex(x => x.ClusterId);
            entity.HasIndex(x => x.TrackId);
            entity.HasIndex(x => new { x.ClusterRunId, x.TrackId })
                .IsUnique();
        });

        modelBuilder.Entity<TrackStem>(entity =>
        {
            entity.ToTable("TrackStems");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.ObjectKey)
                .HasMaxLength(500)
                .IsRequired();

            entity.Property(x => x.ContentType)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(x => x.Codec)
                .HasMaxLength(50);

            entity.Property(x => x.ChecksumSha256)
                .HasMaxLength(64);
        });

        modelBuilder.Entity<TrackStream>(entity =>
        {
            entity.ToTable("TrackStream", table => table.ExcludeFromMigrations());
            entity.HasKey(x => x.Id);

            entity.HasOne<Track>()
                .WithMany()
                .HasForeignKey(x => x.TrackId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasIndex(x => new { x.TrackId, x.StreamedAt });
        });

        modelBuilder.Entity<Artist>(entity =>
        {
            entity.ToTable("Artists");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Name)
                .HasMaxLength(200)
                .IsRequired();
        });

        modelBuilder.Entity<AlbumType>(entity =>
        {
            entity.ToTable("AlbumTypes");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Type)
                .HasMaxLength(100);
        });

        modelBuilder.Entity<Genre>(entity =>
        {
            entity.ToTable("Genres");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Name)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(x => x.Tag)
                .HasMaxLength(100)
                .IsRequired();
        });
    }
}
