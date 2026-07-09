using System;
using System.ComponentModel.DataAnnotations;

namespace _808Music.Domain.Catalog
{
    public class TrackClusterAssignment
    {
        [Key]
        public Guid Id { get; private set; }
        public Guid ClusterRunId { get; private set; }
        public Guid? ClusterId { get; private set; }
        public int TrackId { get; private set; }

        public string ClusterKey { get; private set; } = string.Empty;
        public bool IsNoise { get; private set; }
        public decimal? DistanceToCenter { get; private set; }
        public decimal? MembershipScore { get; private set; }

        public DateTime CreatedAt { get; private set; }

        private TrackClusterAssignment() { } // EF Core

        public TrackClusterAssignment(
            Guid clusterRunId,
            Guid? clusterId,
            int trackId,
            string clusterKey,
            bool isNoise,
            decimal? distanceToCenter,
            decimal? membershipScore)
        {
            Id = Guid.NewGuid();
            ClusterRunId = clusterRunId;
            ClusterId = clusterId;
            TrackId = trackId;
            ClusterKey = clusterKey;
            IsNoise = isNoise;
            DistanceToCenter = distanceToCenter;
            MembershipScore = membershipScore;
            CreatedAt = DateTime.UtcNow;
        }
    }
}
