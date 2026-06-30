namespace _808Music.Application.Abstractions;

public interface IMediaStorage
{
    Task<MediaFileDescriptor?> GetTrackFileAsync(Guid trackId, CancellationToken cancellationToken = default);

    Task<Uri> GetStreamUriAsync(Guid trackId, string streamName, CancellationToken cancellationToken = default);
}

public sealed record MediaFileDescriptor(
    Guid TrackId,
    string StorageKey,
    string ContentType,
    long? SizeInBytes);
