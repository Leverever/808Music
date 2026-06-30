namespace _808Music.Application.Abstractions;

public interface IAiPlaylistGenerator
{
    Task<GeneratedPlaylist> GenerateAsync(
        PlaylistPrompt prompt,
        CancellationToken cancellationToken = default);
}

public sealed record PlaylistPrompt(
    string Prompt,
    int TrackCount,
    IReadOnlyCollection<Guid> SeedTrackIds,
    IReadOnlyCollection<string> Genres,
    string? RequestedByUserId);

public sealed record GeneratedPlaylist(
    string Name,
    string Description,
    IReadOnlyList<GeneratedPlaylistTrack> Tracks);

public sealed record GeneratedPlaylistTrack(
    Guid TrackId,
    string Title,
    string ArtistName,
    string Reason);
