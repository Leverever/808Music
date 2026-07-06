namespace _808Music.Application.AudioAnalysis;

public interface IAudioAnalysisJobQueue
{
    Task EnqueueAsync(
        AudioAnalysisRequestedMessage message,
        CancellationToken cancellationToken = default);
}

public sealed record AudioAnalysisRequestedMessage(
    Guid AnalysisId,
    int TrackId,
    string MasterObjectKey,
    string ProviderName,
    string ModelName,
    string ModelVersion);
