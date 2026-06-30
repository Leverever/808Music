namespace _808Music.Application.Abstractions;

public interface IStemSeparationService
{
    Task<StemSeparationJob> StartAsync(
        Guid trackId,
        string? requestedByUserId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<StemManifestItem>> GetManifestAsync(
        Guid trackId,
        CancellationToken cancellationToken = default);
}

public sealed record StemSeparationJob(
    Guid JobId,
    Guid TrackId,
    string Status,
    DateTimeOffset CreatedAt);

public sealed record StemManifestItem(
    string Name,
    string ContentType,
    Uri StreamUri);
