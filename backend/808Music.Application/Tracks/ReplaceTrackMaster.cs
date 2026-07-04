using _808Music.Application.Abstractions;
using _808Music.Application.Common.Persistence;
using _808Music.Domain.Catalog;

namespace _808Music.Application.Tracks;

public sealed record ReplaceTrackMasterCommand(
    int TrackId,
    string FileName,
    string ContentType,
    Stream Content,
    string? RequestedByUserId);

public sealed record ReplaceTrackMasterResult(
    int Id,
    string ObjectKey);

public interface IReplaceTrackMasterHandler
{
    Task<ReplaceTrackMasterResult?> Handle(
        ReplaceTrackMasterCommand command,
        CancellationToken cancellationToken = default);
}

public sealed class ReplaceTrackMasterHandler : IReplaceTrackMasterHandler
{
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp3",
        ".wav",
        ".flac",
        ".m4a"
    };

    private readonly IMediaStorage _mediaStorage;
    private readonly IAudioMetadataReader _audioMetadataReader;
    private readonly IRepository<Track, int> _trackRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IStemSeparationService _stemSeparationService;

    public ReplaceTrackMasterHandler(
        IMediaStorage mediaStorage,
        IAudioMetadataReader audioMetadataReader,
        IRepository<Track, int> trackRepository,
        IUnitOfWork unitOfWork,
        IStemSeparationService stemSeparationService)
    {
        _mediaStorage = mediaStorage;
        _audioMetadataReader = audioMetadataReader;
        _trackRepository = trackRepository;
        _unitOfWork = unitOfWork;
        _stemSeparationService = stemSeparationService;
    }

    public async Task<ReplaceTrackMasterResult?> Handle(
        ReplaceTrackMasterCommand command,
        CancellationToken cancellationToken = default)
    {
        var track = await _trackRepository.GetByIdAsync(command.TrackId, cancellationToken);
        if (track is null)
        {
            return null;
        }

        var extension = Path.GetExtension(command.FileName);
        if (string.IsNullOrWhiteSpace(extension) || !AllowedExtensions.Contains(extension))
        {
            throw new InvalidOperationException("Unsupported track file type.");
        }

        var oldObjectKey = track.TrackPath;
        var newObjectKey = $"tracks/{command.TrackId}/masters/{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
        var metadata = await _audioMetadataReader.ReadAsync(
            command.Content,
            command.FileName,
            command.ContentType,
            cancellationToken);

        var storedObject = await _mediaStorage.UploadAsync(
            new UploadMediaObject(
                newObjectKey,
                command.Content,
                string.IsNullOrWhiteSpace(command.ContentType)
                    ? "application/octet-stream"
                    : command.ContentType),
            cancellationToken);

        try
        {
            track.TrackPath = storedObject.ObjectKey;
            track.Length = ToWholeSeconds(metadata.Duration);

            _trackRepository.Update(track);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            await _mediaStorage.DeleteAsync(storedObject.ObjectKey, CancellationToken.None);
            throw;
        }

        await TryStartStemSeparationAsync(
            track.Id,
            command.RequestedByUserId);

        if (!string.IsNullOrWhiteSpace(oldObjectKey))
        {
            try
            {
                await _mediaStorage.DeleteAsync(oldObjectKey, CancellationToken.None);
            }
            catch
            {
                // The DB now points at the new master. Old-object cleanup can be retried separately.
            }
        }

        return new ReplaceTrackMasterResult(
            track.Id,
            track.TrackPath);
    }

    private static int ToWholeSeconds(TimeSpan duration)
    {
        return duration <= TimeSpan.Zero
            ? 0
            : Math.Max(1, (int)Math.Round(duration.TotalSeconds, MidpointRounding.AwayFromZero));
    }

    private async Task TryStartStemSeparationAsync(
        int trackId,
        string? requestedByUserId)
    {
        try
        {
            await _stemSeparationService.StartAsync(
                trackId,
                requestedByUserId,
                CancellationToken.None);
        }
        catch
        {
            // The master replacement succeeded. Stem separation can be retried manually if queueing fails.
        }
    }
}
