using _808Music.Application;
using _808Music.Application.Common.Scheduling;
using _808Music.Domain.Scheduling;
using _808Music.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RS1_2024_25.API.Services;

namespace RS1_2024_25.Tests.Application;

public sealed class RecurringTaskSchedulingTests
{
    [Fact]
    public async Task Never_run_task_is_executed_once_on_backend_startup()
    {
        var databaseRoot = new InMemoryDatabaseRoot();
        var task = new RecordingRecurringTask("0 2 * * *");
        var initialStartup = new DateTime(2026, 8, 12, 1, 0, 0, DateTimeKind.Utc);

        await using (var provider = CreateProvider(databaseRoot, task))
        {
            var scheduler = CreateScheduler(provider);

            await scheduler.RunDueTasksAsync(initialStartup);
            await scheduler.RunDueTasksAsync(initialStartup.AddMinutes(1));

            await using var scope = provider.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<MusicDbContext>();
            var schedule = await dbContext.RecurringTaskSchedules.SingleAsync();

            Assert.Equal(initialStartup, schedule.LastScheduledRunUtc);
            Assert.Equal(initialStartup, schedule.LastStartedAtUtc);
            Assert.Equal(
                new DateTime(2026, 8, 12, 2, 0, 0, DateTimeKind.Utc),
                schedule.NextRunUtc);
        }

        Assert.Equal(1, task.ExecutionCount);
    }

    [Fact]
    public async Task Existing_never_run_schedule_is_made_due_on_backend_startup()
    {
        var databaseRoot = new InMemoryDatabaseRoot();
        var task = new RecordingRecurringTask("0 2 * * *");
        var restartedAt = new DateTime(2026, 8, 13, 1, 0, 0, DateTimeKind.Utc);

        await using (var provider = CreateProvider(databaseRoot, task))
        {
            await using var scope = provider.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<MusicDbContext>();
            dbContext.RecurringTaskSchedules.Add(new RecurringTaskSchedule(
                task.Name,
                task.CronExpression,
                restartedAt.AddHours(1)));
            await dbContext.SaveChangesAsync();
        }

        await using (var provider = CreateProvider(databaseRoot, task))
        {
            await CreateScheduler(provider).RunDueTasksAsync(restartedAt);

            await using var scope = provider.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<MusicDbContext>();
            var schedule = await dbContext.RecurringTaskSchedules.SingleAsync();

            Assert.Equal(restartedAt, schedule.LastScheduledRunUtc);
            Assert.Equal(restartedAt, schedule.LastStartedAtUtc);
            Assert.Equal(restartedAt.AddHours(1), schedule.NextRunUtc);
        }

        Assert.Equal(1, task.ExecutionCount);
    }

    [Fact]
    public async Task Missed_run_is_executed_once_when_backend_starts_again()
    {
        var databaseRoot = new InMemoryDatabaseRoot();
        var task = new RecordingRecurringTask("0 2 * * *");
        var initialStartup = new DateTime(2026, 8, 12, 1, 0, 0, DateTimeKind.Utc);

        await using (var provider = CreateProvider(databaseRoot, task))
        {
            await CreateScheduler(provider).RunDueTasksAsync(initialStartup);
        }

        var restartedAt = new DateTime(2026, 8, 13, 1, 0, 0, DateTimeKind.Utc);
        await using (var provider = CreateProvider(databaseRoot, task))
        {
            var scheduler = CreateScheduler(provider);
            await scheduler.RunDueTasksAsync(restartedAt);
            await scheduler.RunDueTasksAsync(restartedAt.AddMinutes(1));
        }

        Assert.Equal(2, task.ExecutionCount);
    }

    [Fact]
    public async Task Several_missed_occurrences_are_collapsed_into_one_catch_up_run()
    {
        var databaseRoot = new InMemoryDatabaseRoot();
        var task = new RecordingRecurringTask("0 2 * * *");
        var initialStartup = new DateTime(2026, 8, 10, 1, 0, 0, DateTimeKind.Utc);

        await using (var provider = CreateProvider(databaseRoot, task))
        {
            await CreateScheduler(provider).RunDueTasksAsync(initialStartup);
        }

        await using (var provider = CreateProvider(databaseRoot, task))
        {
            var restartedAt = new DateTime(2026, 8, 13, 1, 0, 0, DateTimeKind.Utc);
            await CreateScheduler(provider).RunDueTasksAsync(restartedAt);
        }

        Assert.Equal(2, task.ExecutionCount);
    }

    private static ServiceProvider CreateProvider(
        InMemoryDatabaseRoot databaseRoot,
        IRecurringApplicationTask task)
    {
        var services = new ServiceCollection();
        services.AddDbContext<MusicDbContext>(options =>
            options.UseInMemoryDatabase("recurring-task-tests", databaseRoot));
        services.AddScoped<IApplicationDbContext>(provider =>
            provider.GetRequiredService<MusicDbContext>());
        services.AddScoped<IRecurringTaskScheduleStore, RecurringTaskScheduleStore>();
        services.AddSingleton(task);

        return services.BuildServiceProvider();
    }

    private static CleanArchitectureBackgroundService CreateScheduler(
        IServiceProvider provider)
    {
        return new CleanArchitectureBackgroundService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new CleanArchitectureBackgroundServiceOptions()),
            new RecurringTaskExecutionCoordinator(),
            NullLogger<CleanArchitectureBackgroundService>.Instance);
    }

    private sealed class RecordingRecurringTask : IRecurringApplicationTask
    {
        public RecordingRecurringTask(string cronExpression)
        {
            CronExpression = cronExpression;
        }

        public string Name => "test-daily-task";
        public string CronExpression { get; }
        public bool IsEnabled => true;
        public int ExecutionCount { get; private set; }

        public Task ExecuteAsync(CancellationToken cancellationToken = default)
        {
            ExecutionCount++;
            return Task.CompletedTask;
        }
    }
}
