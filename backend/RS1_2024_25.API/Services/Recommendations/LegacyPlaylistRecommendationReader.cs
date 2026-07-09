using _808Music.Application.Abstractions;
using Microsoft.EntityFrameworkCore;
using RS1_2024_25.API.Data;

namespace RS1_2024_25.API.Services.Recommendations;

public sealed class LegacyPlaylistRecommendationReader : ILegacyPlaylistRecommendationReader
{
    private const string DefaultPlaylistCoverPath = "/Images/playlist_placeholder.png";

    private readonly ApplicationDbContext _dbContext;

    public LegacyPlaylistRecommendationReader(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<LegacyPlaylistRecommendation>> GetRecommendedPlaylistsAsync(
        LegacyPlaylistRecommendationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.UserId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request.UserId), "User id must be positive.");
        }

        var limit = Math.Clamp(request.Limit <= 0 ? 10 : request.Limit, 1, 25);
        var trackSignals = request.TrackSignals
            .Where(x => x.TrackId > 0)
            .GroupBy(x => x.TrackId)
            .Select(x => x.OrderByDescending(signal => signal.Score).First())
            .ToArray();

        if (trackSignals.Length == 0)
        {
            return [];
        }

        var scoresByTrack = trackSignals.ToDictionary(x => x.TrackId, x => Clamp01(x.Score));
        var trackIds = scoresByTrack.Keys.ToArray();

        var playlistRows = await _dbContext.Playlists
            .AsNoTracking()
            .Where(x =>
                !x.isLikePlaylist &&
                (x.IsPublic || x.UserPlaylists.Any(userPlaylist => userPlaylist.MyAppUserId == request.UserId)) &&
                x.PlaylistTracks.Any(track => trackIds.Contains(track.TrackId)))
            .Select(x => new PlaylistProjection(
                x.Id,
                x.Title,
                x.CoverPath,
                x.NumOfTracks,
                x.IsPublic,
                x.IsCollaborative,
                x.UserPlaylists
                    .Where(userPlaylist => userPlaylist.IsOwner)
                    .Select(userPlaylist => (int?)userPlaylist.MyAppUserId)
                    .FirstOrDefault(),
                x.UserPlaylists
                    .Where(userPlaylist => userPlaylist.IsOwner)
                    .Select(userPlaylist => userPlaylist.User.Username)
                    .FirstOrDefault()))
            .ToListAsync(cancellationToken);

        if (playlistRows.Count == 0)
        {
            return [];
        }

        var playlistIds = playlistRows
            .Select(x => x.PlaylistId)
            .ToArray();
        var matchedPlaylistTracks = await _dbContext.PlaylistTracks
            .AsNoTracking()
            .Where(x => playlistIds.Contains(x.PlaylistId) && trackIds.Contains(x.TrackId))
            .Select(x => new PlaylistTrackProjection(x.PlaylistId, x.TrackId))
            .ToListAsync(cancellationToken);
        var matchedTracksByPlaylist = matchedPlaylistTracks
            .GroupBy(x => x.PlaylistId)
            .ToDictionary(
                x => x.Key,
                x => x
                    .Select(track => track.TrackId)
                    .Distinct()
                    .ToArray());

        return playlistRows
            .Where(x => matchedTracksByPlaylist.ContainsKey(x.PlaylistId))
            .Select(x =>
            {
                var matchedTrackIds = matchedTracksByPlaylist[x.PlaylistId];
                var score = CalculatePlaylistScore(
                    matchedTrackIds.Select(trackId => scoresByTrack[trackId]),
                    matchedTrackIds.Length,
                    Math.Max(x.TrackCount, matchedTrackIds.Length));

                return new LegacyPlaylistRecommendation(
                    x.PlaylistId,
                    x.Title,
                    NormalizePlaylistCoverPath(x.CoverPath),
                    x.IsPublic,
                    x.IsCollaborative,
                    Math.Max(x.TrackCount, matchedTrackIds.Length),
                    x.OwnerUserId,
                    x.OwnerUsername,
                    score,
                    BuildReason(x.IsPublic, matchedTrackIds.Length),
                    matchedTrackIds);
            })
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Title)
            .Take(limit)
            .ToArray();
    }

    private static double CalculatePlaylistScore(
        IEnumerable<double> scores,
        int matchedTrackCount,
        int playlistTrackCount)
    {
        var topAverage = scores
            .OrderByDescending(x => x)
            .Take(10)
            .DefaultIfEmpty(0)
            .Average();
        var coverageBoost = playlistTrackCount <= 0
            ? 0
            : Math.Min(0.20, (double)matchedTrackCount / playlistTrackCount * 0.20);

        return RoundScore(topAverage * 0.80 + coverageBoost);
    }

    private static string BuildReason(bool isPublic, int matchedTrackCount)
    {
        var trackWord = matchedTrackCount == 1 ? "track" : "tracks";

        return isPublic
            ? $"Matches your taste through {matchedTrackCount} recommended {trackWord}."
            : $"One of your playlists matches {matchedTrackCount} recommended {trackWord}.";
    }

    private static string NormalizePlaylistCoverPath(string? coverPath)
    {
        return string.IsNullOrWhiteSpace(coverPath)
            ? DefaultPlaylistCoverPath
            : coverPath.Trim();
    }

    private static double Clamp01(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            return 0;
        }

        return Math.Clamp(value, 0, 1);
    }

    private static double RoundScore(double value)
    {
        return Math.Round(Clamp01(value), 8, MidpointRounding.AwayFromZero);
    }

    private sealed record PlaylistProjection(
        int PlaylistId,
        string Title,
        string? CoverPath,
        int TrackCount,
        bool IsPublic,
        bool IsCollaborative,
        int? OwnerUserId,
        string? OwnerUsername);

    private sealed record PlaylistTrackProjection(
        int PlaylistId,
        int TrackId);
}
