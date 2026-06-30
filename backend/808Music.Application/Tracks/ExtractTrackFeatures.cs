using _808Music.Application.Abstractions;

namespace _808Music.Application.Tracks;

public sealed record ExtractTrackFeaturesCommand(Guid TrackId, string? RequestedByUserId);

public sealed record ExtractTrackFeaturesResult(
    Guid TrackId,
    AudioFeatureSet Features,
    DateTimeOffset ExtractedAt);

public interface IExtractTrackFeaturesHandler
{
    Task<ExtractTrackFeaturesResult> Handle(
        ExtractTrackFeaturesCommand command,
        CancellationToken cancellationToken = default);
}

public sealed class ExtractTrackFeaturesHandler : IExtractTrackFeaturesHandler
{
    private readonly IAudioFeatureExtractor _audioFeatureExtractor;

    public ExtractTrackFeaturesHandler(IAudioFeatureExtractor audioFeatureExtractor)
    {
        _audioFeatureExtractor = audioFeatureExtractor;
    }

    public async Task<ExtractTrackFeaturesResult> Handle(
        ExtractTrackFeaturesCommand command,
        CancellationToken cancellationToken = default)
    {
        var features = await _audioFeatureExtractor.ExtractAsync(command.TrackId, cancellationToken);

        return new ExtractTrackFeaturesResult(
            command.TrackId,
            features,
            DateTimeOffset.UtcNow);
    }
}
