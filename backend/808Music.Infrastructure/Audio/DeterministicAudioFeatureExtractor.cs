using _808Music.Application.Abstractions;

namespace _808Music.Infrastructure.Audio;

public sealed class DeterministicAudioFeatureExtractor : IAudioFeatureExtractor
{
    private readonly IMediaStorage _mediaStorage;

    public DeterministicAudioFeatureExtractor(IMediaStorage mediaStorage)
    {
        _mediaStorage = mediaStorage;
    }

    public async Task<AudioFeatureSet> ExtractAsync(
        Guid trackId,
        CancellationToken cancellationToken = default)
    {
        var source = await _mediaStorage.GetTrackFileAsync(trackId, cancellationToken);
        var bytes = trackId.ToByteArray();

        return new AudioFeatureSet(
            trackId,
            Tempo: 80 + bytes[0] % 81,
            Energy: Normalize(bytes[1]),
            Danceability: Normalize(bytes[2]),
            Valence: Normalize(bytes[3]),
            Source: source?.StorageKey ?? "unknown");
    }

    private static double Normalize(byte value)
    {
        return Math.Round(value / 255d, 3);
    }
}
