using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace _808Music.Domain.Catalog
{
    public class AudioCluster
    {
        [Key]
        public Guid Id { get; private set; }
        public Guid ClusterRunId { get; private set; }

        public string ClusterKey { get; private set; } = string.Empty;
        public string Name { get; private set; } = string.Empty;
        public int Size { get; private set; }
        public string TopTagsJson { get; private set; } = "[]";

        public DateTime CreatedAt { get; private set; }

        private readonly List<TrackClusterAssignment> _assignments = new();
        public IReadOnlyCollection<TrackClusterAssignment> Assignments => _assignments.AsReadOnly();

        private AudioCluster() { } // EF Core

        public AudioCluster(
            Guid clusterRunId,
            string clusterKey,
            string name,
            int size,
            string topTagsJson)
        {
            Id = Guid.NewGuid();
            ClusterRunId = clusterRunId;
            ClusterKey = clusterKey;
            Name = name;
            Size = size;
            TopTagsJson = string.IsNullOrWhiteSpace(topTagsJson) ? "[]" : topTagsJson;
            CreatedAt = DateTime.UtcNow;
        }
    }
}
