using _808Music.Application.Releases;
using _808Music.Application.Common.Search;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RS1_2024_25.API.Controllers.V2.Requests;
using RS1_2024_25.API.Services;

namespace RS1_2024_25.API.Controllers.V2;

[ApiController]
[Authorize]
[ApiVersion("2.0")]
[Route("api/v{version:apiVersion}/releases/{releaseId:int}/tracks")]
[Produces("application/json")]
public sealed class ReleaseTracksController : ControllerBase
{
    private static readonly string[] ManagerRoles =
        ["Owner", "General Manager", "Streaming Manager"];

    private readonly IReleaseTrackHandler _handler;
    private readonly TokenProvider _tokenProvider;

    public ReleaseTracksController(
        IReleaseTrackHandler handler,
        TokenProvider tokenProvider)
    {
        _handler = handler;
        _tokenProvider = tokenProvider;
    }

    [HttpGet]
    [MapToApiVersion("2.0")]
    public async Task<ActionResult<PagedResult<ReleaseTrackResponse>>> List(
        int releaseId,
        [FromQuery] ListReleaseTracksRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _handler.List(
            new ReleaseTrackListQuery(
                releaseId,
                request.PageNumber,
                request.PageSize,
                request.Title),
            cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpGet("{trackId:int}")]
    [MapToApiVersion("2.0")]
    public async Task<ActionResult<ReleaseTrackResponse>> Get(
        int releaseId,
        int trackId,
        CancellationToken cancellationToken)
    {
        var result = await _handler.Get(releaseId, trackId, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost]
    [MapToApiVersion("2.0")]
    public async Task<ActionResult<ReleaseTrackResponse>> Create(
        int releaseId,
        [FromBody] CreateReleaseTrackRequest request,
        CancellationToken cancellationToken)
    {
        var accessResult = await EnsureCanManageRelease(releaseId, cancellationToken);
        if (accessResult is not null)
        {
            return accessResult;
        }

        try
        {
            var result = await _handler.Create(
                new CreateReleaseTrackCommand(
                    releaseId,
                    request.TrackId,
                    request.DiscNumber,
                    request.TrackNumber,
                    request.TitleOverride,
                    request.IsPrimaryRelease,
                    AllowArtistMismatch: IsAdmin()),
                cancellationToken);

            return CreatedAtAction(
                nameof(Get),
                new { version = "2.0", releaseId, trackId = result.TrackId },
                result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ex.Message);
        }
    }

    [HttpPut("{trackId:int}")]
    [MapToApiVersion("2.0")]
    public async Task<ActionResult<ReleaseTrackResponse>> Update(
        int releaseId,
        int trackId,
        [FromBody] UpdateReleaseTrackRequest request,
        CancellationToken cancellationToken)
    {
        var accessResult = await EnsureCanManageRelease(releaseId, cancellationToken);
        if (accessResult is not null)
        {
            return accessResult;
        }

        try
        {
            var result = await _handler.Update(
                new UpdateReleaseTrackCommand(
                    releaseId,
                    trackId,
                    request.DiscNumber,
                    request.TrackNumber,
                    request.TitleOverride,
                    request.IsPrimaryRelease,
                    AllowArtistMismatch: IsAdmin()),
                cancellationToken);

            return result is null ? NotFound() : Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ex.Message);
        }
    }

    [HttpPut("order")]
    [MapToApiVersion("2.0")]
    public async Task<IActionResult> Reorder(
        int releaseId,
        [FromBody] ReorderReleaseTracksRequest request,
        CancellationToken cancellationToken)
    {
        var accessResult = await EnsureCanManageRelease(releaseId, cancellationToken);
        if (accessResult is not null)
        {
            return accessResult;
        }

        try
        {
            var reordered = await _handler.Reorder(
                new ReorderReleaseTracksCommand(
                    releaseId,
                    request.Tracks
                        .Select(x => new ReleaseTrackPosition(
                            x.TrackId,
                            x.DiscNumber,
                            x.TrackNumber))
                        .ToList()),
                cancellationToken);

            return reordered ? NoContent() : NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ex.Message);
        }
    }

    [HttpDelete("{trackId:int}")]
    [MapToApiVersion("2.0")]
    public async Task<IActionResult> Delete(
        int releaseId,
        int trackId,
        CancellationToken cancellationToken)
    {
        var accessResult = await EnsureCanManageRelease(releaseId, cancellationToken);
        if (accessResult is not null)
        {
            return accessResult;
        }

        var deleted = await _handler.Delete(releaseId, trackId, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }

    private async Task<ActionResult?> EnsureCanManageRelease(
        int releaseId,
        CancellationToken cancellationToken)
    {
        var artistId = await _handler.GetReleaseArtistId(releaseId, cancellationToken);
        if (artistId is null)
        {
            return NotFound();
        }

        if (IsAdmin())
        {
            return null;
        }

        return _tokenProvider.AuthorizeUserArtist(Request, artistId.Value, ManagerRoles)
            ? null
            : Forbid();
    }

    private bool IsAdmin()
    {
        return User.IsInRole("Admin") ||
            string.Equals(
                _tokenProvider.GetJwtRoleClaimValue(Request),
                "Admin",
                StringComparison.OrdinalIgnoreCase);
    }
}
