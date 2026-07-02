using _808Music.Application.Abstractions;

namespace _808Music.Infrastructure.Audio;

public sealed class DeterministicAudioFeatureExtractor : IAudioFeatureExtractor
{
    public Task<AudioFeatureSet> ExtractAsync(
        Guid trackId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var bytes = trackId.ToByteArray();

        var features = new AudioFeatureSet(
            trackId,
            Tempo: 80 + bytes[0] % 81,
            Energy: Normalize(bytes[1]),
            Danceability: Normalize(bytes[2]),
            Valence: Normalize(bytes[3]),
            Source: $"tracks/{trackId:N}/original.mp3");

        return Task.FromResult(features);
    }

    private static double Normalize(byte value)
    {
        return Math.Round(value / 255d, 3);
    }
}
