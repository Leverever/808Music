using _808Music.Application;
using _808Music.Application.Abstractions;
using _808Music.Application.AudioClustering;
using _808Music.Domain.Catalog;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace _808Music.Infrastructure.AudioClustering;

public sealed class QueuedAudioClusteringService : IAudioClusteringService
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IAudioClusteringJobQueue _jobQueue;
    private readonly AudioClusteringOptions _options;

    public QueuedAudioClusteringService(
        IApplicationDbContext dbContext,
        IAudioClusteringJobQueue jobQueue,
        IOptions<AudioClusteringOptions> options)
    {
        _dbContext = dbContext;
        _jobQueue = jobQueue;
        _options = options.Value;
    }

    public async Task<AudioClusteringJob> StartAsync(
        string algorithmName,
        string embeddingSource,
        string parametersJson,
        CancellationToken cancellationToken = default)
    {
        var normalizedAlgorithm = string.IsNullOrWhiteSpace(algorithmName)
            ? _options.DefaultAlgorithmName
            : algorithmName.Trim();
        var normalizedEmbeddingSource = string.IsNullOrWhiteSpace(embeddingSource)
            ? _options.DefaultEmbeddingSource
            : embeddingSource.Trim();
        var normalizedParameters = string.IsNullOrWhiteSpace(parametersJson)
            ? _options.DefaultParametersJson
            : parametersJson.Trim();

        var parameters = DeserializeParameters(normalizedParameters);
        var run = new AudioClusterRun(
            normalizedAlgorithm,
            normalizedEmbeddingSource,
            normalizedParameters);

        await _dbContext.AudioClusterRuns.AddAsync(run, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        try
        {
            await _jobQueue.EnqueueAsync(
                new AudioClusteringRequestedMessage(
                    run.Id,
                    run.AlgorithmName,
                    run.EmbeddingSource,
                    parameters),
                cancellationToken);
        }
        catch (Exception ex)
        {
            run.MarkFailed($"Failed to enqueue audio clustering job: {ex.Message}");
            await _dbContext.SaveChangesAsync(CancellationToken.None);
            throw;
        }

        return new AudioClusteringJob(
            run.Id,
            run.AlgorithmName,
            run.EmbeddingSource,
            run.Status.ToString(),
            run.CreatedAt);
    }

    private static IReadOnlyDictionary<string, object?> DeserializeParameters(string parametersJson)
    {
        using var document = JsonDocument.Parse(parametersJson);
        return document.RootElement.EnumerateObject()
            .ToDictionary(
                property => property.Name,
                property => ToObject(property.Value));
    }

    private static object? ToObject(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number when element.TryGetInt32(out var value) => value,
            JsonValueKind.Number => element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            _ => element.GetRawText()
        };
    }
}
