using _808Music.Application.AudioClustering;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using RS1_2024_25.API.Controllers.V2.Requests;
using RS1_2024_25.API.Services;
using System.Text.Json;

namespace RS1_2024_25.API.Controllers.V2;

[ApiController]
[ApiVersion("2.0")]
[Route("api/v{version:apiVersion}/audio-clustering")]
[Produces("application/json")]
public sealed class AudioClusteringController : ControllerBase
{
    private readonly IRunAudioClusteringHandler _runAudioClusteringHandler;
    private readonly TokenProvider _tokenProvider;

    public AudioClusteringController(
        IRunAudioClusteringHandler runAudioClusteringHandler,
        TokenProvider tokenProvider)
    {
        _runAudioClusteringHandler = runAudioClusteringHandler;
        _tokenProvider = tokenProvider;
    }

    [Authorize]
    [HttpPost("jobs")]
    [MapToApiVersion("2.0")]
    public async Task<ActionResult<RunAudioClusteringResult>> Run(
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] RunAudioClusteringRequest? request,
        CancellationToken cancellationToken)
    {
        if (!IsAdmin())
        {
            return Forbid();
        }

        request ??= new RunAudioClusteringRequest();

        if (!string.IsNullOrWhiteSpace(request.ParametersJson) &&
            !TryIsValidJsonObject(request.ParametersJson))
        {
            return BadRequest("ParametersJson must be a valid JSON object.");
        }

        try
        {
            var result = await _runAudioClusteringHandler.Handle(
                new RunAudioClusteringCommand(
                    request.AlgorithmName ?? string.Empty,
                    request.EmbeddingSource ?? string.Empty,
                    request.ParametersJson ?? string.Empty),
                cancellationToken);

            return Accepted(result);
        }
        catch (JsonException)
        {
            return BadRequest("ParametersJson must be a valid JSON object.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                $"Failed to queue audio clustering job: {ex.Message}");
        }
    }

    private bool IsAdmin()
    {
        return User.IsInRole("Admin") ||
            string.Equals(_tokenProvider.GetJwtRoleClaimValue(Request), "Admin", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryIsValidJsonObject(string value)
    {
        try
        {
            using var document = JsonDocument.Parse(value);
            return document.RootElement.ValueKind == JsonValueKind.Object;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
