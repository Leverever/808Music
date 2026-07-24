namespace _808Music.Application.Abstractions;

public interface IAudioFeatureExtractor
{
    Task<AudioFeatureSet> ExtractAsync(Guid trackId, CancellationToken cancellationToken = default);
}

public sealed record AudioFeatureSet(
    Guid TrackId,
    double Tempo,
    double Energy,
    double Danceability,
    double Valence,
    string Source);
