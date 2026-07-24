namespace RS1_2024_25.API.Controllers.V2.Requests;

public sealed class GeneratePlaylistRequest
{
    public string Prompt { get; init; } = string.Empty;

    public int? TrackCount { get; init; }

    public IReadOnlyCollection<Guid> SeedTrackIds { get; init; } = [];

    public IReadOnlyCollection<string> Genres { get; init; } = [];
}
