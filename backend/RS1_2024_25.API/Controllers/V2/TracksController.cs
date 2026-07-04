using Microsoft.AspNetCore.Authorization;
using _808Music.Application.Tracks;
using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using RS1_2024_25.API.Controllers.V2.Requests;
using RS1_2024_25.API.Services;
using System.Security.Claims;

namespace RS1_2024_25.API.Controllers.V2;

[ApiController]
[ApiVersion("2.0")]
[Route("api/v{version:apiVersion}/tracks")]
[Produces("application/json")]
public sealed class TracksController : ControllerBase
{
    private readonly IExtractTrackFeaturesHandler _extractTrackFeaturesHandler;
    private readonly IUploadTrackHandler _uploadTrackHandler;
    private readonly IUpdateTrackMetadataHandler _updateTrackMetadataHandler;
    private readonly IReplaceTrackMasterHandler _replaceTrackMasterHandler;
    private readonly ITrackArtistAccessQuery _trackArtistAccessQuery;
    private readonly TokenProvider _tokenProvider;

    public TracksController(
        IExtractTrackFeaturesHandler extractTrackFeaturesHandler,
        IUploadTrackHandler uploadTrackHandler,
        IUpdateTrackMetadataHandler updateTrackMetadataHandler,
        IReplaceTrackMasterHandler replaceTrackMasterHandler,
        ITrackArtistAccessQuery trackArtistAccessQuery,
        TokenProvider tokenProvider)
    {
        _extractTrackFeaturesHandler = extractTrackFeaturesHandler;
        _uploadTrackHandler = uploadTrackHandler;
        _updateTrackMetadataHandler = updateTrackMetadataHandler;
        _replaceTrackMasterHandler = replaceTrackMasterHandler;
        _trackArtistAccessQuery = trackArtistAccessQuery;
        _tokenProvider = tokenProvider;
    }

    [Authorize]
    [HttpPost("upload")]
    [MapToApiVersion("2.0")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<UploadTrackResult>> Upload(
        [FromForm] UploadTrackRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
        {
            return BadRequest("Track title is required.");
        }

        if (request.MasterFile is null || request.MasterFile.Length == 0)
        {
            return BadRequest("Master track file is required.");
        }

        var isAdmin = User.IsInRole("Admin") ||
            string.Equals(_tokenProvider.GetJwtRoleClaimValue(Request), "Admin", StringComparison.OrdinalIgnoreCase);

        var canUploadForArtist = _tokenProvider.AuthorizeUserArtist(
            Request,
            request.ArtistId,
            ["Owner", "General Manager", "Streaming Manager"]);

        if (!isAdmin && !canUploadForArtist)
        {
            return Forbid();
        }

        await using var stream = request.MasterFile.OpenReadStream();

        try
        {
            var command = new UploadTrackCommand(
                request.ArtistId,
                request.Title,
                request.IsExplicit,
                request.MasterFile.FileName,
                request.MasterFile.ContentType,
                stream,
                GetCurrentUserId());

            var result = await _uploadTrackHandler.Handle(command, cancellationToken);

            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [Authorize]
    [HttpPut("{trackId:int}/metadata")]
    [MapToApiVersion("2.0")]
    public async Task<ActionResult<UpdateTrackMetadataResult>> UpdateMetadata(
        int trackId,
        [FromBody] UpdateTrackMetadataRequest request,
        CancellationToken cancellationToken)
    {
        if (trackId <= 0)
        {
            return BadRequest("Track id is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Title))
        {
            return BadRequest("Track title is required.");
        }

        var accessResult = await EnsureCanManageTrack(trackId, cancellationToken);
        if (accessResult is not null)
        {
            return accessResult;
        }

        try
        {
            var result = await _updateTrackMetadataHandler.Handle(
                new UpdateTrackMetadataCommand(
                    trackId,
                    request.Title,
                    request.IsExplicit),
                cancellationToken);

            return result is null ? NotFound() : Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [Authorize]
    [HttpPut("{trackId:int}/master")]
    [MapToApiVersion("2.0")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<ReplaceTrackMasterResult>> ReplaceMaster(
        int trackId,
        [FromForm] ReplaceTrackMasterRequest request,
        CancellationToken cancellationToken)
    {
        if (trackId <= 0)
        {
            return BadRequest("Track id is required.");
        }

        if (request.MasterFile is null || request.MasterFile.Length == 0)
        {
            return BadRequest("Master track file is required.");
        }

        var accessResult = await EnsureCanManageTrack(trackId, cancellationToken);
        if (accessResult is not null)
        {
            return accessResult;
        }

        await using var stream = request.MasterFile.OpenReadStream();

        try
        {
            var result = await _replaceTrackMasterHandler.Handle(
                new ReplaceTrackMasterCommand(
                    trackId,
                    request.MasterFile.FileName,
                    request.MasterFile.ContentType,
                    stream,
                    GetCurrentUserId()),
                cancellationToken);

            return result is null ? NotFound() : Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
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

    private async Task<ActionResult?> EnsureCanManageTrack(
        int trackId,
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

        var canManageTrack = _tokenProvider.AuthorizeUserArtist(
            Request,
            leadArtistId.Value,
            ["Owner", "General Manager", "Streaming Manager"]);

        return canManageTrack ? null : Forbid();
    }

    private bool IsAdmin()
    {
        return User.IsInRole("Admin") ||
            string.Equals(_tokenProvider.GetJwtRoleClaimValue(Request), "Admin", StringComparison.OrdinalIgnoreCase);
    }
}
