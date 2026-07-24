using _808Music.Application.Playback;
using _808Music.Application.Tracks;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RS1_2024_25.API.Data;
using RS1_2024_25.API.Services;
using System.Security.Claims;

namespace RS1_2024_25.API.Controllers.V2;

[ApiController]
[Authorize]
[ApiVersion("2.0")]
[Route("api/v{version:apiVersion}/tracks/{trackId:int}/playback")]
[Produces("application/json")]
public sealed class PlaybackController : ControllerBase
{
    private readonly IGetTrackPlaybackManifestHandler _playbackManifestHandler;
    private readonly ITrackArtistAccessQuery _trackArtistAccessQuery;
    private readonly ApplicationDbContext _legacyDbContext;
    private readonly TokenProvider _tokenProvider;
    private readonly IConfiguration _configuration;

    public PlaybackController(
        IGetTrackPlaybackManifestHandler playbackManifestHandler,
        ITrackArtistAccessQuery trackArtistAccessQuery,
        ApplicationDbContext legacyDbContext,
        TokenProvider tokenProvider,
        IConfiguration configuration)
    {
        _playbackManifestHandler = playbackManifestHandler;
        _trackArtistAccessQuery = trackArtistAccessQuery;
        _legacyDbContext = legacyDbContext;
        _tokenProvider = tokenProvider;
        _configuration = configuration;
    }

    [HttpGet]
    [MapToApiVersion("2.0")]
    public async Task<ActionResult<GetTrackPlaybackManifestResult>> Get(
        int trackId,
        [FromQuery] bool artistMode,
        CancellationToken cancellationToken)
    {
        if (trackId <= 0)
        {
            return BadRequest("Track id is required.");
        }

        var accessResult = await EnsureCanStreamTrack(
            trackId,
            artistMode,
            cancellationToken);

        if (accessResult is not null)
        {
            return accessResult;
        }

        try
        {
            var result = await _playbackManifestHandler.Handle(
                new GetTrackPlaybackManifestQuery(
                    trackId,
                    GetSignedUrlLifetime()),
                cancellationToken);

            return result is null ? NotFound() : Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    private async Task<ActionResult?> EnsureCanStreamTrack(
        int trackId,
        bool artistMode,
        CancellationToken cancellationToken)
    {
        var leadArtistId = await _trackArtistAccessQuery.GetLeadArtistId(trackId, cancellationToken);
        if (leadArtistId is null)
        {
            return NotFound();
        }

        if (IsAdmin())
        {
            return null;
        }

        if (artistMode)
        {
            var canManageTrack = _tokenProvider.AuthorizeUserArtist(
                Request,
                leadArtistId.Value,
                ["Owner", "General Manager", "Streaming Manager", "Shop Manager", "Viewer"]);

            return canManageTrack ? null : Forbid();
        }

        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var hasActiveSubscription = await _legacyDbContext.MyAppUsers
            .AsNoTracking()
            .Where(user => user.ID == userId)
            .AnyAsync(
                user =>
                    user.Subscription != null &&
                    user.Subscription.IsActive &&
                    user.Subscription.EndDate >= DateTime.UtcNow,
                cancellationToken);

        if (!hasActiveSubscription)
        {
            return Unauthorized(new
            {
                message = "Your subscription has expired or is not active."
            });
        }

        return null;
    }

    private int? GetCurrentUserId()
    {
        var userId = User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(userId, out var parsedUserId)
            ? parsedUserId
            : null;
    }

    private bool IsAdmin()
    {
        return User.IsInRole("Admin") ||
            string.Equals(_tokenProvider.GetJwtRoleClaimValue(Request), "Admin", StringComparison.OrdinalIgnoreCase);
    }

    private TimeSpan GetSignedUrlLifetime()
    {
        var minutes = _configuration.GetValue<int?>("Playback:SignedUrlExpirationMinutes") ?? 120;
        minutes = Math.Clamp(minutes, 5, 240);

        return TimeSpan.FromMinutes(minutes);
    }
}
