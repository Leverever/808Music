using _808Music.Application.Abstractions;
using _808Music.Domain.Catalog;
using _808Music.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace _808Music.Application.Stems;

public sealed record SeparateTrackStemsCommand(int TrackId, string? RequestedByUserId);

public sealed record SeparateTrackStemsResult(StemSeparationJob Job);

public sealed record GetTrackStemsQuery(int TrackId, string? RequestedByUserId);

public sealed record TrackStemItemResult(
    Guid Id,
    string Name,
    string ContentType,
    long SizeBytes,
    Uri StreamUri);

public sealed record TrackStemSetResult(
    Guid Id,
    string Source,
    string Status,
    string StemProfile,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? CompletedAt,
    string? ErrorMessage,
    IReadOnlyList<TrackStemItemResult> Stems);

public sealed record GetTrackStemsResult(
    int TrackId,
    IReadOnlyList<TrackStemSetResult> StemSets);

public sealed record ActivateTrackStemSetCommand(int TrackId, Guid StemSetId);
public sealed record DeleteTrackStemSetCommand(int TrackId, Guid StemSetId);

public interface ISeparateTrackStemsHandler
{
    Task<SeparateTrackStemsResult> Handle(
        SeparateTrackStemsCommand command,
        CancellationToken cancellationToken = default);
}

public interface IGetTrackStemsHandler
{
    Task<GetTrackStemsResult> Handle(
        GetTrackStemsQuery query,
        CancellationToken cancellationToken = default);
}

public interface IManageTrackStemSetsHandler
{
    Task<TrackStemSetResult?> Activate(
        ActivateTrackStemSetCommand command,
        CancellationToken cancellationToken = default);

    Task<bool> Delete(
        DeleteTrackStemSetCommand command,
        CancellationToken cancellationToken = default);
}

public sealed class SeparateTrackStemsHandler : ISeparateTrackStemsHandler
{
    private readonly IStemSeparationService _stemSeparationService;

    public SeparateTrackStemsHandler(IStemSeparationService stemSeparationService)
    {
        _stemSeparationService = stemSeparationService;
    }

    public async Task<SeparateTrackStemsResult> Handle(
        SeparateTrackStemsCommand command,
        CancellationToken cancellationToken = default)
    {
        var job = await _stemSeparationService.StartAsync(
            command.TrackId,
            command.RequestedByUserId,
            cancellationToken);

        return new SeparateTrackStemsResult(job);
    }
}

public sealed class GetTrackStemsHandler : IGetTrackStemsHandler
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IMediaStorage _mediaStorage;

    public GetTrackStemsHandler(
        IApplicationDbContext dbContext,
        IMediaStorage mediaStorage)
    {
        _dbContext = dbContext;
        _mediaStorage = mediaStorage;
    }

    public async Task<GetTrackStemsResult> Handle(
        GetTrackStemsQuery query,
        CancellationToken cancellationToken = default)
    {
        var sets = await _dbContext.TrackStemSets
            .AsNoTracking()
            .Include(x => x.Stems)
            .Where(x => x.TrackId == query.TrackId)
            .OrderByDescending(x => x.IsActive)
            .ThenByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        var responses = new List<TrackStemSetResult>();
        foreach (var set in sets)
        {
            responses.Add(await ToResult(set, _mediaStorage, cancellationToken));
        }

        return new GetTrackStemsResult(query.TrackId, responses);
    }

    internal static async Task<TrackStemSetResult> ToResult(
        TrackStemSet set,
        IMediaStorage mediaStorage,
        CancellationToken cancellationToken)
    {
        var stems = new List<TrackStemItemResult>();
        foreach (var stem in set.Stems.OrderBy(x => x.StemType))
        {
            stems.Add(new TrackStemItemResult(
                stem.Id,
                stem.StemType.ToString(),
                stem.ContentType,
                stem.SizeBytes,
                await mediaStorage.CreateReadUrlAsync(
                    stem.ObjectKey,
                    TimeSpan.FromMinutes(10),
                    cancellationToken)));
        }

        return new TrackStemSetResult(
            set.Id,
            set.Source.ToString(),
            set.Status.ToString(),
            set.StemProfile,
            set.IsActive,
            set.CreatedAt,
            set.CompletedAt,
            set.ErrorMessage,
            stems);
    }
}

public sealed class ManageTrackStemSetsHandler : IManageTrackStemSetsHandler
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IMediaStorage _mediaStorage;

    public ManageTrackStemSetsHandler(
        IApplicationDbContext dbContext,
        IMediaStorage mediaStorage)
    {
        _dbContext = dbContext;
        _mediaStorage = mediaStorage;
    }

    public async Task<TrackStemSetResult?> Activate(
        ActivateTrackStemSetCommand command,
        CancellationToken cancellationToken = default)
    {
        var sets = await _dbContext.TrackStemSets
            .Include(x => x.Stems)
            .Where(x => x.TrackId == command.TrackId)
            .ToListAsync(cancellationToken);
        var target = sets.FirstOrDefault(x => x.Id == command.StemSetId);
        if (target is null)
        {
            return null;
        }

        if (target.Status != StemSetStatus.Ready)
        {
            throw new InvalidOperationException("Only a ready stem set can be activated.");
        }

        foreach (var set in sets.Where(x => x.IsActive && x.Id != target.Id))
        {
            set.Deactivate();
        }

        target.Activate();
        await _dbContext.SaveChangesAsync(cancellationToken);

        return await GetTrackStemsHandler.ToResult(target, _mediaStorage, cancellationToken);
    }

    public async Task<bool> Delete(
        DeleteTrackStemSetCommand command,
        CancellationToken cancellationToken = default)
    {
        var sets = await _dbContext.TrackStemSets
            .Include(x => x.Stems)
            .Where(x => x.TrackId == command.TrackId)
            .OrderByDescending(x => x.CompletedAt)
            .ThenByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
        var target = sets.FirstOrDefault(x => x.Id == command.StemSetId);
        if (target is null)
        {
            return false;
        }

        if (target.Status is StemSetStatus.Pending or StemSetStatus.Processing)
        {
            throw new InvalidOperationException("A stem set cannot be deleted while it is processing.");
        }

        if (target.IsActive)
        {
            var fallback = sets.FirstOrDefault(
                x => x.Id != target.Id && x.Status == StemSetStatus.Ready);
            fallback?.Activate();
        }

        var objectKeys = target.Stems.Select(x => x.ObjectKey).ToArray();
        _dbContext.TrackStemSets.Remove(target);
        await _dbContext.SaveChangesAsync(cancellationToken);

        foreach (var objectKey in objectKeys)
        {
            try
            {
                await _mediaStorage.DeleteAsync(objectKey, CancellationToken.None);
            }
            catch
            {
                // The database no longer references this object. Storage cleanup can be retried.
            }
        }

        return true;
    }
}
