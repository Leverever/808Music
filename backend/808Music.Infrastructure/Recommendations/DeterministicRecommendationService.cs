using _808Music.Application.Abstractions;

namespace _808Music.Infrastructure.Recommendations;

public sealed class DeterministicRecommendationService : IRecommendationService
{
    public Task<IReadOnlyList<TrackRecommendation>> GetForTrackAsync(
        Guid trackId,
        string? requestedByUserId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        IReadOnlyList<TrackRecommendation> recommendations = Enumerable
            .Range(1, 5)
            .Select(index => new TrackRecommendation(
                CreateRelatedTrackId(trackId, index),
                $"Recommended Track {index}",
                "808 Music",
                Score: Math.Round(1 - index * 0.08, 2),
                Reason: "Similar audio profile and listener context."))
            .ToList();

        return Task.FromResult(recommendations);
    }

    private static Guid CreateRelatedTrackId(Guid trackId, int offset)
    {
        var bytes = trackId.ToByteArray();
        bytes[0] = (byte)(bytes[0] + offset);
        bytes[1] = (byte)(bytes[1] + offset * 7);

        return new Guid(bytes);
    }
}
