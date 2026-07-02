using _808Music.Application.Abstractions;
using NAudio.Wave;

namespace _808Music.Infrastructure.Audio;

public sealed class NAudioMetadataReader : IAudioMetadataReader
{
    public async Task<AudioMetadata> ReadAsync(
        Stream content,
        string fileName,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        if (!content.CanSeek)
        {
            throw new InvalidOperationException("Track file stream must be seekable to read audio metadata.");
        }

        var extension = Path.GetExtension(fileName);
        var tempPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}{extension}");

        try
        {
            content.Position = 0;

            await using (var tempFile = File.Create(tempPath))
            {
                await content.CopyToAsync(tempFile, cancellationToken);
            }

            using var reader = new MediaFoundationReader(tempPath);

            return new AudioMetadata(reader.TotalTime);
        }
        finally
        {
            content.Position = 0;

            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }
}
