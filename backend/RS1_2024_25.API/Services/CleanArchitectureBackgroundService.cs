using _808Music.Application.Common.Scheduling;
using Microsoft.Extensions.Options;
using NCrontab;

namespace RS1_2024_25.API.Services;

public sealed class CleanArchitectureBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly CleanArchitectureBackgroundServiceOptions _options;
    private readonly ILogger<CleanArchitectureBackgroundService> _logger;
    private readonly RecurringTaskExecutionCoordinator _executionCoordinator;
    private readonly Dictionary<string, DateTime> _nextRuns = new(StringComparer.OrdinalIgnoreCase);

    public CleanArchitectureBackgroundService(
        IServiceScopeFactory scopeFactory,
        IOptions<CleanArchitectureBackgroundServiceOptions> options,
        RecurringTaskExecutionCoordinator executionCoordinator,
        ILogger<CleanArchitectureBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _executionCoordinator = executionCoordinator;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Clean background task scheduler is disabled.");
            return;
        }

        var pollInterval = TimeSpan.FromSeconds(Math.Max(5, _options.PollIntervalSeconds));
        using var timer = new PeriodicTimer(pollInterval);

        _logger.LogInformation(
            "Clean background task scheduler started with poll interval {PollInterval}",
            pollInterval);

        await RunDueTasks(stoppingToken);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RunDueTasks(stoppingToken);
        }
    }

    private async Task RunDueTasks(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var tasks = scope.ServiceProvider
            .GetServices<IRecurringApplicationTask>()
            .Where(task => task.IsEnabled)
            .ToList();

        foreach (var task in tasks)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var startedTask = false;
            try
            {
                var now = DateTime.UtcNow;
                if (!_nextRuns.TryGetValue(task.Name, out var nextRun))
                {
                    nextRun = GetNextRun(task, now);
                    _nextRuns[task.Name] = nextRun;

                    _logger.LogInformation(
                        "Scheduled clean background task {TaskName} for {NextRun:u}",
                        task.Name,
                        nextRun);
                }

                if (nextRun > now || !_executionCoordinator.TryBegin(task.Name))
                {
                    continue;
                }

                startedTask = true;
                _logger.LogInformation("Running clean background task {TaskName}", task.Name);
                await task.ExecuteAsync(cancellationToken);
                _logger.LogInformation("Completed clean background task {TaskName}", task.Name);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Clean background task {TaskName} failed", task.Name);
            }
            finally
            {
                if (startedTask)
                {
                    _executionCoordinator.End(task.Name);
                    TryScheduleNextRun(task);
                }
            }
        }
    }

    private void TryScheduleNextRun(IRecurringApplicationTask task)
    {
        try
        {
            _nextRuns[task.Name] = GetNextRun(task, DateTime.UtcNow);
        }
        catch (Exception ex)
        {
            _nextRuns.Remove(task.Name);
            _logger.LogError(
                ex,
                "Failed to schedule next run for clean background task {TaskName}",
                task.Name);
        }
    }

    private static DateTime GetNextRun(IRecurringApplicationTask task, DateTime fromUtc)
    {
        var schedule = CrontabSchedule.Parse(task.CronExpression);
        return schedule.GetNextOccurrence(fromUtc);
    }
}
