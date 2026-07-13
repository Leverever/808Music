using Microsoft.EntityFrameworkCore;
using RS1_2024_25.API.Data.Models;
using RS1_2024_25.API.Data.Models.Auth;
using RS1_2024_25.API.Data.Models.Stripe;
using RS1_2024_25.API.Endpoints.ProductEndpoints;

namespace RS1_2024_25.API.Data
{
    public class ApplicationDbContext(
        DbContextOptions<ApplicationDbContext> options) : DbContext(options)
    {
        public DbSet<City> Cities { get; set; }
        public DbSet<MyAuthenticationToken> MyAuthenticationTokens { get; set; }

        public DbSet<Country> Countries { get; set; }
        public DbSet<MyAppUser> MyAppUsers { get; set; }
        public DbSet<Track> Tracks { get; set; }
        public DbSet<Genre> Genres { get; set; }
        public DbSet<TrackGenre> TrackGenres { get; set; }
        public DbSet<ProductPhoto> ProductPhotos { get; set; }
        public DbSet<Product> Products { get; set; }
        public DbSet<Credits> Credits { get; set; }
        public DbSet<ArtistTrack> ArtistsTracks { get; set; }
        public DbSet<Artist> Artists { get; set; }
        public DbSet<UserArtist> UserArtists { get; set; }
        public DbSet<UserArtistRole> UserArtistRoles { get; set; }
        public DbSet<Album> Albums { get; set; }
        public DbSet<AlbumTrackAssociation> AlbumTrackAssociations { get; set; }
        public DbSet<AlbumType> AlbumTypes { get; set; }
        public DbSet<MyRefreshToken> MyRefreshTokens { get; set; }
        public DbSet<MyResetToken> MyResetTokens { get; set; }
        public DbSet<MyUserArtistInvite> MyUserArtistInvites { get; set; }
        public DbSet<PaymentTransaction> PaymentTransactions { get; set; }
        public DbSet<Playlist> Playlists { get; set; }
        public DbSet<UserPlaylist> UserPlaylist { get; set; }
        public DbSet<PlaylistTracks> PlaylistTracks { get; set; }
        public DbSet<Subscription> Subscription { get; set; }
        public DbSet<SubscriptionDetails> SubscriptionDetails { get; set; }
        public DbSet<UserProductWishlist> UserProductWishlist { get; set; }
        public DbSet<UserShoppingCart> UserShoppingCart { get; set; }
        public DbSet<Order> Order { get; set; }
        public DbSet<OrderDetails> OrderDetails { get; set; }
        public DbSet<UserOrders> UserOrders { get; set; }
        public DbSet<Follow> Follows { get; set; }
        public DbSet<Notification> Notifications { get; set; }
        public DbSet<ReadNotification> ReadNotifications { get; set; }
        public DbSet<MyAppUserPreference> MyAppUserPreferences { get; set; }
        public DbSet<TrackStream> TrackStream { get; set; }

        public DbSet<Events> Events { get; set; }
        public DbSet<UserChat> UserChats { get; set; }
        public DbSet<ChatMessage> ChatMessages { get; set; }
        public DbSet<FollowForUser> FollowForUser { get; set; }

        public DbSet<ArtistAlbumSpotlight> AlbumSpotlights { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<AlbumTrackAssociation>(entity =>
            {
                entity.ToTable("AlbumTracks", table => table.ExcludeFromMigrations());
                entity.Property(x => x.TitleOverride).HasMaxLength(200);
            });

            foreach (var relationship in modelBuilder.Model.GetEntityTypes().SelectMany(e => e.GetForeignKeys()))
            {
                relationship.DeleteBehavior = DeleteBehavior.NoAction;
            }

            // opcija kod nasljeđivanja
            // modelBuilder.Entity<NekaBaznaKlasa>().UseTpcMappingStrategy();
        }
    }
}
