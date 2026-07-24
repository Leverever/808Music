using _808Music.Domain.Enums;
using System;
using System.ComponentModel.DataAnnotations;

namespace _808Music.Domain.Catalog
{
    public class UserTrackInteraction
    {
        [Key]
        public Guid Id { get; private set; }
        public int UserId { get; private set; }
        public int TrackId { get; private set; }
        public UserTrackInteractionType InteractionType { get; private set; }
        public DateTime OccurredAt { get; private set; }
        public long? PlayedMs { get; private set; }
        public long? TrackDurationMs { get; private set; }
        public decimal? CompletionRatio { get; private set; }
        public string? ContextType { get; private set; }
        public string? ClientEventId { get; private set; }
        public DateTime CreatedAt { get; private set; }

        private UserTrackInteraction() { } // EF Core

        public UserTrackInteraction(
            int userId,
            int trackId,
            UserTrackInteractionType interactionType,
            DateTime occurredAt,
            long? playedMs,
            long? trackDurationMs,
            string? contextType,
            string? clientEventId)
        {
            if (userId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(userId), "User id must be positive.");
            }

            if (trackId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(trackId), "Track id must be positive.");
            }

            if (playedMs is < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(playedMs), "Played milliseconds cannot be negative.");
            }

            if (trackDurationMs is < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(trackDurationMs), "Track duration milliseconds cannot be negative.");
            }

            Id = Guid.NewGuid();
            UserId = userId;
            TrackId = trackId;
            InteractionType = interactionType;
            OccurredAt = occurredAt;
            PlayedMs = playedMs;
            TrackDurationMs = trackDurationMs;
            CompletionRatio = CalculateCompletionRatio(playedMs, trackDurationMs);
            ContextType = Normalize(contextType);
            ClientEventId = Normalize(clientEventId);
            CreatedAt = DateTime.UtcNow;
        }

        private static decimal? CalculateCompletionRatio(long? playedMs, long? trackDurationMs)
        {
            if (playedMs is null || trackDurationMs is null || trackDurationMs <= 0)
            {
                return null;
            }

            var ratio = (decimal)playedMs.Value / trackDurationMs.Value;
            return Math.Round(Math.Clamp(ratio, 0m, 1m), 6);
        }

        private static string? Normalize(string? value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? null
                : value.Trim();
        }
    }
}
