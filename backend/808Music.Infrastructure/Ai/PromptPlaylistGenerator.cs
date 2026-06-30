using _808Music.Application.Abstractions;

namespace _808Music.Infrastructure.Ai;

public sealed class PromptPlaylistGenerator : IAiPlaylistGenerator
{
    public Task<GeneratedPlaylist> GenerateAsync(
        PlaylistPrompt prompt,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var trackCount = Math.Clamp(prompt.TrackCount, 1, 100);
        var seedIds = prompt.SeedTrackIds.ToList();

        IReadOnlyList<GeneratedPlaylistTrack> tracks = Enumerable
            .Range(1, trackCount)
            .Select(index => new GeneratedPlaylistTrack(
                index <= seedIds.Count ? seedIds[index - 1] : Guid.NewGuid(),
                $"AI Pick {index}",
                "808 Music",
                BuildReason(prompt, index)))
            .ToList();

        var playlist = new GeneratedPlaylist(
            Name: "AI Generated Playlist",
            Description: prompt.Prompt,
            Tracks: tracks);

        return Task.FromResult(playlist);
    }

    private static string BuildReason(PlaylistPrompt prompt, int index)
    {
        if (prompt.Genres.Count == 0)
        {
            return $"Selected from the requested mood and prompt context for slot {index}.";
        }

        return $"Matches {string.Join(", ", prompt.Genres)} and the requested prompt context.";
    }
}
