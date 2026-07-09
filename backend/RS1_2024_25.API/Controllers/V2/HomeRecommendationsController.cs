using _808Music.Application.Recommendations;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace RS1_2024_25.API.Controllers.V2;

[ApiController]
[Authorize]
[ApiVersion("2.0")]
[Route("api/v{version:apiVersion}/recommendations/home")]
[Produces("application/json")]
public sealed class HomeRecommendationsController : ControllerBase
{
    private readonly IGetHomeRecommendationsHandler _getHomeRecommendationsHandler;

    public HomeRecommendationsController(IGetHomeRecommendationsHandler getHomeRecommendationsHandler)
    {
        _getHomeRecommendationsHandler = getHomeRecommendationsHandler;
    }

    [HttpGet]
    [MapToApiVersion("2.0")]
    public async Task<ActionResult<GetHomeRecommendationsResult>> Get(
        [FromQuery] DateOnly? date = null,
        [FromQuery] int dailyPlaylistLimit = 6,
        [FromQuery] int albumLimit = 10,
        [FromQuery] int artistLimit = 10,
        [FromQuery] int playlistLimit = 10,
        [FromQuery] int trackLimit = 20,
        CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        try
        {
            var result = await _getHomeRecommendationsHandler.Handle(
                new GetHomeRecommendationsQuery(
                    userId.Value,
                    date ?? DateOnly.FromDateTime(DateTime.UtcNow),
                    dailyPlaylistLimit,
                    albumLimit,
                    artistLimit,
                    playlistLimit,
                    trackLimit),
                cancellationToken);

            return Ok(result);
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
