using _808Music.Application.Recommendations;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace RS1_2024_25.API.Controllers.V2;

[ApiController]
[Authorize]
[ApiVersion("2.0")]
[Route("api/v{version:apiVersion}/tracks/{trackId:int}/radio")]
[Produces("application/json")]
public sealed class TrackRadioController : ControllerBase
{
    private readonly IGetTrackRadioHandler _getTrackRadioHandler;

    public TrackRadioController(IGetTrackRadioHandler getTrackRadioHandler)
    {
        _getTrackRadioHandler = getTrackRadioHandler;
    }

    [HttpGet]
    [MapToApiVersion("2.0")]
    public async Task<ActionResult<GetTrackRadioResult>> Get(
        int trackId,
        [FromQuery] int limit = 50,
        CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        if (trackId <= 0)
        {
            return BadRequest("Track id is required.");
        }

        if (limit <= 0)
        {
            return BadRequest("Limit must be positive.");
        }

        try
        {
            var result = await _getTrackRadioHandler.Handle(
                new GetTrackRadioQuery(userId.Value, trackId, limit),
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
