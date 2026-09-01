using _808Music.Application;
using _808Music.Application.Common.Scheduling;
using _808Music.Domain.Scheduling;
using Microsoft.EntityFrameworkCore;

namespace _808Music.Infrastructure.Persistence;

public sealed class RecurringTaskScheduleStore : IRecurringTaskScheduleStore
{
    private readonly IApplicationDbContext _dbContext;

    public RecurringTaskScheduleStore(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<DateTime> GetOrCreateNextRunAsync(
        string taskName,
        string cronExpression,
        DateTime nowUtc,
        DateTime nextCronRunUtc,
        CancellationToken cancellationToken = default)
    {
        var schedule = await _dbContext.RecurringTaskSchedules
            .SingleOrDefaultAsync(
                item => item.TaskName == taskName,
                cancellationToken);

        if (schedule is null)
        {
            schedule = new RecurringTaskSchedule(
                taskName,
                cronExpression,
                nowUtc);
            _dbContext.RecurringTaskSchedules.Add(schedule);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        else if (!string.Equals(
            schedule.CronExpression,
            cronExpression,
            StringComparison.Ordinal))
        {
            schedule.Reschedule(
                cronExpression,
                schedule.LastStartedAtUtc is null ? nowUtc : nextCronRunUtc);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        else if (schedule.LastStartedAtUtc is null && schedule.NextRunUtc > nowUtc)
        {
            // Schedule rows created before startup execution was introduced have
            // a future next-run value but no recorded execution. Make them due
            // once so existing installations also run the tasks after upgrading.
            schedule.Reschedule(cronExpression, nowUtc);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return schedule.NextRunUtc;
    }

    public async Task RecordRunStartedAsync(
        string taskName,
        DateTime scheduledRunUtc,
        DateTime startedAtUtc,
        DateTime nextRunUtc,
        CancellationToken cancellationToken = default)
    {
        var schedule = await _dbContext.RecurringTaskSchedules
            .SingleOrDefaultAsync(
                item => item.TaskName == taskName,
                cancellationToken)
            ?? throw new InvalidOperationException(
                $"Recurring task schedule '{taskName}' was not found.");

        schedule.RecordRunStarted(
            scheduledRunUtc,
            startedAtUtc,
            nextRunUtc);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
