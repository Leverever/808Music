using _808Music.Application.Tracks;
using _808Music.Domain.Enums;
using _808Music.Application.Stems;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RS1_2024_25.API.Controllers.V2.Requests;
using RS1_2024_25.API.Services;
using System.Security.Claims;

namespace RS1_2024_25.API.Controllers.V2;

[ApiController]
[ApiVersion("2.0")]
[Route("api/v{version:apiVersion}/tracks/{trackId:int}/stems")]
[Produces("application/json")]
public sealed class StemsController : ControllerBase
{
    private readonly ISeparateTrackStemsHandler _separateTrackStemsHandler;
    private readonly IGetTrackStemsHandler _getTrackStemsHandler;
    private readonly IUploadManualStemSetHandler _uploadManualStemSetHandler;
    private readonly ITrackArtistAccessQuery _trackArtistAccessQuery;
    private readonly TokenProvider _tokenProvider;

    public StemsController(
        ISeparateTrackStemsHandler separateTrackStemsHandler,
        IGetTrackStemsHandler getTrackStemsHandler,
        IUploadManualStemSetHandler uploadManualStemSetHandler,
        ITrackArtistAccessQuery trackArtistAccessQuery,
        TokenProvider tokenProvider)
    {
        _separateTrackStemsHandler = separateTrackStemsHandler;
        _getTrackStemsHandler = getTrackStemsHandler;
        _uploadManualStemSetHandler = uploadManualStemSetHandler;
        _trackArtistAccessQuery = trackArtistAccessQuery;
        _tokenProvider = tokenProvider;
    }

    [Authorize]
    [HttpPost("separate")]
    [MapToApiVersion("2.0")]
    public async Task<ActionResult<SeparateTrackStemsResult>> Separate(
        int trackId,
        CancellationToken cancellationToken)
    {
        if (trackId <= 0)
        {
            return BadRequest("Track id is required.");
        }

        var accessResult = await EnsureCanManageTrack(trackId, cancellationToken);
        if (accessResult is not null)
        {
            return accessResult;
        }

        try
        {
            var command = new SeparateTrackStemsCommand(trackId, GetCurrentUserId());
            var result = await _separateTrackStemsHandler.Handle(command, cancellationToken);

            return Accepted(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [Authorize]
    [HttpPost("upload")]
    [MapToApiVersion("2.0")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<UploadManualStemSetResult>> Upload(
        int trackId,
        [FromForm] UploadManualStemSetRequest request,
        CancellationToken cancellationToken)
    {
        if (trackId <= 0)
        {
            return BadRequest("Track id is required.");
        }

        var accessResult = await EnsureCanManageTrack(trackId, cancellationToken);
        if (accessResult is not null)
        {
            return accessResult;
        }

        var uploads = new List<ManualStemUpload>();

        try
        {
            AddUpload(uploads, StemType.Vocals, request.Vocals);
            AddUpload(uploads, StemType.Drums, request.Drums);
            AddUpload(uploads, StemType.Bass, request.Bass);
            AddUpload(uploads, StemType.Other, request.Other);
            AddUpload(uploads, StemType.Instrumental, request.Instrumental);

            var result = await _uploadManualStemSetHandler.Handle(
                new UploadManualStemSetCommand(
                    trackId,
                    request.StemProfile,
                    uploads),
                cancellationToken);

            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        finally
        {
            foreach (var upload in uploads)
            {
                await upload.Content.DisposeAsync();
            }
        }
    }

    [HttpGet]
    [MapToApiVersion("2.0")]
    public async Task<ActionResult<GetTrackStemsResult>> GetManifest(
        int trackId,
        CancellationToken cancellationToken)
    {
        if (trackId <= 0)
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

    private static void AddUpload(
        ICollection<ManualStemUpload> uploads,
        StemType stemType,
        IFormFile? file)
    {
        if (file is null)
        {
            return;
        }

        uploads.Add(new ManualStemUpload(
            stemType,
            file.FileName,
            file.ContentType,
            file.Length,
            file.OpenReadStream()));
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
