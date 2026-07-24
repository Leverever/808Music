namespace _808Music.Application.AudioClustering;

public interface IAudioClusteringJobQueue
{
    Task EnqueueAsync(
        AudioClusteringRequestedMessage message,
        CancellationToken cancellationToken = default);
}

public sealed record AudioClusteringRequestedMessage(
    Guid ClusterRunId,
    string AlgorithmName,
    string EmbeddingSource,
    IReadOnlyDictionary<string, object?> Parameters);
