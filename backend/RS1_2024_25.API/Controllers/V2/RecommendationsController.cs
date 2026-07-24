using _808Music.Application.Recommendations;
using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace RS1_2024_25.API.Controllers.V2;

[ApiController]
[ApiVersion("2.0")]
[Route("api/v{version:apiVersion}/tracks/{trackId:guid}/recommendations")]
[Produces("application/json")]
public sealed class RecommendationsController : ControllerBase
{
    private readonly IGetTrackRecommendationsHandler _getTrackRecommendationsHandler;

    public RecommendationsController(IGetTrackRecommendationsHandler getTrackRecommendationsHandler)
    {
        _getTrackRecommendationsHandler = getTrackRecommendationsHandler;
    }

    [HttpGet]
    [MapToApiVersion("2.0")]
    public async Task<ActionResult<GetTrackRecommendationsResult>> GetForTrack(
        Guid trackId,
        CancellationToken cancellationToken)
    {
        if (trackId == Guid.Empty)
        {
            return BadRequest("Track id is required.");
        }

        var query = new GetTrackRecommendationsQuery(trackId, GetCurrentUserId());
        var result = await _getTrackRecommendationsHandler.Handle(query, cancellationToken);

        return Ok(result);
    }

    private string? GetCurrentUserId()
    {
        return User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
    }
}
