using _808Music.Domain.Catalog;
using _808Music.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace _808Music.Application.Personalization;

public sealed record RecordUserTrackInteractionCommand(
    int UserId,
    int TrackId,
    UserTrackInteractionType InteractionType,
    long? PlayedMs,
    long? TrackDurationMs,
    string? ContextType,
    string? ClientEventId,
    DateTime? OccurredAt);

public sealed record RecordUserTrackInteractionResult(
    Guid InteractionId,
    bool Created,
    DateTime OccurredAt);

public interface IRecordUserTrackInteractionHandler
{
    Task<RecordUserTrackInteractionResult> Handle(
        RecordUserTrackInteractionCommand command,
        CancellationToken cancellationToken = default);
}

public sealed class RecordUserTrackInteractionHandler : IRecordUserTrackInteractionHandler
{
    private readonly IApplicationDbContext _dbContext;

    public RecordUserTrackInteractionHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<RecordUserTrackInteractionResult> Handle(
        RecordUserTrackInteractionCommand command,
        CancellationToken cancellationToken = default)
    {
        if (command.UserId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(command.UserId), "User id must be positive.");
        }

        if (command.TrackId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(command.TrackId), "Track id must be positive.");
        }

        if (!string.IsNullOrWhiteSpace(command.ClientEventId))
        {
            var existingInteraction = await _dbContext.UserTrackInteractions
                .AsNoTracking()
                .Where(x =>
                    x.UserId == command.UserId &&
                    x.ClientEventId == command.ClientEventId.Trim())
                .Select(x => new
                {
                    x.Id,
                    x.OccurredAt
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (existingInteraction is not null)
            {
                return new RecordUserTrackInteractionResult(
                    existingInteraction.Id,
                    Created: false,
                    existingInteraction.OccurredAt);
            }
        }

        var trackExists = await _dbContext.Tracks
            .AsNoTracking()
            .AnyAsync(x => x.Id == command.TrackId, cancellationToken);

        if (!trackExists)
        {
            throw new KeyNotFoundException("Track was not found.");
        }

        var occurredAt = command.OccurredAt ?? DateTime.UtcNow;
        var interaction = new UserTrackInteraction(
            command.UserId,
            command.TrackId,
            command.InteractionType,
            occurredAt,
            command.PlayedMs,
            command.TrackDurationMs,
            command.ContextType,
            command.ClientEventId);

        await _dbContext.UserTrackInteractions.AddAsync(interaction, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new RecordUserTrackInteractionResult(
            interaction.Id,
            Created: true,
            interaction.OccurredAt);
    }
}
