using _808Music.Application;
using _808Music.Application.Abstractions;
using _808Music.Application.AudioAnalysis;
using _808Music.Domain.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace _808Music.Infrastructure.AudioAnalysis;

public sealed class QueuedAudioAnalysisService : IAudioAnalysisService
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IAudioAnalysisJobQueue _jobQueue;
    private readonly AudioAnalysisOptions _options;

    public QueuedAudioAnalysisService(
        IApplicationDbContext dbContext,
        IAudioAnalysisJobQueue jobQueue,
        IOptions<AudioAnalysisOptions> options)
    {
        _dbContext = dbContext;
        _jobQueue = jobQueue;
        _options = options.Value;
    }

    public async Task<AudioAnalysisJob> StartAsync(
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

        var analysis = new TrackAudioAnalysis(
            track.Id,
            _options.DefaultProvider,
            _options.DefaultModelName,
            _options.DefaultModelVersion);

        await _dbContext.TrackAudioAnalyses.AddAsync(analysis, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        try
        {
            await _jobQueue.EnqueueAsync(
                new AudioAnalysisRequestedMessage(
                    analysis.Id,
                    track.Id,
                    track.TrackPath,
                    analysis.ProviderName,
                    analysis.ModelName,
                    analysis.ModelVersion),
                cancellationToken);
        }
        catch (Exception ex)
        {
            analysis.MarkFailed($"Failed to enqueue audio analysis job: {ex.Message}");
            await _dbContext.SaveChangesAsync(CancellationToken.None);
            throw;
        }

        return new AudioAnalysisJob(
            analysis.Id,
            track.Id,
            analysis.Status.ToString(),
            analysis.CreatedAt);
    }
}
