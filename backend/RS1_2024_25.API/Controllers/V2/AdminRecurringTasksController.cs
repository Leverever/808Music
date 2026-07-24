using _808Music.Application.Common.Scheduling;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NCrontab;
using RS1_2024_25.API.Services;

namespace RS1_2024_25.API.Controllers.V2;

[ApiController]
[Authorize]
[ApiVersion("2.0")]
[Route("api/v{version:apiVersion}/admin/recurring-tasks")]
[Produces("application/json")]
public sealed class AdminRecurringTasksController : ControllerBase
{
    private readonly IReadOnlyList<IRecurringApplicationTask> _tasks;
    private readonly RecurringTaskExecutionCoordinator _coordinator;
    private readonly TokenProvider _tokenProvider;

    public AdminRecurringTasksController(
        IEnumerable<IRecurringApplicationTask> tasks,
        RecurringTaskExecutionCoordinator coordinator,
        TokenProvider tokenProvider)
    {
        _tasks = tasks.OrderBy(x => x.Name).ToArray();
        _coordinator = coordinator;
        _tokenProvider = tokenProvider;
    }

    [HttpGet]
    [MapToApiVersion("2.0")]
    public ActionResult<IReadOnlyList<RecurringTaskAdminItem>> List()
    {
        if (!IsAdmin())
        {
            return Forbid();
        }

        var now = DateTime.UtcNow;
        return Ok(_tasks.Select(task => new RecurringTaskAdminItem(
            task.Name,
            task.CronExpression,
            task.IsEnabled,
            _coordinator.IsRunning(task.Name),
            GetNextScheduledRunUtc(task, now))).ToArray());
    }

    [HttpPost("{taskName}/run")]
    [MapToApiVersion("2.0")]
    public async Task<ActionResult<RecurringTaskManualRunResult>> Run(
        string taskName,
        CancellationToken cancellationToken)
    {
        if (!IsAdmin())
        {
            return Forbid();
        }

        var task = _tasks.FirstOrDefault(x =>
            x.Name.Equals(taskName, StringComparison.OrdinalIgnoreCase));
        if (task is null)
        {
            return NotFound("Recurring task was not found.");
        }

        if (!_coordinator.TryBegin(task.Name))
        {
            return Conflict($"Recurring task '{task.Name}' is already running.");
        }

        var startedAt = DateTime.UtcNow;
        try
        {
            await task.ExecuteAsync(cancellationToken);
            return Ok(new RecurringTaskManualRunResult(
                task.Name,
                startedAt,
                DateTime.UtcNow,
                "Completed"));
        }
        finally
        {
            _coordinator.End(task.Name);
        }
    }

    private bool IsAdmin()
    {
        return User.IsInRole("Admin") ||
            string.Equals(
                _tokenProvider.GetJwtRoleClaimValue(Request),
                "Admin",
                StringComparison.OrdinalIgnoreCase);
    }

    private static DateTime? GetNextScheduledRunUtc(
        IRecurringApplicationTask task,
        DateTime fromUtc)
    {
        if (!task.IsEnabled)
        {
            return null;
        }

        try
        {
            return CrontabSchedule
                .Parse(task.CronExpression)
                .GetNextOccurrence(fromUtc);
        }
        catch (CrontabException)
        {
            return null;
        }
    }
}

public sealed record RecurringTaskAdminItem(
    string Name,
    string CronExpression,
    bool IsScheduled,
    bool IsRunning,
    DateTime? NextScheduledRunUtc);

public sealed record RecurringTaskManualRunResult(
    string Name,
    DateTime StartedAt,
    DateTime CompletedAt,
    string Status);
