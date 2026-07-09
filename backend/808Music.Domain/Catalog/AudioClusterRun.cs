using _808Music.Domain.Enums;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace _808Music.Domain.Catalog
{
    public class AudioClusterRun
    {
        [Key]
        public Guid Id { get; private set; }

        public AudioClusterRunStatus Status { get; private set; }
        public string AlgorithmName { get; private set; } = string.Empty;
        public string EmbeddingSource { get; private set; } = string.Empty;
        public string ParametersJson { get; private set; } = "{}";
        public bool IsActive { get; private set; }

        public DateTime CreatedAt { get; private set; }
        public DateTime? StartedAt { get; private set; }
        public DateTime? CompletedAt { get; private set; }
        public string? ErrorMessage { get; private set; }

        private readonly List<AudioCluster> _clusters = new();
        public IReadOnlyCollection<AudioCluster> Clusters => _clusters.AsReadOnly();

        private AudioClusterRun() { } // EF Core

        public AudioClusterRun(
            string algorithmName,
            string embeddingSource,
            string parametersJson)
        {
            Id = Guid.NewGuid();
            AlgorithmName = algorithmName;
            EmbeddingSource = embeddingSource;
            ParametersJson = string.IsNullOrWhiteSpace(parametersJson) ? "{}" : parametersJson;
            Status = AudioClusterRunStatus.Pending;
            CreatedAt = DateTime.UtcNow;
        }

        public void MarkProcessing()
        {
            if (Status != AudioClusterRunStatus.Pending)
                throw new InvalidOperationException("Only pending cluster runs can start processing.");

            Status = AudioClusterRunStatus.Processing;
            StartedAt = DateTime.UtcNow;
        }

        public void MarkFailed(string errorMessage)
        {
            Status = AudioClusterRunStatus.Failed;
            ErrorMessage = errorMessage;
            CompletedAt = DateTime.UtcNow;
        }
    }
}
