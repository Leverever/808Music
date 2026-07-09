using _808Music.Application.Abstractions;
using _808Music.Application.Common.Scheduling;
using _808Music.Infrastructure.AudioClustering;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace _808Music.Infrastructure.BackgroundTasks;

public sealed class AudioClusteringRecurringTask : IRecurringApplicationTask
{
    private readonly IAudioClusteringService _audioClusteringService;
    private readonly AudioClusteringOptions _options;
    private readonly ILogger<AudioClusteringRecurringTask> _logger;

    public AudioClusteringRecurringTask(
        IAudioClusteringService audioClusteringService,
        IOptions<AudioClusteringOptions> options,
        ILogger<AudioClusteringRecurringTask> logger)
    {
        _audioClusteringService = audioClusteringService;
        _options = options.Value;
        _logger = logger;
    }

    public string Name => "audio-clustering";
    public string CronExpression => _options.RecurringCronExpression;
    public bool IsEnabled => _options.RecurringEnabled;

    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var job = await _audioClusteringService.StartAsync(
            _options.DefaultAlgorithmName,
            _options.DefaultEmbeddingSource,
            _options.DefaultParametersJson,
            cancellationToken);

        _logger.LogInformation(
            "Queued audio clustering job {JobId} using {AlgorithmName}/{EmbeddingSource}",
            job.JobId,
            job.AlgorithmName,
            job.EmbeddingSource);
    }
}
