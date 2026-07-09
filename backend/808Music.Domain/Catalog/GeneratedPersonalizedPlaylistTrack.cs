namespace _808Music.Domain.Catalog;

public class GeneratedPersonalizedPlaylistTrack
{
    private GeneratedPersonalizedPlaylistTrack()
    {
        Reason = string.Empty;
    }

    public GeneratedPersonalizedPlaylistTrack(
        Guid playlistId,
        int trackId,
        int position,
        decimal score,
        string reason)
    {
        if (playlistId == Guid.Empty)
        {
            throw new ArgumentException("Playlist id is required.", nameof(playlistId));
        }

        if (trackId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(trackId), "Track id must be positive.");
        }

        if (position <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(position), "Position must be positive.");
        }

        PlaylistId = playlistId;
        TrackId = trackId;
        Position = position;
        Score = Math.Clamp(score, 0m, 1m);
        Reason = NormalizeReason(reason);
    }

    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid PlaylistId { get; private set; }
    public int TrackId { get; private set; }
    public int Position { get; private set; }
    public decimal Score { get; private set; }
    public string Reason { get; private set; }

    private static string NormalizeReason(string? reason)
    {
        var normalized = reason?.Trim() ?? string.Empty;

        return normalized.Length <= 500
            ? normalized
            : normalized[..500];
    }
}
