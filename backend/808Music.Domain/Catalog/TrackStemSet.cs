using _808Music.Domain.Enums;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace _808Music.Domain.Catalog
{
    public class TrackStemSet 
    {
        [Key]
        public Guid Id { get; private set; }
        public int TrackId { get; private set; }

        public StemSetSource Source { get; private set; }
        public StemSetStatus Status { get; private set; }

        public string? ModelName { get; private set; }
        public string? ModelVersion { get; private set; }

        public string ProviderName { get; private set; } = string.Empty;
        public string StemProfile { get; private set; } = string.Empty;

        public bool IsActive { get; private set; }

        public DateTime CreatedAt { get; private set; }
        public DateTime? StartedAt { get; private set; }
        public DateTime? CompletedAt { get; private set; }

        public string? ErrorMessage { get; private set; }

        private readonly List<TrackStem> _stems = new();
        public IReadOnlyCollection<TrackStem> Stems => _stems.AsReadOnly();

        private TrackStemSet() { } // EF Core

        public TrackStemSet(
            int trackId,
            StemSetSource source,
            Guid? requestedByUserId,
            string providerName,
            string? modelName,
            string? modelVersion,
            string stemProfile)
        {
            Id = Guid.NewGuid();
            TrackId = trackId;
            Source = source;
            ProviderName = providerName;
            ModelName = modelName;
            ModelVersion = modelVersion;
            StemProfile = stemProfile;
            Status = StemSetStatus.Pending;
            CreatedAt = DateTime.UtcNow;
        }

        public void MarkProcessing()
        {
            if (Status != StemSetStatus.Pending)
                throw new InvalidOperationException("Only pending stem sets can start processing.");

            Status = StemSetStatus.Processing;
            StartedAt = DateTime.UtcNow;
        }

        public void AddStem(TrackStem stem)
        {
            if (Status == StemSetStatus.Ready)
                throw new InvalidOperationException("Cannot add stems to a ready stem set.");

            if (_stems.Any(x => x.StemType == stem.StemType))
                throw new InvalidOperationException($"Stem type '{stem.StemType}' already exists in this stem set.");

            _stems.Add(stem);
        }

        public void MarkReady()
        {
            var required = GetRequiredStemTypes();

            var existing = _stems.Select(x => x.StemType).ToHashSet();
            var missing = required
                .Where(stemType => !existing.Contains(stemType))
                .ToArray();

            if (missing.Length != 0)
                throw new InvalidOperationException(
                    $"Stem set is missing required stems: {string.Join(", ", missing)}.");

            Status = StemSetStatus.Ready;
            CompletedAt = DateTime.UtcNow;
        }

        public void MarkFailed(string errorMessage)
        {
            Status = StemSetStatus.Failed;
            ErrorMessage = errorMessage;
            CompletedAt = DateTime.UtcNow;
        }

        public void Activate()
        {
            if (Status != StemSetStatus.Ready)
                throw new InvalidOperationException("Only ready stem sets can be activated.");

            IsActive = true;
        }

        public void Deactivate()
        {
            IsActive = false;
        }

        private IReadOnlyCollection<StemType> GetRequiredStemTypes()
        {
            if (StemProfile.Equals("two-stem-vocals", StringComparison.OrdinalIgnoreCase) ||
                StemProfile.Equals("vocals", StringComparison.OrdinalIgnoreCase))
            {
                return
                [
                    StemType.Vocals,
                    StemType.Instrumental
                ];
            }

            return
            [
                StemType.Vocals,
                StemType.Drums,
                StemType.Bass,
                StemType.Other
            ];
        }
    }
}
