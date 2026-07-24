using _808Music.Domain.Catalog;
using _808Music.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace _808Music.Application.AudioAnalysis;

public sealed record MarkAudioAnalysisProcessingCommand(Guid AnalysisId);

public sealed record CompleteAudioAnalysisCommand(
    Guid AnalysisId,
    int TrackId,
    string EmbeddingModel,
    IReadOnlyList<double> Embedding,
    IReadOnlyList<AudioAnalysisTagDto> Tags);

public sealed record AudioAnalysisTagDto(
    string Namespace,
    string Label,
    decimal Score,
    string ModelName);

public sealed record FailAudioAnalysisCommand(
    Guid AnalysisId,
    string ErrorMessage);

public interface IMarkAudioAnalysisProcessingHandler
{
    Task Handle(
        MarkAudioAnalysisProcessingCommand command,
        CancellationToken cancellationToken = default);
}

public interface ICompleteAudioAnalysisHandler
{
    Task Handle(
        CompleteAudioAnalysisCommand command,
        CancellationToken cancellationToken = default);
}

public interface IFailAudioAnalysisHandler
{
    Task Handle(
        FailAudioAnalysisCommand command,
        CancellationToken cancellationToken = default);
}

public sealed class MarkAudioAnalysisProcessingHandler : IMarkAudioAnalysisProcessingHandler
{
    private readonly IApplicationDbContext _dbContext;

    public MarkAudioAnalysisProcessingHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task Handle(
        MarkAudioAnalysisProcessingCommand command,
        CancellationToken cancellationToken = default)
    {
        var analysis = await _dbContext.TrackAudioAnalyses
            .FirstOrDefaultAsync(x => x.Id == command.AnalysisId, cancellationToken);

        if (analysis is null)
        {
            throw new KeyNotFoundException("Audio analysis was not found.");
        }

        if (analysis.Status == AudioAnalysisStatus.Pending)
        {
            analysis.MarkProcessing();
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}

public sealed class CompleteAudioAnalysisHandler : ICompleteAudioAnalysisHandler
{
    private readonly IApplicationDbContext _dbContext;

    public CompleteAudioAnalysisHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task Handle(
        CompleteAudioAnalysisCommand command,
        CancellationToken cancellationToken = default)
    {
        if (command.Embedding.Count == 0)
        {
            throw new InvalidOperationException("Audio analysis embedding is required.");
        }

        var analysis = await _dbContext.TrackAudioAnalyses
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == command.AnalysisId, cancellationToken);

        if (analysis is null)
        {
            throw new KeyNotFoundException("Audio analysis was not found.");
        }

        if (analysis.TrackId != command.TrackId)
        {
            throw new InvalidOperationException("Audio analysis track id does not match the callback payload.");
        }

        if (analysis.Status == AudioAnalysisStatus.Ready)
        {
            return;
        }

        var tags = command.Tags
            .Where(tag => !string.IsNullOrWhiteSpace(tag.Namespace) && !string.IsNullOrWhiteSpace(tag.Label))
            .Select(tag => new TrackAudioTag(
                command.AnalysisId,
                tag.Namespace.Trim(),
                tag.Label.Trim(),
                tag.Score,
                string.IsNullOrWhiteSpace(tag.ModelName) ? analysis.ModelName : tag.ModelName.Trim()))
            .ToList();

        var dbContext = _dbContext as DbContext ??
            throw new InvalidOperationException("Audio analysis completion requires an EF Core DbContext.");

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        await _dbContext.TrackAudioTags
            .Where(x => x.TrackAudioAnalysisId == command.AnalysisId)
            .ExecuteDeleteAsync(cancellationToken);

        await _dbContext.TrackAudioTags.AddRangeAsync(tags, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _dbContext.TrackAudioAnalyses
            .Where(x => x.TrackId == analysis.TrackId && x.Id != analysis.Id && x.IsActive)
            .ExecuteUpdateAsync(
                updates => updates.SetProperty(x => x.IsActive, false),
                cancellationToken);

        var completedAt = DateTime.UtcNow;
        var embeddingJson = JsonSerializer.Serialize(command.Embedding);
        var affectedRows = await _dbContext.TrackAudioAnalyses
            .Where(x => x.Id == command.AnalysisId)
            .ExecuteUpdateAsync(
                updates => updates
                    .SetProperty(x => x.Status, AudioAnalysisStatus.Ready)
                    .SetProperty(x => x.IsActive, true)
                    .SetProperty(x => x.EmbeddingModel, command.EmbeddingModel)
                    .SetProperty(x => x.EmbeddingJson, embeddingJson)
                    .SetProperty(x => x.CompletedAt, completedAt)
                    .SetProperty(x => x.ErrorMessage, (string?)null)
                    .SetProperty(x => x.StartedAt, x => x.StartedAt ?? completedAt),
                cancellationToken);

        if (affectedRows == 0)
        {
            throw new KeyNotFoundException("Audio analysis was not found while completing the job.");
        }

        await transaction.CommitAsync(cancellationToken);
    }
}

public sealed class FailAudioAnalysisHandler : IFailAudioAnalysisHandler
{
    private readonly IApplicationDbContext _dbContext;

    public FailAudioAnalysisHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task Handle(
        FailAudioAnalysisCommand command,
        CancellationToken cancellationToken = default)
    {
        var analysis = await _dbContext.TrackAudioAnalyses
            .FirstOrDefaultAsync(x => x.Id == command.AnalysisId, cancellationToken);

        if (analysis is null)
        {
            throw new KeyNotFoundException("Audio analysis was not found.");
        }

        if (analysis.Status == AudioAnalysisStatus.Ready)
        {
            return;
        }

        analysis.MarkFailed(command.ErrorMessage);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
