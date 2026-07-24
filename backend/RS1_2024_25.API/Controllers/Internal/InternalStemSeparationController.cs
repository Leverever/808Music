using _808Music.Application.Stems;
using Microsoft.AspNetCore.Mvc;
using RS1_2024_25.API.Controllers.Internal.Requests;

namespace RS1_2024_25.API.Controllers.Internal;

[ApiController]
[ApiExplorerSettings(IgnoreApi = true)]
[Route("api/internal/stem-separation")]
[Produces("application/json")]
public sealed class InternalStemSeparationController : ControllerBase
{
    private const string InternalApiKeyHeader = "X-Internal-Api-Key";

    private readonly IConfiguration _configuration;
    private readonly IMarkStemSeparationProcessingHandler _processingHandler;
    private readonly ICompleteStemSeparationHandler _completeHandler;
    private readonly IFailStemSeparationHandler _failHandler;

    public InternalStemSeparationController(
        IConfiguration configuration,
        IMarkStemSeparationProcessingHandler processingHandler,
        ICompleteStemSeparationHandler completeHandler,
        IFailStemSeparationHandler failHandler)
    {
        _configuration = configuration;
        _processingHandler = processingHandler;
        _completeHandler = completeHandler;
        _failHandler = failHandler;
    }

    [HttpPost("{stemSetId:guid}/processing")]
    public async Task<IActionResult> MarkProcessing(
        Guid stemSetId,
        CancellationToken cancellationToken)
    {
        if (!IsAuthorized())
        {
            return Unauthorized();
        }

        try
        {
            await _processingHandler.Handle(
                new MarkStemSeparationProcessingCommand(stemSetId),
                cancellationToken);

            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }

    [HttpPost("{stemSetId:guid}/complete")]
    public async Task<IActionResult> MarkComplete(
        Guid stemSetId,
        [FromBody] CompleteStemSeparationRequest request,
        CancellationToken cancellationToken)
    {
        if (!IsAuthorized())
        {
            return Unauthorized();
        }

        if (request.Stems is null || request.Stems.Count == 0)
        {
            return BadRequest("At least one stem is required.");
        }

        try
        {
            await _completeHandler.Handle(
                new CompleteStemSeparationCommand(
                    stemSetId,
                    request.Stems
                        .Select(stem => new CompletedStemDto(
                            stem.StemType,
                            stem.ObjectKey,
                            stem.ContentType,
                            stem.SizeBytes,
                            stem.DurationMs,
                            stem.SampleRate,
                            stem.BitrateKbps,
                            stem.Codec,
                            stem.Channels,
                            stem.ChecksumSha256))
                        .ToList()),
                cancellationToken);

            return NoContent();
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

    [HttpPost("{stemSetId:guid}/failed")]
    public async Task<IActionResult> MarkFailed(
        Guid stemSetId,
        [FromBody] FailStemSeparationRequest request,
        CancellationToken cancellationToken)
    {
        if (!IsAuthorized())
        {
            return Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(request.ErrorMessage))
        {
            return BadRequest("Error message is required.");
        }

        try
        {
            await _failHandler.Handle(
                new FailStemSeparationCommand(stemSetId, request.ErrorMessage),
                cancellationToken);

            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }

    private bool IsAuthorized()
    {
        var configuredApiKey = _configuration["InternalApi:ApiKey"];
        if (string.IsNullOrWhiteSpace(configuredApiKey))
        {
            return false;
        }

        return Request.Headers.TryGetValue(InternalApiKeyHeader, out var providedApiKey) &&
            string.Equals(providedApiKey.ToString(), configuredApiKey, StringComparison.Ordinal);
    }
}
