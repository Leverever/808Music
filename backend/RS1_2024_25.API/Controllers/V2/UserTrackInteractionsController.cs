using _808Music.Application.Personalization;
using _808Music.Domain.Enums;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RS1_2024_25.API.Controllers.V2.Requests;
using System.Security.Claims;

namespace RS1_2024_25.API.Controllers.V2;

[ApiController]
[Authorize]
[ApiVersion("2.0")]
[Route("api/v{version:apiVersion}/me/track-interactions")]
[Produces("application/json")]
public sealed class UserTrackInteractionsController : ControllerBase
{
    private readonly IRecordUserTrackInteractionHandler _recordHandler;

    public UserTrackInteractionsController(IRecordUserTrackInteractionHandler recordHandler)
    {
        _recordHandler = recordHandler;
    }

    [HttpPost]
    [MapToApiVersion("2.0")]
    public async Task<ActionResult<RecordUserTrackInteractionResult>> Record(
        [FromBody] RecordUserTrackInteractionRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        if (request.TrackId <= 0)
        {
            return BadRequest("Track id is required.");
        }

        if (!Enum.TryParse<UserTrackInteractionType>(
                request.InteractionType,
                ignoreCase: true,
                out var interactionType))
        {
            return BadRequest("Interaction type is invalid.");
        }

        if (request.PlayedMs is < 0)
        {
            return BadRequest("Played milliseconds cannot be negative.");
        }

        if (request.TrackDurationMs is < 0)
        {
            return BadRequest("Track duration milliseconds cannot be negative.");
        }

        if (request.ContextType?.Length > 50)
        {
            return BadRequest("Context type cannot be longer than 50 characters.");
        }

        if (request.ClientEventId?.Length > 100)
        {
            return BadRequest("Client event id cannot be longer than 100 characters.");
        }

        try
        {
            var result = await _recordHandler.Handle(
                new RecordUserTrackInteractionCommand(
                    userId.Value,
                    request.TrackId,
                    interactionType,
                    request.PlayedMs,
                    request.TrackDurationMs,
                    request.ContextType,
                    request.ClientEventId,
                    request.OccurredAt),
                cancellationToken);

            return result.Created
                ? StatusCode(StatusCodes.Status201Created, result)
                : Ok(result);
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
