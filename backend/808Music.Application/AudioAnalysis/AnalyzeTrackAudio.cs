using _808Music.Application.Abstractions;

namespace _808Music.Application.AudioAnalysis;

public sealed record AnalyzeTrackAudioCommand(int TrackId, string? RequestedByUserId);

public sealed record AnalyzeTrackAudioResult(AudioAnalysisJob Job);

public interface IAnalyzeTrackAudioHandler
{
    Task<AnalyzeTrackAudioResult> Handle(
        AnalyzeTrackAudioCommand command,
        CancellationToken cancellationToken = default);
}

public sealed class AnalyzeTrackAudioHandler : IAnalyzeTrackAudioHandler
{
    private readonly IAudioAnalysisService _audioAnalysisService;

    public AnalyzeTrackAudioHandler(IAudioAnalysisService audioAnalysisService)
    {
        _audioAnalysisService = audioAnalysisService;
    }

    public async Task<AnalyzeTrackAudioResult> Handle(
        AnalyzeTrackAudioCommand command,
        CancellationToken cancellationToken = default)
    {
        var job = await _audioAnalysisService.StartAsync(
            command.TrackId,
            command.RequestedByUserId,
            cancellationToken);

        return new AnalyzeTrackAudioResult(job);
    }
}
