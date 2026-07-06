using _808Music.Application.AudioAnalysis;
using Microsoft.AspNetCore.Mvc;
using RS1_2024_25.API.Controllers.Internal.Requests;

namespace RS1_2024_25.API.Controllers.Internal;

[ApiController]
[ApiExplorerSettings(IgnoreApi = true)]
[Route("api/internal/audio-analysis")]
[Produces("application/json")]
public sealed class InternalAudioAnalysisController : ControllerBase
{
    private const string InternalApiKeyHeader = "X-Internal-Api-Key";

    private readonly IConfiguration _configuration;
    private readonly IMarkAudioAnalysisProcessingHandler _processingHandler;
    private readonly ICompleteAudioAnalysisHandler _completeHandler;
    private readonly IFailAudioAnalysisHandler _failHandler;

    public InternalAudioAnalysisController(
        IConfiguration configuration,
        IMarkAudioAnalysisProcessingHandler processingHandler,
        ICompleteAudioAnalysisHandler completeHandler,
        IFailAudioAnalysisHandler failHandler)
    {
        _configuration = configuration;
        _processingHandler = processingHandler;
        _completeHandler = completeHandler;
        _failHandler = failHandler;
    }

    [HttpPost("{analysisId:guid}/processing")]
    public async Task<IActionResult> MarkProcessing(
        Guid analysisId,
        CancellationToken cancellationToken)
    {
        if (!IsAuthorized())
        {
            return Unauthorized();
        }

        try
        {
            await _processingHandler.Handle(
                new MarkAudioAnalysisProcessingCommand(analysisId),
                cancellationToken);

            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }

    [HttpPost("{analysisId:guid}/complete")]
    public async Task<IActionResult> MarkComplete(
        Guid analysisId,
        [FromBody] CompleteAudioAnalysisRequest request,
        CancellationToken cancellationToken)
    {
        if (!IsAuthorized())
        {
            return Unauthorized();
        }

        if (request.TrackId <= 0)
        {
            return BadRequest("Track id is required.");
        }

        if (string.IsNullOrWhiteSpace(request.EmbeddingModel))
        {
            return BadRequest("Embedding model is required.");
        }

        if (request.Embedding.Count == 0)
        {
            return BadRequest("Embedding is required.");
        }

        try
        {
            await _completeHandler.Handle(
                new CompleteAudioAnalysisCommand(
                    analysisId,
                    request.TrackId,
                    request.EmbeddingModel,
                    request.Embedding,
                    request.Tags
                        .Select(tag => new AudioAnalysisTagDto(
                            tag.Namespace,
                            tag.Label,
                            tag.Score,
                            tag.ModelName))
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

    [HttpPost("{analysisId:guid}/failed")]
    public async Task<IActionResult> MarkFailed(
        Guid analysisId,
        [FromBody] FailAudioAnalysisRequest request,
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
                new FailAudioAnalysisCommand(analysisId, request.ErrorMessage),
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
