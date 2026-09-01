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

        await RunDueTasksAsync(DateTime.UtcNow, stoppingToken);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RunDueTasksAsync(DateTime.UtcNow, stoppingToken);
        }
    }

    public async Task RunDueTasksAsync(
        DateTime nowUtc,
        CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var scheduleStore = scope.ServiceProvider
            .GetRequiredService<IRecurringTaskScheduleStore>();
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
                var initialNextRun = GetNextRun(task, nowUtc);
                var nextRun = await scheduleStore.GetOrCreateNextRunAsync(
                    task.Name,
                    task.CronExpression,
                    nowUtc,
                    initialNextRun,
                    cancellationToken);

                if (nextRun > nowUtc || !_executionCoordinator.TryBegin(task.Name))
                {
                    continue;
                }

                startedTask = true;
                var followingRun = GetNextRun(task, nowUtc);
                await scheduleStore.RecordRunStartedAsync(
                    task.Name,
                    nextRun,
                    nowUtc,
                    followingRun,
                    cancellationToken);

                _logger.LogInformation(
                    "Running clean background task {TaskName} scheduled for {ScheduledRun:u}; next run is {NextRun:u}",
                    task.Name,
                    nextRun,
                    followingRun);
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
                }
            }
        }
    }

    private static DateTime GetNextRun(IRecurringApplicationTask task, DateTime fromUtc)
    {
        var schedule = CrontabSchedule.Parse(task.CronExpression);
        return schedule.GetNextOccurrence(fromUtc);
    }
}
