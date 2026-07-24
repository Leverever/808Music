using _808Music.Application.Abstractions;

namespace _808Music.Application.AutomaticPlaylists;

public sealed record GenerateDailyAutomaticPlaylistsCommand(DateOnly PlaylistDate);

public sealed record GenerateDailyAutomaticPlaylistsResult(
    DateOnly PlaylistDate,
    int GeneratedPlaylistCount);

public interface IGenerateDailyAutomaticPlaylistsHandler
{
    Task<GenerateDailyAutomaticPlaylistsResult> Handle(
        GenerateDailyAutomaticPlaylistsCommand command,
        CancellationToken cancellationToken = default);
}

public sealed class GenerateDailyAutomaticPlaylistsHandler : IGenerateDailyAutomaticPlaylistsHandler
{
    private readonly IAutomaticPlaylistGenerationService _playlistGenerationService;

    public GenerateDailyAutomaticPlaylistsHandler(
        IAutomaticPlaylistGenerationService playlistGenerationService)
    {
        _playlistGenerationService = playlistGenerationService;
    }

    public async Task<GenerateDailyAutomaticPlaylistsResult> Handle(
        GenerateDailyAutomaticPlaylistsCommand command,
        CancellationToken cancellationToken = default)
    {
        var result = await _playlistGenerationService.GenerateDailyAsync(
            command.PlaylistDate,
            cancellationToken);

        return new GenerateDailyAutomaticPlaylistsResult(
            result.PlaylistDate,
            result.GeneratedPlaylistCount);
    }
}
