namespace _808Music.Application.Abstractions;

public interface ILegacyPlaylistRecommendationReader
{
    Task<IReadOnlyList<LegacyPlaylistRecommendation>> GetRecommendedPlaylistsAsync(
        LegacyPlaylistRecommendationRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record LegacyPlaylistRecommendationRequest(
    int UserId,
    IReadOnlyCollection<LegacyPlaylistTrackSignal> TrackSignals,
    int Limit);

public sealed record LegacyPlaylistTrackSignal(
    int TrackId,
    double Score,
    string Reason);

public sealed record LegacyPlaylistRecommendation(
    int PlaylistId,
    string Title,
    string CoverPath,
    bool IsPublic,
    bool IsCollaborative,
    int TrackCount,
    int? OwnerUserId,
    string? OwnerUsername,
    double Score,
    string Reason,
    IReadOnlyList<int> MatchedTrackIds);
