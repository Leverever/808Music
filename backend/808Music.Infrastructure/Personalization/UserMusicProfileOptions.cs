namespace _808Music.Infrastructure.Personalization;

public sealed class UserMusicProfileOptions
{
    public const string SectionName = "UserMusicProfiles";

    public bool RecurringEnabled { get; set; } = true;
    public string RecurringCronExpression { get; set; } = "15 1 * * *";
    public int SourceWindowDays { get; set; } = 90;
    public double RecencyHalfLifeDays { get; set; } = 14;
    public int MaxRecentTrackIds { get; set; } = 50;
    public int MaxTagAffinities { get; set; } = 100;
    public int MaxClusterAffinities { get; set; } = 50;
    public int MaxFavoriteArtistIds { get; set; } = 50;
    public int MaxFavoriteAlbumIds { get; set; } = 50;
}
