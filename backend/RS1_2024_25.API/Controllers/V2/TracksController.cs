using _808Music.Application.Tracks;
using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace RS1_2024_25.API.Controllers.V2;

[ApiController]
[ApiVersion("2.0")]
[Route("api/v{version:apiVersion}/tracks")]
[Produces("application/json")]
public sealed class TracksController : ControllerBase
{
    private readonly IExtractTrackFeaturesHandler _extractTrackFeaturesHandler;

    public TracksController(IExtractTrackFeaturesHandler extractTrackFeaturesHandler)
    {
        _extractTrackFeaturesHandler = extractTrackFeaturesHandler;
    }

    [HttpPost("{trackId:guid}/features/extract")]
    [MapToApiVersion("2.0")]
    public async Task<ActionResult<ExtractTrackFeaturesResult>> ExtractFeatures(
        Guid trackId,
        CancellationToken cancellationToken)
    {
        if (trackId == Guid.Empty)
        {
            return BadRequest("Track id is required.");
        }

        var command = new ExtractTrackFeaturesCommand(trackId, GetCurrentUserId());
        var result = await _extractTrackFeaturesHandler.Handle(command, cancellationToken);

        return Ok(result);
    }

    private string? GetCurrentUserId()
    {
        return User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
    }
}
