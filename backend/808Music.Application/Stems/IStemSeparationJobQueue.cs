namespace _808Music.Application.Stems;

public interface IStemSeparationJobQueue
{
    Task EnqueueAsync(
        StemSeparationRequestedMessage message,
        CancellationToken cancellationToken = default);
}

public sealed record StemSeparationRequestedMessage(
    Guid StemSetId,
    int TrackId,
    string MasterObjectKey,
    string ProviderName,
    string ModelName,
    string ModelVersion,
    string StemProfile);
