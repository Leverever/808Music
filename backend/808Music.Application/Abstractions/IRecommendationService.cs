namespace _808Music.Application.Abstractions;

public interface IRecommendationService
{
    Task<IReadOnlyList<TrackRecommendation>> GetForTrackAsync(
        Guid trackId,
        string? requestedByUserId,
        CancellationToken cancellationToken = default);
}

public sealed record TrackRecommendation(
    Guid TrackId,
    string Title,
    string ArtistName,
    double Score,
    string Reason);
