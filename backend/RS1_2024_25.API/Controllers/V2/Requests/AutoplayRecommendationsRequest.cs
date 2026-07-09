namespace RS1_2024_25.API.Controllers.V2.Requests;

public sealed class AutoplayRecommendationsRequest
{
    public List<int> SeedTrackIds { get; set; } = [];
    public List<int> ExcludedTrackIds { get; set; } = [];
    public int? Limit { get; set; }
}
