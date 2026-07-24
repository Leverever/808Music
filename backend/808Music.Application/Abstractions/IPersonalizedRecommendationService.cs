namespace _808Music.Application.Abstractions;

public interface IPersonalizedRecommendationService
{
    Task<IReadOnlyList<PersonalizedRecommendation>> GetRecommendationsAsync(
        PersonalizedRecommendationRequest request,
        CancellationToken cancellationToken = default);
}

public enum PersonalizedRecommendationIntent
{
    General = 1,
    SongRadio = 2,
    Autoplay = 3,
    DailyThematicPlaylist = 4
}

public sealed record PersonalizedRecommendationRequest(
    int UserId,
    PersonalizedRecommendationIntent Intent,
    IReadOnlyCollection<int> SeedTrackIds,
    string? ThemeKey,
    int Limit,
    IReadOnlyCollection<int> ExcludedTrackIds);

public sealed record PersonalizedRecommendation(
    int TrackId,
    double Score,
    string Reason,
    IReadOnlyList<string> MatchedTags,
    string? ClusterKey,
    IReadOnlyDictionary<string, double> SourceSignals);
