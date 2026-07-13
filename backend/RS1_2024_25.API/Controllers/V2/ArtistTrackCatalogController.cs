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
[Route("api/v{version:apiVersion}/artists")]
[Produces("application/json")]
public sealed class ArtistTrackCatalogController : ControllerBase
{
    private static readonly string[] MemberRoles =
        ["Owner", "General Manager", "Streaming Manager", "Shop Manager", "Viewer"];

    private readonly ITrackCatalogHandler _catalogHandler;
    private readonly ITrackCatalogSearchHandler _searchHandler;
    private readonly TokenProvider _tokenProvider;

    public ArtistTrackCatalogController(
        ITrackCatalogHandler catalogHandler,
        ITrackCatalogSearchHandler searchHandler,
        TokenProvider tokenProvider)
    {
        _catalogHandler = catalogHandler;
        _searchHandler = searchHandler;
        _tokenProvider = tokenProvider;
    }

    [HttpGet("{artistId:int}/tracks")]
    [MapToApiVersion("2.0")]
    public async Task<ActionResult<PagedResult<TrackCatalogItemResponse>>> ListTracks(
        int artistId,
        [FromQuery] ListArtistTracksRequest request,
        CancellationToken cancellationToken)
    {
        if (!await _searchHandler.ArtistExists(artistId, cancellationToken))
        {
            return NotFound();
        }

        if (!CanViewArtist(artistId))
        {
            return Forbid();
        }

        var result = await _catalogHandler.List(
            new TrackCatalogQuery(
                artistId,
                request.PageNumber,
                request.PageSize,
                request.Title,
                request.PrimaryReleaseTitle,
                request.MinStreams,
                request.MaxStreams,
                request.MinDurationSeconds,
                request.MaxDurationSeconds,
                request.SortBy,
                request.SortDirection),
            cancellationToken);

        return Ok(result);
    }

    [HttpGet("search")]
    [MapToApiVersion("2.0")]
    public async Task<ActionResult<IReadOnlyList<ArtistSearchResponse>>> Search(
        [FromQuery] SearchArtistsRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _searchHandler.SearchArtists(
            request.Query,
            request.ExcludeArtistId,
            request.Limit,
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
