namespace _808Music.Application.Abstractions;

public interface IMediaStorage
{
    Task<StoredMediaObject> UploadAsync(
        UploadMediaObject request,
        CancellationToken cancellationToken = default);

    Task<Uri> CreateReadUrlAsync(
        string objectKey,
        TimeSpan expiresIn,
        CancellationToken cancellationToken = default);

    Task<MediaObjectMetadata?> GetMetadataAsync(
        string objectKey,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        string objectKey,
        CancellationToken cancellationToken = default);
}

public sealed record UploadMediaObject(
    string ObjectKey,
    Stream Content,
    string ContentType,
    IReadOnlyDictionary<string, string>? Metadata = null);

public sealed record StoredMediaObject(
    string ObjectKey,
    string ContentType,
    long? SizeInBytes);

public sealed record MediaObjectMetadata(
    string ObjectKey,
    string ContentType,
    long SizeInBytes,
    IReadOnlyDictionary<string, string> Metadata);
