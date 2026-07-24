namespace _808Music.Application.Common.Scheduling;

public interface IRecurringApplicationTask
{
    string Name { get; }
    string CronExpression { get; }
    bool IsEnabled { get; }

    Task ExecuteAsync(CancellationToken cancellationToken = default);
}
