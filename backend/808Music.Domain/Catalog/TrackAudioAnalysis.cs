using _808Music.Domain.Enums;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace _808Music.Domain.Catalog
{
    public class TrackAudioAnalysis
    {
        [Key]
        public Guid Id { get; private set; }
        public int TrackId { get; private set; }

        public AudioAnalysisStatus Status { get; private set; }

        public string ProviderName { get; private set; } = string.Empty;
        public string ModelName { get; private set; } = string.Empty;
        public string ModelVersion { get; private set; } = string.Empty;
        public string? EmbeddingModel { get; private set; }
        public string? EmbeddingJson { get; private set; }

        public bool IsActive { get; private set; }

        public DateTime CreatedAt { get; private set; }
        public DateTime? StartedAt { get; private set; }
        public DateTime? CompletedAt { get; private set; }

        public string? ErrorMessage { get; private set; }

        private readonly List<TrackAudioTag> _tags = new();
        public IReadOnlyCollection<TrackAudioTag> Tags => _tags.AsReadOnly();

        private TrackAudioAnalysis() { } // EF Core

        public TrackAudioAnalysis(
            int trackId,
            string providerName,
            string modelName,
            string modelVersion)
        {
            Id = Guid.NewGuid();
            TrackId = trackId;
            ProviderName = providerName;
            ModelName = modelName;
            ModelVersion = modelVersion;
            Status = AudioAnalysisStatus.Pending;
            CreatedAt = DateTime.UtcNow;
        }

        public void MarkProcessing()
        {
            if (Status != AudioAnalysisStatus.Pending)
                throw new InvalidOperationException("Only pending audio analyses can start processing.");

            Status = AudioAnalysisStatus.Processing;
            StartedAt = DateTime.UtcNow;
        }

        public void MarkFailed(string errorMessage)
        {
            Status = AudioAnalysisStatus.Failed;
            ErrorMessage = errorMessage;
            CompletedAt = DateTime.UtcNow;
        }
    }
}
