namespace _808Music.Infrastructure.AutomaticPlaylists;

public sealed class AutomaticPlaylistOptions
{
    public const string SectionName = "AutomaticPlaylists";

    public bool RecurringEnabled { get; set; } = false;
    public string RecurringCronExpression { get; set; } = "30 2 * * *";
    public int SourceWindowDays { get; set; } = 90;
    public int DefaultTrackCount { get; set; } = 25;
    public int MaxTrackCount { get; set; } = 50;
    public List<AutomaticPlaylistThemeOptions> Themes { get; set; } =
    [
        new()
        {
            ThemeKey = "energetic-mix",
            Name = "Energetic Mix",
            Description = "A daily upbeat mix shaped around your recent listening.",
            TrackCount = 25,
            PositiveTags = ["energetic", "energy", "upbeat", "dance", "electronic", "pop", "party"],
            NegativeTags = ["sad", "ambient", "acoustic", "calm", "sleep"]
        },
        new()
        {
            ThemeKey = "three-am-and-alone",
            Name = "3am and Alone Mix",
            Description = "A late-night, introspective mix picked for your taste.",
            TrackCount = 25,
            PositiveTags = ["sad", "melancholic", "lonely", "night", "ambient", "downtempo", "chill"],
            NegativeTags = ["party", "dance", "gym", "workout", "club"]
        },
        new()
        {
            ThemeKey = "late-night-drive",
            Name = "Late Night Drive Mix",
            Description = "Smooth tracks for night drives, tuned to your profile.",
            TrackCount = 25,
            PositiveTags = ["night", "drive", "chill", "electronic", "synth", "pop", "rnb", "hiphop"],
            NegativeTags = ["workout", "gym", "aggressive"]
        },
        new()
        {
            ThemeKey = "gym-motivation",
            Name = "Gym Motivation Mix",
            Description = "High-energy tracks for training, personalized daily.",
            TrackCount = 25,
            PositiveTags = ["gym", "workout", "energetic", "energy", "aggressive", "hiphop", "rock", "dance"],
            NegativeTags = ["sad", "ambient", "calm", "acoustic", "sleep"]
        }
    ];
}

public sealed class AutomaticPlaylistThemeOptions
{
    public string ThemeKey { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int TrackCount { get; set; } = 25;
    public List<string> PositiveTags { get; set; } = [];
    public List<string> NegativeTags { get; set; } = [];
}
