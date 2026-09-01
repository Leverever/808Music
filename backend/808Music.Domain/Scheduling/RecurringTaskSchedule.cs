namespace _808Music.Domain.Scheduling;

public sealed class RecurringTaskSchedule
{
    private RecurringTaskSchedule()
    {
        TaskName = string.Empty;
        CronExpression = string.Empty;
    }

    public RecurringTaskSchedule(
        string taskName,
        string cronExpression,
        DateTime nextRunUtc)
    {
        TaskName = NormalizeRequired(taskName, 200, nameof(taskName));
        CronExpression = NormalizeRequired(cronExpression, 100, nameof(cronExpression));
        NextRunUtc = nextRunUtc;
    }

    public string TaskName { get; private set; }
    public string CronExpression { get; private set; }
    public DateTime NextRunUtc { get; private set; }
    public DateTime? LastScheduledRunUtc { get; private set; }
    public DateTime? LastStartedAtUtc { get; private set; }

    public void Reschedule(string cronExpression, DateTime nextRunUtc)
    {
        CronExpression = NormalizeRequired(
            cronExpression,
            100,
            nameof(cronExpression));
        NextRunUtc = nextRunUtc;
    }

    public void RecordRunStarted(
        DateTime scheduledRunUtc,
        DateTime startedAtUtc,
        DateTime nextRunUtc)
    {
        LastScheduledRunUtc = scheduledRunUtc;
        LastStartedAtUtc = startedAtUtc;
        NextRunUtc = nextRunUtc;
    }

    private static string NormalizeRequired(
        string value,
        int maxLength,
        string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A value is required.", parameterName);
        }

        var normalized = value.Trim();
        return normalized.Length <= maxLength
            ? normalized
            : normalized[..maxLength];
    }
}
