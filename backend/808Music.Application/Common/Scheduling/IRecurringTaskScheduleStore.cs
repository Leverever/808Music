namespace _808Music.Application.Common.Scheduling;

public interface IRecurringTaskScheduleStore
{
    Task<DateTime> GetOrCreateNextRunAsync(
        string taskName,
        string cronExpression,
        DateTime nowUtc,
        DateTime nextCronRunUtc,
        CancellationToken cancellationToken = default);

    Task RecordRunStartedAsync(
        string taskName,
        DateTime scheduledRunUtc,
        DateTime startedAtUtc,
        DateTime nextRunUtc,
        CancellationToken cancellationToken = default);
}
