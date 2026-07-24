using _808Music.Domain.Catalog;
using _808Music.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace _808Music.Application.Stems;

public sealed record MarkStemSeparationProcessingCommand(Guid StemSetId);

public sealed record CompleteStemSeparationCommand(
    Guid StemSetId,
    IReadOnlyList<CompletedStemDto> Stems);

public sealed record CompletedStemDto(
    string StemType,
    string ObjectKey,
    string ContentType,
    long SizeBytes,
    int? DurationMs,
    int? SampleRate,
    int? BitrateKbps,
    string? Codec,
    int? Channels,
    string? ChecksumSha256);

public sealed record FailStemSeparationCommand(
    Guid StemSetId,
    string ErrorMessage);

public interface IMarkStemSeparationProcessingHandler
{
    Task Handle(
        MarkStemSeparationProcessingCommand command,
        CancellationToken cancellationToken = default);
}

public interface ICompleteStemSeparationHandler
{
    Task Handle(
        CompleteStemSeparationCommand command,
        CancellationToken cancellationToken = default);
}

public interface IFailStemSeparationHandler
{
    Task Handle(
        FailStemSeparationCommand command,
        CancellationToken cancellationToken = default);
}

public sealed class MarkStemSeparationProcessingHandler : IMarkStemSeparationProcessingHandler
{
    private readonly IApplicationDbContext _dbContext;

    public MarkStemSeparationProcessingHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task Handle(
        MarkStemSeparationProcessingCommand command,
        CancellationToken cancellationToken = default)
    {
        var stemSet = await _dbContext.TrackStemSets
            .FirstOrDefaultAsync(x => x.Id == command.StemSetId, cancellationToken);

        if (stemSet is null)
        {
            throw new KeyNotFoundException("Stem set was not found.");
        }

        if (stemSet.Status == StemSetStatus.Pending)
        {
            stemSet.MarkProcessing();
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}

public sealed class CompleteStemSeparationHandler : ICompleteStemSeparationHandler
{
    private readonly IApplicationDbContext _dbContext;

    public CompleteStemSeparationHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task Handle(
        CompleteStemSeparationCommand command,
        CancellationToken cancellationToken = default)
    {
        if (command.Stems.Count == 0)
        {
            throw new InvalidOperationException("At least one stem is required.");
        }

        var stemSet = await _dbContext.TrackStemSets
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == command.StemSetId, cancellationToken);

        if (stemSet is null)
        {
            throw new KeyNotFoundException("Stem set was not found.");
        }

        if (stemSet.Status == StemSetStatus.Ready)
        {
            return;
        }

        var stems = command.Stems
            .Select(stem => new TrackStem(
                command.StemSetId,
                ParseStemType(stem.StemType),
                "s3",
                stem.ObjectKey,
                stem.ContentType,
                stem.SizeBytes,
                stem.DurationMs,
                stem.SampleRate,
                stem.BitrateKbps,
                stem.Codec,
                stem.Channels,
                stem.ChecksumSha256))
            .ToList();

        var duplicateStemTypes = stems
            .GroupBy(stem => stem.StemType)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();

        if (duplicateStemTypes.Length != 0)
        {
            throw new InvalidOperationException(
                $"Completion payload contains duplicate stems: {string.Join(", ", duplicateStemTypes)}.");
        }

        ValidateRequiredStems(stemSet.StemProfile, stems.Select(stem => stem.StemType));

        var dbContext = _dbContext as DbContext ??
            throw new InvalidOperationException("Stem completion requires an EF Core DbContext.");

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        await _dbContext.TrackStems
            .Where(x => x.StemSetId == command.StemSetId)
            .ExecuteDeleteAsync(cancellationToken);

        await _dbContext.TrackStems.AddRangeAsync(stems, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _dbContext.TrackStemSets
            .Where(x => x.TrackId == stemSet.TrackId && x.Id != stemSet.Id && x.IsActive)
            .ExecuteUpdateAsync(
                updates => updates.SetProperty(x => x.IsActive, false),
                cancellationToken);

        var completedAt = DateTime.UtcNow;
        var affectedRows = await _dbContext.TrackStemSets
            .Where(x => x.Id == command.StemSetId)
            .ExecuteUpdateAsync(
                updates => updates
                    .SetProperty(x => x.Status, StemSetStatus.Ready)
                    .SetProperty(x => x.IsActive, true)
                    .SetProperty(x => x.CompletedAt, completedAt)
                    .SetProperty(x => x.ErrorMessage, (string?)null)
                    .SetProperty(x => x.StartedAt, x => x.StartedAt ?? completedAt),
                cancellationToken);

        if (affectedRows == 0)
        {
            throw new KeyNotFoundException("Stem set was not found while completing the job.");
        }

        await transaction.CommitAsync(cancellationToken);
    }

    private static StemType ParseStemType(string value)
    {
        return Enum.TryParse<StemType>(value, ignoreCase: true, out var stemType)
            ? stemType
            : throw new InvalidOperationException($"Unsupported stem type '{value}'.");
    }

    private static void ValidateRequiredStems(
        string stemProfile,
        IEnumerable<StemType> stemTypes)
    {
        var requiredStemTypes = GetRequiredStemTypes(stemProfile);
        var existingStemTypes = stemTypes.ToHashSet();
        var missingStemTypes = requiredStemTypes
            .Where(stemType => !existingStemTypes.Contains(stemType))
            .ToArray();

        if (missingStemTypes.Length != 0)
        {
            throw new InvalidOperationException(
                $"Stem set is missing required stems: {string.Join(", ", missingStemTypes)}.");
        }
    }

    private static IReadOnlyCollection<StemType> GetRequiredStemTypes(string stemProfile)
    {
        if (stemProfile.Equals("two-stem-vocals", StringComparison.OrdinalIgnoreCase) ||
            stemProfile.Equals("vocals", StringComparison.OrdinalIgnoreCase))
        {
            return
            [
                StemType.Vocals,
                StemType.Instrumental
            ];
        }

        return
        [
            StemType.Vocals,
            StemType.Drums,
            StemType.Bass,
            StemType.Other
        ];
    }
}

public sealed class FailStemSeparationHandler : IFailStemSeparationHandler
{
    private readonly IApplicationDbContext _dbContext;

    public FailStemSeparationHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task Handle(
        FailStemSeparationCommand command,
        CancellationToken cancellationToken = default)
    {
        var stemSet = await _dbContext.TrackStemSets
            .FirstOrDefaultAsync(x => x.Id == command.StemSetId, cancellationToken);

        if (stemSet is null)
        {
            throw new KeyNotFoundException("Stem set was not found.");
        }

        if (stemSet.Status == StemSetStatus.Ready)
        {
            return;
        }

        stemSet.MarkFailed(command.ErrorMessage);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
