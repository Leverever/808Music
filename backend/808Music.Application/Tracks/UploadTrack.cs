using _808Music.Application.Abstractions;
using _808Music.Application.Common.Persistence;
using _808Music.Domain.Artists;
using _808Music.Domain.Catalog;

namespace _808Music.Application.Tracks;

public sealed record UploadTrackCommand(
    int ArtistId,
    string Title,
    bool IsExplicit,
    string FileName,
    string ContentType,
    Stream Content,
    string? RequestedByUserId);

public sealed record UploadTrackResult(
    int Id,
    string Title,
    bool IsExplicit,
    int MainArtistId,
    string ObjectKey);

public interface IUploadTrackHandler
{
    Task<UploadTrackResult> Handle(
        UploadTrackCommand command,
        CancellationToken cancellationToken = default);
}

public sealed class UploadTrackHandler : IUploadTrackHandler
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
    private readonly IRepository<Artist, int> _artistRepository;
    private readonly IRepository<Track, int> _trackRepository;
    private readonly IRepository<ArtistTrack, int> _artistTrackRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IStemSeparationService _stemSeparationService;

    public UploadTrackHandler(
        IMediaStorage mediaStorage,
        IAudioMetadataReader audioMetadataReader,
        IRepository<Artist, int> artistRepository,
        IRepository<Track, int> trackRepository,
        IRepository<ArtistTrack, int> artistTrackRepository,
        IUnitOfWork unitOfWork,
        IStemSeparationService stemSeparationService)
    {
        _mediaStorage = mediaStorage;
        _audioMetadataReader = audioMetadataReader;
        _artistRepository = artistRepository;
        _trackRepository = trackRepository;
        _artistTrackRepository = artistTrackRepository;
        _unitOfWork = unitOfWork;
        _stemSeparationService = stemSeparationService;
    }

    public async Task<UploadTrackResult> Handle(
        UploadTrackCommand command,
        CancellationToken cancellationToken = default)
    {
        var artist = await _artistRepository.GetByIdAsync(command.ArtistId, cancellationToken);
        if (artist is null)
        {
            throw new KeyNotFoundException("Artist was not found.");
        }

        var extension = Path.GetExtension(command.FileName);
        if (string.IsNullOrWhiteSpace(extension) || !AllowedExtensions.Contains(extension))
        {
            throw new InvalidOperationException("Unsupported track file type.");
        }

        var objectKey = $"tracks/artists/{command.ArtistId}/{Guid.NewGuid():N}/master{extension.ToLowerInvariant()}";
        var metadata = await _audioMetadataReader.ReadAsync(
            command.Content,
            command.FileName,
            command.ContentType,
            cancellationToken);

        var storedObject = await _mediaStorage.UploadAsync(
            new UploadMediaObject(
                objectKey,
                command.Content,
                string.IsNullOrWhiteSpace(command.ContentType)
                    ? "application/octet-stream"
                    : command.ContentType),
            cancellationToken);

        Track track;

        try
        {
            track = new Track
            {
                Title = command.Title.Trim(),
                IsExplicit = command.IsExplicit,
                TrackPath = storedObject.ObjectKey,
                Streams = 0,
                Length = ToWholeSeconds(metadata.Duration),
                AlbumId = null
            };

            var artistTrack = new ArtistTrack
            {
                ArtistId = command.ArtistId,
                Track = track,
                IsLead = true,
                ShowOnProfile = true
            };

            await _trackRepository.AddAsync(track, cancellationToken);
            await _artistTrackRepository.AddAsync(artistTrack, cancellationToken);
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

        return new UploadTrackResult(
            track.Id,
            track.Title,
            track.IsExplicit,
            command.ArtistId,
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
            // The master upload succeeded. Stem separation can be retried manually if queueing fails.
        }
    }
}
