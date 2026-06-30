using _808Music.Application.Stems;
using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace RS1_2024_25.API.Controllers.V2;

[ApiController]
[ApiVersion("2.0")]
[Route("api/v{version:apiVersion}/tracks/{trackId:guid}/stems")]
[Produces("application/json")]
public sealed class StemsController : ControllerBase
{
    private readonly ISeparateTrackStemsHandler _separateTrackStemsHandler;
    private readonly IGetTrackStemsHandler _getTrackStemsHandler;

    public StemsController(
        ISeparateTrackStemsHandler separateTrackStemsHandler,
        IGetTrackStemsHandler getTrackStemsHandler)
    {
        _separateTrackStemsHandler = separateTrackStemsHandler;
        _getTrackStemsHandler = getTrackStemsHandler;
    }

    [HttpPost("separate")]
    [MapToApiVersion("2.0")]
    public async Task<ActionResult<SeparateTrackStemsResult>> Separate(
        Guid trackId,
        CancellationToken cancellationToken)
    {
        if (trackId == Guid.Empty)
        {
            return BadRequest("Track id is required.");
        }

        var command = new SeparateTrackStemsCommand(trackId, GetCurrentUserId());
        var result = await _separateTrackStemsHandler.Handle(command, cancellationToken);

        return Accepted(result);
    }

    [HttpGet]
    [MapToApiVersion("2.0")]
    public async Task<ActionResult<GetTrackStemsResult>> GetManifest(
        Guid trackId,
        CancellationToken cancellationToken)
    {
        if (trackId == Guid.Empty)
        {
            return BadRequest("Track id is required.");
        }

        var query = new GetTrackStemsQuery(trackId, GetCurrentUserId());
        var result = await _getTrackStemsHandler.Handle(query, cancellationToken);

        return Ok(result);
    }

    private string? GetCurrentUserId()
    {
        return User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
    }
}
