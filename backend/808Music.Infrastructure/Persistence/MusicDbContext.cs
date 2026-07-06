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
    public DbSet<TrackStemSet> TrackStemSets => Set<TrackStemSet>();
    public DbSet<TrackStem> TrackStems => Set<TrackStem>();

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
