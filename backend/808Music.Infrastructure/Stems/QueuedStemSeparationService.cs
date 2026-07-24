using _808Music.Application;
using _808Music.Application.Abstractions;
using _808Music.Application.Stems;
using _808Music.Domain.Catalog;
using _808Music.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace _808Music.Infrastructure.Stems;

public sealed class QueuedStemSeparationService : IStemSeparationService
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IStemSeparationJobQueue _jobQueue;
    private readonly IMediaStorage _mediaStorage;
    private readonly StemSeparationOptions _options;

    public QueuedStemSeparationService(
        IApplicationDbContext dbContext,
        IStemSeparationJobQueue jobQueue,
        IMediaStorage mediaStorage,
        IOptions<StemSeparationOptions> options)
    {
        _dbContext = dbContext;
        _jobQueue = jobQueue;
        _mediaStorage = mediaStorage;
        _options = options.Value;
    }

    public async Task<StemSeparationJob> StartAsync(
        int trackId,
        string? requestedByUserId,
        CancellationToken cancellationToken = default)
    {
        var track = await _dbContext.Tracks
            .FirstOrDefaultAsync(x => x.Id == trackId, cancellationToken);

        if (track is null)
        {
            throw new KeyNotFoundException("Track was not found.");
        }

        if (string.IsNullOrWhiteSpace(track.TrackPath))
        {
            throw new InvalidOperationException("Track has no master object key.");
        }

        var stemSet = new TrackStemSet(
            track.Id,
            StemSetSource.AiGenerated,
            requestedByUserId: null,
            _options.DefaultProvider,
            _options.DefaultModelName,
            _options.DefaultModelVersion,
            _options.DefaultStemProfile);

        await _dbContext.TrackStemSets.AddAsync(stemSet, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        try
        {
            await _jobQueue.EnqueueAsync(
                new StemSeparationRequestedMessage(
                    stemSet.Id,
                    track.Id,
                    track.TrackPath,
                    stemSet.ProviderName,
                    stemSet.ModelName ?? string.Empty,
                    stemSet.ModelVersion ?? string.Empty,
                    stemSet.StemProfile),
                cancellationToken);
        }
        catch (Exception ex)
        {
            stemSet.MarkFailed($"Failed to enqueue stem separation job: {ex.Message}");
            await _dbContext.SaveChangesAsync(CancellationToken.None);
            throw;
        }

        return new StemSeparationJob(
            stemSet.Id,
            track.Id,
            stemSet.Status.ToString(),
            stemSet.CreatedAt);
    }

    public async Task<IReadOnlyList<StemManifestItem>> GetManifestAsync(
        int trackId,
        CancellationToken cancellationToken = default)
    {
        var stemSet = await _dbContext.TrackStemSets
            .Include(x => x.Stems)
            .Where(x => x.TrackId == trackId && x.Status == StemSetStatus.Ready)
            .OrderByDescending(x => x.IsActive)
            .ThenByDescending(x => x.CompletedAt)
            .ThenByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (stemSet is null)
        {
            return [];
        }

        var stems = new List<StemManifestItem>();

        foreach (var stem in stemSet.Stems)
        {
            var streamUri = await _mediaStorage.CreateReadUrlAsync(
                stem.ObjectKey,
                TimeSpan.FromMinutes(10),
                cancellationToken);

            stems.Add(new StemManifestItem(
                stem.StemType.ToString(),
                stem.ContentType,
                streamUri));
        }

        return stems;
    }
}
