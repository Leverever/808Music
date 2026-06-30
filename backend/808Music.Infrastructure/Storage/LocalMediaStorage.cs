using _808Music.Application.Abstractions;

namespace _808Music.Infrastructure.Storage;

public sealed class LocalMediaStorage : IMediaStorage
{
    public Task<MediaFileDescriptor?> GetTrackFileAsync(
        Guid trackId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var descriptor = new MediaFileDescriptor(
            trackId,
            $"tracks/{trackId:N}.mp3",
            "audio/mpeg",
            SizeInBytes: null);

        return Task.FromResult<MediaFileDescriptor?>(descriptor);
    }

    public Task<Uri> GetStreamUriAsync(
        Guid trackId,
        string streamName,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(new Uri($"/media/stems/{trackId:N}/{streamName}.mp3", UriKind.Relative));
    }
}
