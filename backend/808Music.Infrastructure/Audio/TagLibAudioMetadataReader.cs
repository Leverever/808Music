using _808Music.Application.Abstractions;

namespace _808Music.Infrastructure.Audio;

public sealed class TagLibAudioMetadataReader : IAudioMetadataReader
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

            await using (var tempFile = System.IO.File.Create(tempPath))
            {
                await content.CopyToAsync(tempFile, cancellationToken);
            }

            using var mediaFile = TagLib.File.Create(tempPath);
            return new AudioMetadata(mediaFile.Properties.Duration);
        }
        catch (TagLib.CorruptFileException ex)
        {
            throw new InvalidOperationException("The uploaded track is not a valid audio file.", ex);
        }
        catch (TagLib.UnsupportedFormatException ex)
        {
            throw new InvalidOperationException("The uploaded track audio format is unsupported.", ex);
        }
        finally
        {
            content.Position = 0;

            if (System.IO.File.Exists(tempPath))
            {
                System.IO.File.Delete(tempPath);
            }
        }
    }
}
