using _808Music.Application.Abstractions;

namespace _808Music.Application.AudioClustering;

public sealed record RunAudioClusteringCommand(
    string AlgorithmName,
    string EmbeddingSource,
    string ParametersJson);

public sealed record RunAudioClusteringResult(AudioClusteringJob Job);

public interface IRunAudioClusteringHandler
{
    Task<RunAudioClusteringResult> Handle(
        RunAudioClusteringCommand command,
        CancellationToken cancellationToken = default);
}

public sealed class RunAudioClusteringHandler : IRunAudioClusteringHandler
{
    private readonly IAudioClusteringService _audioClusteringService;

    public RunAudioClusteringHandler(IAudioClusteringService audioClusteringService)
    {
        _audioClusteringService = audioClusteringService;
    }

    public async Task<RunAudioClusteringResult> Handle(
        RunAudioClusteringCommand command,
        CancellationToken cancellationToken = default)
    {
        var job = await _audioClusteringService.StartAsync(
            command.AlgorithmName,
            command.EmbeddingSource,
            command.ParametersJson,
            cancellationToken);

        return new RunAudioClusteringResult(job);
    }
}
