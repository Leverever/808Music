using _808Music.Application.Common.Search;
using _808Music.Application.Tracks;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RS1_2024_25.API.Controllers.V2.Requests;
using RS1_2024_25.API.Services;

namespace RS1_2024_25.API.Controllers.V2;

[ApiController]
[Authorize]
[ApiVersion("2.0")]
[Route("api/v{version:apiVersion}/releases")]
[Produces("application/json")]
public sealed class ReleasesController : ControllerBase
{
    private static readonly string[] MemberRoles =
        ["Owner", "General Manager", "Streaming Manager", "Shop Manager", "Viewer"];

    private readonly ITrackCatalogSearchHandler _searchHandler;
    private readonly TokenProvider _tokenProvider;

    public ReleasesController(
        ITrackCatalogSearchHandler searchHandler,
        TokenProvider tokenProvider)
    {
        _searchHandler = searchHandler;
        _tokenProvider = tokenProvider;
    }

    [HttpGet]
    [MapToApiVersion("2.0")]
    public async Task<ActionResult<PagedResult<ReleaseSearchResponse>>> Search(
        [FromQuery] SearchReleasesRequest request,
        CancellationToken cancellationToken)
    {
        if (!await _searchHandler.ArtistExists(request.ArtistId, cancellationToken))
        {
            return NotFound();
        }

        if (!CanViewArtist(request.ArtistId))
        {
            return Forbid();
        }

        var result = await _searchHandler.SearchReleases(
            new ReleaseSearchQuery(
                request.ArtistId,
                request.ExcludeTrackId,
                request.PageNumber,
                request.PageSize,
                request.Title),
            cancellationToken);

        return Ok(result);
    }

    private bool CanViewArtist(int artistId) =>
        IsAdmin() || _tokenProvider.AuthorizeUserArtist(Request, artistId, MemberRoles);

    private bool IsAdmin() =>
        User.IsInRole("Admin") ||
        string.Equals(
            _tokenProvider.GetJwtRoleClaimValue(Request),
            "Admin",
            StringComparison.OrdinalIgnoreCase);
}
