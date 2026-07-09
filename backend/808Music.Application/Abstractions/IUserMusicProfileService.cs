namespace _808Music.Application.Abstractions;

public interface IUserMusicProfileService
{
    Task<UserMusicProfile> GetOrRefreshDailyProfileAsync(
        int userId,
        DateOnly profileDate,
        CancellationToken cancellationToken = default);

    Task<UserMusicProfileRefreshResult> RefreshActiveUserProfilesAsync(
        DateOnly profileDate,
        CancellationToken cancellationToken = default);
}

public sealed record UserMusicProfile(
    Guid Id,
    int UserId,
    DateOnly ProfileDate,
    DateTime GeneratedAt,
    int SourceInteractionCount,
    int SourceWindowDays,
    string EmbeddingJson,
    string TagAffinitiesJson,
    string ClusterAffinitiesJson,
    string RecentTrackIdsJson,
    string FavoriteArtistIdsJson,
    string FavoriteAlbumIdsJson);

public sealed record UserMusicProfileRefreshResult(
    DateOnly ProfileDate,
    int RefreshedUserCount);
