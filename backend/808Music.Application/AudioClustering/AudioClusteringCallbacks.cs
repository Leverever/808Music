using _808Music.Domain.Catalog;
using _808Music.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace _808Music.Application.AudioClustering;

public sealed record MarkAudioClusteringProcessingCommand(Guid ClusterRunId);

public sealed record GetAudioClusteringTracksQuery(
    Guid ClusterRunId,
    string EmbeddingSource);

public sealed record GetAudioClusteringTracksResult(
    IReadOnlyList<AudioClusteringTrackDto> Tracks);

public sealed record AudioClusteringTrackDto(
    int TrackId,
    string Embedding,
    IReadOnlyList<AudioClusteringTagDto> Tags);

public sealed record AudioClusteringTagDto(
    string Namespace,
    string Label,
    decimal Score);

public sealed record CompleteAudioClusteringCommand(
    Guid ClusterRunId,
    string AlgorithmName,
    string EmbeddingSource,
    IReadOnlyList<AudioClusterDto> Clusters,
    IReadOnlyList<TrackClusterAssignmentDto> Assignments);

public sealed record AudioClusterDto(
    string ClusterKey,
    string Name,
    int Size,
    IReadOnlyList<AudioClusteringTagDto> TopTags);

public sealed record TrackClusterAssignmentDto(
    int TrackId,
    string ClusterKey,
    bool IsNoise,
    decimal? DistanceToCenter,
    decimal? MembershipScore);

public sealed record FailAudioClusteringCommand(
    Guid ClusterRunId,
    string ErrorMessage);

public interface IMarkAudioClusteringProcessingHandler
{
    Task Handle(
        MarkAudioClusteringProcessingCommand command,
        CancellationToken cancellationToken = default);
}

public interface IGetAudioClusteringTracksHandler
{
    Task<GetAudioClusteringTracksResult> Handle(
        GetAudioClusteringTracksQuery query,
        CancellationToken cancellationToken = default);
}

public interface ICompleteAudioClusteringHandler
{
    Task Handle(
        CompleteAudioClusteringCommand command,
        CancellationToken cancellationToken = default);
}

public interface IFailAudioClusteringHandler
{
    Task Handle(
        FailAudioClusteringCommand command,
        CancellationToken cancellationToken = default);
}

public sealed class MarkAudioClusteringProcessingHandler : IMarkAudioClusteringProcessingHandler
{
    private readonly IApplicationDbContext _dbContext;

    public MarkAudioClusteringProcessingHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task Handle(
        MarkAudioClusteringProcessingCommand command,
        CancellationToken cancellationToken = default)
    {
        var run = await _dbContext.AudioClusterRuns
            .FirstOrDefaultAsync(x => x.Id == command.ClusterRunId, cancellationToken);

        if (run is null)
        {
            throw new KeyNotFoundException("Audio cluster run was not found.");
        }

        if (run.Status == AudioClusterRunStatus.Pending)
        {
            run.MarkProcessing();
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}

public sealed class GetAudioClusteringTracksHandler : IGetAudioClusteringTracksHandler
{
    private readonly IApplicationDbContext _dbContext;

    public GetAudioClusteringTracksHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<GetAudioClusteringTracksResult> Handle(
        GetAudioClusteringTracksQuery query,
        CancellationToken cancellationToken = default)
    {
        var runExists = await _dbContext.AudioClusterRuns
            .AnyAsync(x => x.Id == query.ClusterRunId, cancellationToken);

        if (!runExists)
        {
            throw new KeyNotFoundException("Audio cluster run was not found.");
        }

        var tracks = await _dbContext.TrackAudioAnalyses
            .AsNoTracking()
            .Include(x => x.Tags)
            .Where(x =>
                x.IsActive &&
                x.Status == AudioAnalysisStatus.Ready &&
                x.EmbeddingJson != null &&
                (string.IsNullOrWhiteSpace(query.EmbeddingSource) ||
                    x.ProviderName == query.EmbeddingSource ||
                    x.EmbeddingModel == query.EmbeddingSource))
            .Select(x => new AudioClusteringTrackDto(
                x.TrackId,
                x.EmbeddingJson!,
                x.Tags
                    .Select(tag => new AudioClusteringTagDto(
                        tag.Namespace,
                        tag.Label,
                        tag.Score))
                    .ToList()))
            .ToListAsync(cancellationToken);

        return new GetAudioClusteringTracksResult(tracks);
    }
}

public sealed class CompleteAudioClusteringHandler : ICompleteAudioClusteringHandler
{
    private readonly IApplicationDbContext _dbContext;

    public CompleteAudioClusteringHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task Handle(
        CompleteAudioClusteringCommand command,
        CancellationToken cancellationToken = default)
    {
        var run = await _dbContext.AudioClusterRuns
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == command.ClusterRunId, cancellationToken);

        if (run is null)
        {
            throw new KeyNotFoundException("Audio cluster run was not found.");
        }

        if (run.Status == AudioClusterRunStatus.Ready)
        {
            return;
        }

        if (!run.AlgorithmName.Equals(command.AlgorithmName, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Cluster run algorithm does not match the completion payload.");
        }

        if (!run.EmbeddingSource.Equals(command.EmbeddingSource, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Cluster run embedding source does not match the completion payload.");
        }

        var dbContext = _dbContext as DbContext ??
            throw new InvalidOperationException("Audio clustering completion requires an EF Core DbContext.");

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        await _dbContext.TrackClusterAssignments
            .Where(x => x.ClusterRunId == command.ClusterRunId)
            .ExecuteDeleteAsync(cancellationToken);

        await _dbContext.AudioClusters
            .Where(x => x.ClusterRunId == command.ClusterRunId)
            .ExecuteDeleteAsync(cancellationToken);

        var clusters = command.Clusters
            .Select(cluster => new AudioCluster(
                command.ClusterRunId,
                cluster.ClusterKey,
                cluster.Name,
                cluster.Size,
                JsonSerializer.Serialize(cluster.TopTags)))
            .ToList();

        await _dbContext.AudioClusters.AddRangeAsync(clusters, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var clusterIdsByKey = clusters.ToDictionary(x => x.ClusterKey, x => x.Id);
        var assignments = command.Assignments
            .Select(assignment =>
            {
                Guid? clusterId = clusterIdsByKey.TryGetValue(assignment.ClusterKey, out var matchedClusterId)
                    ? matchedClusterId
                    : null;

                return new TrackClusterAssignment(
                    command.ClusterRunId,
                    clusterId,
                    assignment.TrackId,
                    assignment.ClusterKey,
                    assignment.IsNoise,
                    assignment.DistanceToCenter,
                    assignment.MembershipScore);
            })
            .ToList();

        await _dbContext.TrackClusterAssignments.AddRangeAsync(assignments, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _dbContext.AudioClusterRuns
            .Where(x =>
                x.Id != command.ClusterRunId &&
                x.IsActive &&
                x.EmbeddingSource == run.EmbeddingSource &&
                x.AlgorithmName == run.AlgorithmName)
            .ExecuteUpdateAsync(
                updates => updates.SetProperty(x => x.IsActive, false),
                cancellationToken);

        var completedAt = DateTime.UtcNow;
        var affectedRows = await _dbContext.AudioClusterRuns
            .Where(x => x.Id == command.ClusterRunId)
            .ExecuteUpdateAsync(
                updates => updates
                    .SetProperty(x => x.Status, AudioClusterRunStatus.Ready)
                    .SetProperty(x => x.IsActive, true)
                    .SetProperty(x => x.CompletedAt, completedAt)
                    .SetProperty(x => x.ErrorMessage, (string?)null)
                    .SetProperty(x => x.StartedAt, x => x.StartedAt ?? completedAt),
                cancellationToken);

        if (affectedRows == 0)
        {
            throw new KeyNotFoundException("Audio cluster run was not found while completing the job.");
        }

        await transaction.CommitAsync(cancellationToken);
    }
}

public sealed class FailAudioClusteringHandler : IFailAudioClusteringHandler
{
    private readonly IApplicationDbContext _dbContext;

    public FailAudioClusteringHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task Handle(
        FailAudioClusteringCommand command,
        CancellationToken cancellationToken = default)
    {
        var run = await _dbContext.AudioClusterRuns
            .FirstOrDefaultAsync(x => x.Id == command.ClusterRunId, cancellationToken);

        if (run is null)
        {
            throw new KeyNotFoundException("Audio cluster run was not found.");
        }

        if (run.Status == AudioClusterRunStatus.Ready)
        {
            return;
        }

        run.MarkFailed(command.ErrorMessage);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
