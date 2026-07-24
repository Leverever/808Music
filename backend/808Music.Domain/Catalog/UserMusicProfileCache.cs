using System;
using System.ComponentModel.DataAnnotations;

namespace _808Music.Domain.Catalog
{
    public class UserMusicProfileCache
    {
        [Key]
        public Guid Id { get; private set; }
        public int UserId { get; private set; }
        public DateOnly ProfileDate { get; private set; }
        public DateTime GeneratedAt { get; private set; }
        public int SourceInteractionCount { get; private set; }
        public int SourceWindowDays { get; private set; }
        public string EmbeddingJson { get; private set; } = "[]";
        public string TagAffinitiesJson { get; private set; } = "[]";
        public string ClusterAffinitiesJson { get; private set; } = "[]";
        public string RecentTrackIdsJson { get; private set; } = "[]";
        public string FavoriteArtistIdsJson { get; private set; } = "[]";
        public string FavoriteAlbumIdsJson { get; private set; } = "[]";

        private UserMusicProfileCache() { } // EF Core

        public UserMusicProfileCache(
            int userId,
            DateOnly profileDate,
            int sourceInteractionCount,
            int sourceWindowDays,
            string? embeddingJson,
            string? tagAffinitiesJson,
            string? clusterAffinitiesJson,
            string? recentTrackIdsJson,
            string? favoriteArtistIdsJson,
            string? favoriteAlbumIdsJson)
        {
            if (userId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(userId), "User id must be positive.");
            }

            Id = Guid.NewGuid();
            UserId = userId;
            ProfileDate = profileDate;

            Refresh(
                sourceInteractionCount,
                sourceWindowDays,
                embeddingJson,
                tagAffinitiesJson,
                clusterAffinitiesJson,
                recentTrackIdsJson,
                favoriteArtistIdsJson,
                favoriteAlbumIdsJson);
        }

        public void Refresh(
            int sourceInteractionCount,
            int sourceWindowDays,
            string? embeddingJson,
            string? tagAffinitiesJson,
            string? clusterAffinitiesJson,
            string? recentTrackIdsJson,
            string? favoriteArtistIdsJson,
            string? favoriteAlbumIdsJson)
        {
            if (sourceInteractionCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(sourceInteractionCount), "Source interaction count cannot be negative.");
            }

            if (sourceWindowDays <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(sourceWindowDays), "Source window days must be positive.");
            }

            GeneratedAt = DateTime.UtcNow;
            SourceInteractionCount = sourceInteractionCount;
            SourceWindowDays = sourceWindowDays;
            EmbeddingJson = NormalizeJson(embeddingJson);
            TagAffinitiesJson = NormalizeJson(tagAffinitiesJson);
            ClusterAffinitiesJson = NormalizeJson(clusterAffinitiesJson);
            RecentTrackIdsJson = NormalizeJson(recentTrackIdsJson);
            FavoriteArtistIdsJson = NormalizeJson(favoriteArtistIdsJson);
            FavoriteAlbumIdsJson = NormalizeJson(favoriteAlbumIdsJson);
        }

        private static string NormalizeJson(string? value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? "[]"
                : value.Trim();
        }
    }
}
