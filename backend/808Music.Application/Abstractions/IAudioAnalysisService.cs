namespace _808Music.Application.Abstractions;

public interface IAudioAnalysisService
{
    Task<AudioAnalysisJob> StartAsync(
        int trackId,
        string? requestedByUserId,
        CancellationToken cancellationToken = default);
}

public sealed record AudioAnalysisJob(
    Guid JobId,
    int TrackId,
    string Status,
    DateTimeOffset CreatedAt);
