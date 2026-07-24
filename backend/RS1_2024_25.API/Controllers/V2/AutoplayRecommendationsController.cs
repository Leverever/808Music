using _808Music.Application.Recommendations;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RS1_2024_25.API.Controllers.V2.Requests;
using System.Security.Claims;

namespace RS1_2024_25.API.Controllers.V2;

[ApiController]
[Authorize]
[ApiVersion("2.0")]
[Route("api/v{version:apiVersion}/recommendations/autoplay")]
[Produces("application/json")]
public sealed class AutoplayRecommendationsController : ControllerBase
{
    private readonly IGetAutoplayRecommendationsHandler _getAutoplayRecommendationsHandler;

    public AutoplayRecommendationsController(
        IGetAutoplayRecommendationsHandler getAutoplayRecommendationsHandler)
    {
        _getAutoplayRecommendationsHandler = getAutoplayRecommendationsHandler;
    }

    [HttpPost]
    [MapToApiVersion("2.0")]
    public async Task<ActionResult<GetAutoplayRecommendationsResult>> Get(
        [FromBody] AutoplayRecommendationsRequest? request,
        CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        if (request is null)
        {
            return BadRequest("Request body is required.");
        }

        if (request.SeedTrackIds.Count == 0)
        {
            return BadRequest("At least one seed track is required.");
        }

        if (request.SeedTrackIds.Count > 10)
        {
            return BadRequest("Seed track count cannot be greater than 10.");
        }

        if (request.SeedTrackIds.Any(trackId => trackId <= 0))
        {
            return BadRequest("Seed track ids must be positive.");
        }

        if (request.ExcludedTrackIds.Any(trackId => trackId <= 0))
        {
            return BadRequest("Excluded track ids must be positive.");
        }

        if (request.Limit is <= 0)
        {
            return BadRequest("Limit must be positive.");
        }

        try
        {
            var result = await _getAutoplayRecommendationsHandler.Handle(
                new GetAutoplayRecommendationsQuery(
                    userId.Value,
                    request.SeedTrackIds,
                    request.ExcludedTrackIds,
                    request.Limit ?? 25),
                cancellationToken);

            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    private int? GetCurrentUserId()
    {
        var userId = User.FindFirstValue("sub") ??
            User.FindFirstValue(ClaimTypes.NameIdentifier) ??
            User.FindFirstValue("userId");

        return int.TryParse(userId, out var parsedUserId)
            ? parsedUserId
            : null;
    }
}
