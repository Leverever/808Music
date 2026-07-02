using _808Music.Application.Abstractions;

namespace _808Music.Infrastructure.Stems;

public sealed class ManifestStemSeparationService : IStemSeparationService
{
    private static readonly string[] DefaultStems = ["vocals", "drums", "bass", "other"];

    private readonly IMediaStorage _mediaStorage;

    public ManifestStemSeparationService(IMediaStorage mediaStorage)
    {
        _mediaStorage = mediaStorage;
    }

    public Task<StemSeparationJob> StartAsync(
        Guid trackId,
        string? requestedByUserId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(new StemSeparationJob(
            Guid.NewGuid(),
            trackId,
            Status: "Queued",
            DateTimeOffset.UtcNow));
    }

    public async Task<IReadOnlyList<StemManifestItem>> GetManifestAsync(
        Guid trackId,
        CancellationToken cancellationToken = default)
    {
        var stems = new List<StemManifestItem>();

        foreach (var stem in DefaultStems)
        {
            var streamUri = await _mediaStorage.CreateReadUrlAsync(
                $"stems/{trackId:N}/{stem}.mp3",
                TimeSpan.FromMinutes(10),
                cancellationToken);
            stems.Add(new StemManifestItem(stem, "audio/mpeg", streamUri));
        }

        return stems;
    }
}
