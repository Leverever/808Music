namespace _808Music.Application.Abstractions;

public interface IAudioMetadataReader
{
    Task<AudioMetadata> ReadAsync(
        Stream content,
        string fileName,
        string contentType,
        CancellationToken cancellationToken = default);
}

public sealed record AudioMetadata(
    TimeSpan Duration);
