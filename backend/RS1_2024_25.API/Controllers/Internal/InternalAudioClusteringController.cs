using _808Music.Application.AudioClustering;
using Microsoft.AspNetCore.Mvc;
using RS1_2024_25.API.Controllers.Internal.Requests;

namespace RS1_2024_25.API.Controllers.Internal;

[ApiController]
[ApiExplorerSettings(IgnoreApi = true)]
[Route("api/internal/audio-clustering")]
[Produces("application/json")]
public sealed class InternalAudioClusteringController : ControllerBase
{
    private const string InternalApiKeyHeader = "X-Internal-Api-Key";

    private readonly IConfiguration _configuration;
    private readonly IMarkAudioClusteringProcessingHandler _processingHandler;
    private readonly IGetAudioClusteringTracksHandler _tracksHandler;
    private readonly ICompleteAudioClusteringHandler _completeHandler;
    private readonly IFailAudioClusteringHandler _failHandler;

    public InternalAudioClusteringController(
        IConfiguration configuration,
        IMarkAudioClusteringProcessingHandler processingHandler,
        IGetAudioClusteringTracksHandler tracksHandler,
        ICompleteAudioClusteringHandler completeHandler,
        IFailAudioClusteringHandler failHandler)
    {
        _configuration = configuration;
        _processingHandler = processingHandler;
        _tracksHandler = tracksHandler;
        _completeHandler = completeHandler;
        _failHandler = failHandler;
    }

    [HttpPost("{clusterRunId:guid}/processing")]
    public async Task<IActionResult> MarkProcessing(
        Guid clusterRunId,
        CancellationToken cancellationToken)
    {
        if (!IsAuthorized())
        {
            return Unauthorized();
        }

        try
        {
            await _processingHandler.Handle(
                new MarkAudioClusteringProcessingCommand(clusterRunId),
                cancellationToken);

            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }

    [HttpGet("{clusterRunId:guid}/tracks")]
    public async Task<IActionResult> GetTracks(
        Guid clusterRunId,
        [FromQuery] string embeddingSource,
        CancellationToken cancellationToken)
    {
        if (!IsAuthorized())
        {
            return Unauthorized();
        }

        try
        {
            var result = await _tracksHandler.Handle(
                new GetAudioClusteringTracksQuery(clusterRunId, embeddingSource),
                cancellationToken);

            return Ok(new
            {
                tracks = result.Tracks.Select(track => new
                {
                    trackId = track.TrackId,
                    embedding = track.Embedding,
                    tags = track.Tags.Select(tag => new
                    {
                        @namespace = tag.Namespace,
                        label = tag.Label,
                        score = tag.Score
                    })
                })
            });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }

    [HttpPost("{clusterRunId:guid}/complete")]
    public async Task<IActionResult> MarkComplete(
        Guid clusterRunId,
        [FromBody] CompleteAudioClusteringRequest request,
        CancellationToken cancellationToken)
    {
        if (!IsAuthorized())
        {
            return Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(request.AlgorithmName))
        {
            return BadRequest("Algorithm name is required.");
        }

        if (string.IsNullOrWhiteSpace(request.EmbeddingSource))
        {
            return BadRequest("Embedding source is required.");
        }

        try
        {
            await _completeHandler.Handle(
                new CompleteAudioClusteringCommand(
                    clusterRunId,
                    request.AlgorithmName,
                    request.EmbeddingSource,
                    request.Clusters
                        .Select(cluster => new AudioClusterDto(
                            cluster.ClusterKey,
                            cluster.Name,
                            cluster.Size,
                            cluster.TopTags
                                .Select(tag => new AudioClusteringTagDto(
                                    tag.Namespace,
                                    tag.Label,
                                    tag.Score))
                                .ToList()))
                        .ToList(),
                    request.Assignments
                        .Select(assignment => new TrackClusterAssignmentDto(
                            assignment.TrackId,
                            assignment.ClusterKey,
                            assignment.IsNoise,
                            assignment.DistanceToCenter,
                            assignment.MembershipScore))
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

    [HttpPost("{clusterRunId:guid}/failed")]
    public async Task<IActionResult> MarkFailed(
        Guid clusterRunId,
        [FromBody] FailAudioClusteringRequest request,
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
                new FailAudioClusteringCommand(clusterRunId, request.ErrorMessage),
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
