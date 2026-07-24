using _808Music.Application.Abstractions;

namespace _808Music.Application.Ai;

public sealed record GenerateAiPlaylistCommand(
    string Prompt,
    int TrackCount,
    IReadOnlyCollection<Guid> SeedTrackIds,
    IReadOnlyCollection<string> Genres,
    string? RequestedByUserId);

public sealed record GenerateAiPlaylistResult(GeneratedPlaylist Playlist);

public interface IGenerateAiPlaylistHandler
{
    Task<GenerateAiPlaylistResult> Handle(
        GenerateAiPlaylistCommand command,
        CancellationToken cancellationToken = default);
}

public sealed class GenerateAiPlaylistHandler : IGenerateAiPlaylistHandler
{
    private readonly IAiPlaylistGenerator _playlistGenerator;

    public GenerateAiPlaylistHandler(IAiPlaylistGenerator playlistGenerator)
    {
        _playlistGenerator = playlistGenerator;
    }

    public async Task<GenerateAiPlaylistResult> Handle(
        GenerateAiPlaylistCommand command,
        CancellationToken cancellationToken = default)
    {
        var playlist = await _playlistGenerator.GenerateAsync(
            new PlaylistPrompt(
                command.Prompt,
                command.TrackCount,
                command.SeedTrackIds,
                command.Genres,
                command.RequestedByUserId),
            cancellationToken);

        return new GenerateAiPlaylistResult(playlist);
    }
}
