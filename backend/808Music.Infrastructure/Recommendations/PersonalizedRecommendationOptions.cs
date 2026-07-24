namespace _808Music.Infrastructure.Recommendations;

public sealed class PersonalizedRecommendationOptions
{
    public const string SectionName = "PersonalizedRecommendations";

    public int MaxCandidateTracks { get; set; } = 2_000;
    public int MaxMatchedTags { get; set; } = 5;
    public int SameArtistLimit { get; set; } = 4;
    public int SameAlbumLimit { get; set; } = 3;
}
