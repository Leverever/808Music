namespace _808Music.Infrastructure.AutomaticPlaylists;

public sealed class AutomaticPlaylistOptions
{
    public const string SectionName = "AutomaticPlaylists";

    public bool RecurringEnabled { get; set; } = false;
    public string RecurringCronExpression { get; set; } = "30 2 * * *";
    public int SourceWindowDays { get; set; } = 90;
    public int DefaultTrackCount { get; set; } = 25;
    public int MaxTrackCount { get; set; } = 50;
}
