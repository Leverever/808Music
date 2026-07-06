using System;
using System.ComponentModel.DataAnnotations;

namespace _808Music.Domain.Catalog
{
    public class TrackAudioTag
    {
        [Key]
        public Guid Id { get; private set; }
        public Guid TrackAudioAnalysisId { get; private set; }

        public string Namespace { get; private set; } = string.Empty;
        public string Label { get; private set; } = string.Empty;
        public decimal Score { get; private set; }
        public string ModelName { get; private set; } = string.Empty;

        public DateTime CreatedAt { get; private set; }

        private TrackAudioTag() { } // EF Core

        public TrackAudioTag(
            Guid trackAudioAnalysisId,
            string tagNamespace,
            string label,
            decimal score,
            string modelName)
        {
            Id = Guid.NewGuid();
            TrackAudioAnalysisId = trackAudioAnalysisId;
            Namespace = tagNamespace;
            Label = label;
            Score = score;
            ModelName = modelName;
            CreatedAt = DateTime.UtcNow;
        }
    }
}
