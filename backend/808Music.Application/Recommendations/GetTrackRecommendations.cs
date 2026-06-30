using _808Music.Application.Abstractions;

namespace _808Music.Application.Recommendations;

public sealed record GetTrackRecommendationsQuery(Guid TrackId, string? RequestedByUserId);

public sealed record GetTrackRecommendationsResult(
    Guid TrackId,
    IReadOnlyList<TrackRecommendation> Recommendations);

public interface IGetTrackRecommendationsHandler
{
    Task<GetTrackRecommendationsResult> Handle(
        GetTrackRecommendationsQuery query,
        CancellationToken cancellationToken = default);
}

public sealed class GetTrackRecommendationsHandler : IGetTrackRecommendationsHandler
{
    private readonly IRecommendationService _recommendationService;

    public GetTrackRecommendationsHandler(IRecommendationService recommendationService)
    {
        _recommendationService = recommendationService;
    }

    public async Task<GetTrackRecommendationsResult> Handle(
        GetTrackRecommendationsQuery query,
        CancellationToken cancellationToken = default)
    {
        var recommendations = await _recommendationService.GetForTrackAsync(
            query.TrackId,
            query.RequestedByUserId,
            cancellationToken);

        return new GetTrackRecommendationsResult(query.TrackId, recommendations);
    }
}
