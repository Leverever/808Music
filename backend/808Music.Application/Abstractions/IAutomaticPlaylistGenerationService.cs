namespace _808Music.Application.Abstractions;

public interface IAutomaticPlaylistGenerationService
{
    Task<AutomaticPlaylistGenerationResult> GenerateDailyAsync(
        DateOnly playlistDate,
        CancellationToken cancellationToken = default);
}

public sealed record AutomaticPlaylistGenerationResult(
    DateOnly PlaylistDate,
    int GeneratedPlaylistCount);
