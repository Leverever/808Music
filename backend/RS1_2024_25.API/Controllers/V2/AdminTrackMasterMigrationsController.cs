using _808Music.Application.Tracks;
using _808Music.Domain.Enums;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using RS1_2024_25.API.Controllers.V2.Requests;
using RS1_2024_25.API.Services;

namespace RS1_2024_25.API.Controllers.V2;

[ApiController]
[Authorize]
[ApiVersion("2.0")]
[Route("api/v{version:apiVersion}/admin/track-master-migrations")]
[Produces("application/json")]
public sealed class AdminTrackMasterMigrationsController : ControllerBase
{
    private const string ExecutionKey = "legacy-track-master-migration";
    private readonly ILegacyTrackMasterMigrationService _migrationService;
    private readonly TokenProvider _tokenProvider;
    private readonly RecurringTaskExecutionCoordinator _coordinator;

    public AdminTrackMasterMigrationsController(
        ILegacyTrackMasterMigrationService migrationService,
        RecurringTaskExecutionCoordinator coordinator,
        TokenProvider tokenProvider)
    {
        _migrationService = migrationService;
        _coordinator = coordinator;
        _tokenProvider = tokenProvider;
    }

    [HttpPost("discover")]
    [MapToApiVersion("2.0")]
    public async Task<ActionResult<LegacyTrackMigrationDiscoveryResult>> Discover(
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] DiscoverTrackMasterMigrationsRequest? request,
        CancellationToken cancellationToken)
    {
        if (!IsAdmin())
        {
            return Forbid();
        }

        var dryRun = request?.DryRun ?? true;
        if (!dryRun && !_coordinator.TryBegin(ExecutionKey))
        {
            return Conflict("A track master migration operation is already running.");
        }

        try
        {
            var result = await _migrationService.DiscoverAsync(dryRun, cancellationToken);
            return Ok(result);
        }
        finally
        {
            if (!dryRun)
            {
                _coordinator.End(ExecutionKey);
            }
        }
    }

    [HttpPost("run")]
    [MapToApiVersion("2.0")]
    public async Task<ActionResult<LegacyTrackMigrationRunResult>> Run(
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] RunTrackMasterMigrationsRequest? request,
        CancellationToken cancellationToken)
    {
        if (!IsAdmin())
        {
            return Forbid();
        }

        request ??= new RunTrackMasterMigrationsRequest();
        if (!_coordinator.TryBegin(ExecutionKey))
        {
            return Conflict("A track master migration operation is already running.");
        }

        try
        {
            var result = await _migrationService.RunBatchAsync(
                request.BatchSize,
                request.QueueAnalysis,
                request.QueueStems,
                cancellationToken);
            return Ok(result);
        }
        finally
        {
            _coordinator.End(ExecutionKey);
        }
    }

    [HttpPost("{trackId:int}/retry")]
    [MapToApiVersion("2.0")]
    public async Task<ActionResult<LegacyTrackMigrationRunResult>> Retry(
        int trackId,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] RetryTrackMasterMigrationRequest? request,
        CancellationToken cancellationToken)
    {
        if (!IsAdmin())
        {
            return Forbid();
        }

        request ??= new RetryTrackMasterMigrationRequest();
        if (!_coordinator.TryBegin(ExecutionKey))
        {
            return Conflict("A track master migration operation is already running.");
        }

        try
        {
            var result = await _migrationService.RetryAsync(
                trackId,
                request.QueueAnalysis,
                request.QueueStems,
                cancellationToken);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        finally
        {
            _coordinator.End(ExecutionKey);
        }
    }

    [HttpGet]
    [MapToApiVersion("2.0")]
    public async Task<ActionResult<IReadOnlyList<LegacyTrackMigrationStatusItem>>> GetStatus(
        [FromQuery] TrackMasterMigrationStatus? status = null,
        [FromQuery] int take = 100,
        CancellationToken cancellationToken = default)
    {
        if (!IsAdmin())
        {
            return Forbid();
        }

        var result = await _migrationService.GetStatusAsync(status, take, cancellationToken);
        return Ok(result);
    }

    [HttpPost("cleanup")]
    [MapToApiVersion("2.0")]
    public async Task<ActionResult<LegacyTrackMigrationCleanupResult>> Cleanup(
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] CleanupTrackMasterMigrationsRequest? request,
        CancellationToken cancellationToken)
    {
        if (!IsAdmin())
        {
            return Forbid();
        }

        request ??= new CleanupTrackMasterMigrationsRequest();
        if (!_coordinator.TryBegin(ExecutionKey))
        {
            return Conflict("A track master migration operation is already running.");
        }

        try
        {
            var result = await _migrationService.CleanupAsync(
                request.RetentionDays,
                request.BatchSize,
                cancellationToken);
            return Ok(result);
        }
        finally
        {
            _coordinator.End(ExecutionKey);
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
}
