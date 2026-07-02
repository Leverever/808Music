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

    Task DeleteAsync(
        string objectKey,
        CancellationToken cancellationToken = default);
}

public sealed record UploadMediaObject(
    string ObjectKey,
    Stream Content,
    string ContentType);

public sealed record StoredMediaObject(
    string ObjectKey,
    string ContentType,
    long? SizeInBytes);
