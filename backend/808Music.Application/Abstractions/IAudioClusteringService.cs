namespace _808Music.Application.Abstractions;

public interface IAudioClusteringService
{
    Task<AudioClusteringJob> StartAsync(
        string algorithmName,
        string embeddingSource,
        string parametersJson,
        CancellationToken cancellationToken = default);
}

public sealed record AudioClusteringJob(
    Guid JobId,
    string AlgorithmName,
    string EmbeddingSource,
    string Status,
    DateTimeOffset CreatedAt);
