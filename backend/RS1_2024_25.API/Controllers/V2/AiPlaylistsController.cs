using _808Music.Application.Ai;
using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using RS1_2024_25.API.Controllers.V2.Requests;
using System.Security.Claims;

namespace RS1_2024_25.API.Controllers.V2;

[ApiController]
[ApiVersion("2.0")]
[Route("api/v{version:apiVersion}/ai/playlists")]
[Produces("application/json")]
public sealed class AiPlaylistsController : ControllerBase
{
    private const int DefaultTrackCount = 20;
    private const int MaximumTrackCount = 100;

    private readonly IGenerateAiPlaylistHandler _generateAiPlaylistHandler;

    public AiPlaylistsController(IGenerateAiPlaylistHandler generateAiPlaylistHandler)
    {
        _generateAiPlaylistHandler = generateAiPlaylistHandler;
    }

    [HttpPost("generate")]
    [MapToApiVersion("2.0")]
    public async Task<ActionResult<GenerateAiPlaylistResult>> Generate(
        [FromBody] GeneratePlaylistRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Prompt))
        {
            return BadRequest("Prompt is required.");
        }

        var trackCount = request.TrackCount ?? DefaultTrackCount;
        if (trackCount is < 1 or > MaximumTrackCount)
        {
            return BadRequest($"Track count must be between 1 and {MaximumTrackCount}.");
        }

        var command = new GenerateAiPlaylistCommand(
            request.Prompt,
            trackCount,
            request.SeedTrackIds,
            request.Genres,
            GetCurrentUserId());

        var result = await _generateAiPlaylistHandler.Handle(command, cancellationToken);

        return Ok(result);
    }

    private string? GetCurrentUserId()
    {
        return User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
    }
}
