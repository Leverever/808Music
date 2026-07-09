using _808Music.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace _808Music.Infrastructure.AutomaticPlaylists;

public sealed class NoOpAutomaticPlaylistGenerationService : IAutomaticPlaylistGenerationService
{
    private readonly ILogger<NoOpAutomaticPlaylistGenerationService> _logger;

    public NoOpAutomaticPlaylistGenerationService(
        ILogger<NoOpAutomaticPlaylistGenerationService> logger)
    {
        _logger = logger;
    }

    public Task<AutomaticPlaylistGenerationResult> GenerateDailyAsync(
        DateOnly playlistDate,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Automatic playlist generation is not implemented yet for date {PlaylistDate}",
            playlistDate);

        return Task.FromResult(new AutomaticPlaylistGenerationResult(
            playlistDate,
            GeneratedPlaylistCount: 0));
    }
}
