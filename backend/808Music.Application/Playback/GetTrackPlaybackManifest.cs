using _808Music.Application.Abstractions;
using _808Music.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace _808Music.Application.Playback;

public sealed record GetTrackPlaybackManifestQuery(
    int TrackId,
    TimeSpan SignedUrlLifetime);

public sealed record GetTrackPlaybackManifestResult(
    PlaybackTrackDto Track,
    PlaybackStreamDto Stream);

public sealed record PlaybackTrackDto(
    int Id,
    string Title,
    bool IsExplicit,
    int LengthSeconds,
    int Streams,
    IReadOnlyList<PlaybackArtistDto> Artists);

public sealed record PlaybackArtistDto(
    int Id,
    string Name,
    bool IsLead,
    string Role);

public sealed record PlaybackStreamDto(
    DateTimeOffset ExpiresAt,
    PlaybackAssetDto Master,
    PlaybackStemSetDto? StemSet);

public sealed record PlaybackAssetDto(
    string Name,
    string ContentType,
    Uri Url);

public sealed record PlaybackStemSetDto(
    Guid Id,
    string Source,
    string StemProfile,
    IReadOnlyList<PlaybackAssetDto> Stems);

public interface IGetTrackPlaybackManifestHandler
{
    Task<GetTrackPlaybackManifestResult?> Handle(
        GetTrackPlaybackManifestQuery query,
        CancellationToken cancellationToken = default);
}

public sealed class GetTrackPlaybackManifestHandler : IGetTrackPlaybackManifestHandler
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IMediaStorage _mediaStorage;

    public GetTrackPlaybackManifestHandler(
        IApplicationDbContext dbContext,
        IMediaStorage mediaStorage)
    {
        _dbContext = dbContext;
        _mediaStorage = mediaStorage;
    }

    public async Task<GetTrackPlaybackManifestResult?> Handle(
        GetTrackPlaybackManifestQuery query,
        CancellationToken cancellationToken = default)
    {
        if (query.TrackId <= 0)
        {
            throw new InvalidOperationException("Track id is required.");
        }

        var lifetime = query.SignedUrlLifetime <= TimeSpan.Zero
            ? TimeSpan.FromHours(2)
            : query.SignedUrlLifetime;

        var expiresAt = DateTimeOffset.UtcNow.Add(lifetime);

        var track = await _dbContext.Tracks
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == query.TrackId, cancellationToken);

        if (track is null)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(track.TrackPath))
        {
            throw new InvalidOperationException("Track has no master object key.");
        }

        var artists = await _dbContext.ArtistTracks
            .AsNoTracking()
            .Include(x => x.Artist)
            .Where(x => x.TrackId == track.Id)
            .OrderByDescending(x => x.IsLead)
            .ThenBy(x => x.Artist!.Name)
            .Select(x => new PlaybackArtistDto(
                x.ArtistId,
                x.Artist!.Name,
                x.IsLead,
                x.IsLead ? "Main" : "Featured"))
            .ToListAsync(cancellationToken);

        var master = new PlaybackAssetDto(
            "Master",
            InferContentType(track.TrackPath),
            await _mediaStorage.CreateReadUrlAsync(
                track.TrackPath,
                lifetime,
                cancellationToken));

        var stemSet = await _dbContext.TrackStemSets
            .AsNoTracking()
            .Include(x => x.Stems)
            .Where(x => x.TrackId == track.Id && x.Status == StemSetStatus.Ready)
            .OrderByDescending(x => x.IsActive)
            .ThenByDescending(x => x.CompletedAt)
            .ThenByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        PlaybackStemSetDto? playbackStemSet = null;
        if (stemSet is not null)
        {
            var stems = new List<PlaybackAssetDto>();

            foreach (var stem in stemSet.Stems.OrderBy(x => x.StemType))
            {
                stems.Add(new PlaybackAssetDto(
                    stem.StemType.ToString(),
                    stem.ContentType,
                    await _mediaStorage.CreateReadUrlAsync(
                        stem.ObjectKey,
                        lifetime,
                        cancellationToken)));
            }

            playbackStemSet = new PlaybackStemSetDto(
                stemSet.Id,
                stemSet.Source.ToString(),
                stemSet.StemProfile,
                stems);
        }

        return new GetTrackPlaybackManifestResult(
            new PlaybackTrackDto(
                track.Id,
                track.Title,
                track.IsExplicit,
                track.Length,
                track.Streams,
                artists),
            new PlaybackStreamDto(
                expiresAt,
                master,
                playbackStemSet));
    }

    private static string InferContentType(string objectKey)
    {
        return Path.GetExtension(objectKey).ToLowerInvariant() switch
        {
            ".mp3" => "audio/mpeg",
            ".wav" => "audio/wav",
            ".flac" => "audio/flac",
            ".m4a" => "audio/mp4",
            ".aac" => "audio/aac",
            ".ogg" => "audio/ogg",
            _ => "application/octet-stream"
        };
    }
}
