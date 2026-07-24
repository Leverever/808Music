using _808Music.Application.AutomaticPlaylists;
using _808Music.Application.PersonalizedPlaylists;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using RS1_2024_25.API.Controllers.V2.Requests;
using RS1_2024_25.API.Services;
using System.Security.Claims;

namespace RS1_2024_25.API.Controllers.V2;

[ApiController]
[Authorize]
[ApiVersion("2.0")]
[Route("api/v{version:apiVersion}/personalized-playlists")]
[Produces("application/json")]
public sealed class PersonalizedPlaylistsController : ControllerBase
{
    private readonly IGenerateDailyAutomaticPlaylistsHandler _generateDailyAutomaticPlaylistsHandler;
    private readonly IGetDailyPersonalizedPlaylistsHandler _getDailyPersonalizedPlaylistsHandler;
    private readonly IGetPersonalizedPlaylistHandler _getPersonalizedPlaylistHandler;
    private readonly TokenProvider _tokenProvider;

    public PersonalizedPlaylistsController(
        IGenerateDailyAutomaticPlaylistsHandler generateDailyAutomaticPlaylistsHandler,
        IGetDailyPersonalizedPlaylistsHandler getDailyPersonalizedPlaylistsHandler,
        IGetPersonalizedPlaylistHandler getPersonalizedPlaylistHandler,
        TokenProvider tokenProvider)
    {
        _generateDailyAutomaticPlaylistsHandler = generateDailyAutomaticPlaylistsHandler;
        _getDailyPersonalizedPlaylistsHandler = getDailyPersonalizedPlaylistsHandler;
        _getPersonalizedPlaylistHandler = getPersonalizedPlaylistHandler;
        _tokenProvider = tokenProvider;
    }

    [HttpPost("daily/generate")]
    [MapToApiVersion("2.0")]
    public async Task<ActionResult<GenerateDailyAutomaticPlaylistsResult>> GenerateDaily(
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] GenerateDailyPersonalizedPlaylistsRequest? request,
        CancellationToken cancellationToken = default)
    {
        if (!IsAdmin())
        {
            return Forbid();
        }

        var playlistDate = request?.PlaylistDate ?? DateOnly.FromDateTime(DateTime.UtcNow);

        var result = await _generateDailyAutomaticPlaylistsHandler.Handle(
            new GenerateDailyAutomaticPlaylistsCommand(playlistDate),
            cancellationToken);

        return Ok(result);
    }

    [HttpGet("daily")]
    [MapToApiVersion("2.0")]
    public async Task<ActionResult<GetDailyPersonalizedPlaylistsResult>> GetDaily(
        [FromQuery] DateOnly? date = null,
        CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        try
        {
            var result = await _getDailyPersonalizedPlaylistsHandler.Handle(
                new GetDailyPersonalizedPlaylistsQuery(
                    userId.Value,
                    date ?? DateOnly.FromDateTime(DateTime.UtcNow)),
                cancellationToken);

            return Ok(result);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpGet("{id:guid}")]
    [MapToApiVersion("2.0")]
    public async Task<ActionResult<GetPersonalizedPlaylistResult>> GetById(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        if (id == Guid.Empty)
        {
            return BadRequest("Playlist id is required.");
        }

        try
        {
            var result = await _getPersonalizedPlaylistHandler.Handle(
                new GetPersonalizedPlaylistQuery(userId.Value, id),
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

    private bool IsAdmin()
    {
        return User.IsInRole("Admin") ||
            string.Equals(_tokenProvider.GetJwtRoleClaimValue(Request), "Admin", StringComparison.OrdinalIgnoreCase);
    }
}
