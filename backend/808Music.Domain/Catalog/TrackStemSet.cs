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
        public Guid TrackId { get; private set; }

        public StemSetSource Source { get; private set; }
        public StemSetStatus Status { get; private set; }

        public string? ModelName { get; private set; }
        public string? ModelVersion { get; private set; }

        public bool IsActive { get; private set; }

        public DateTime CreatedAt { get; private set; }
        public DateTime? StartedAt { get; private set; }
        public DateTime? CompletedAt { get; private set; }

        public string? ErrorMessage { get; private set; }

        private readonly List<TrackStem> _stems = new();
        public IReadOnlyCollection<TrackStem> Stems => _stems.AsReadOnly();

        private TrackStemSet() { } // EF Core

        public TrackStemSet(
            Guid trackId,
            StemSetSource source,
            Guid? requestedByUserId,
            string? modelName,
            string? modelVersion)
        {
            Id = Guid.NewGuid();
            TrackId = trackId;
            Source = source;
            ModelName = modelName;
            ModelVersion = modelVersion;
            Status = StemSetStatus.Pending;
            CreatedAt = DateTime.UtcNow;
        }

        public void MarkProcessing()
        {
            if (Status != StemSetStatus.Pending)
                throw new Exception("Only pending stem sets can start processing.");

            Status = StemSetStatus.Processing;
            StartedAt = DateTime.UtcNow;
        }

        public void AddStem(TrackStem stem)
        {
            if (Status == StemSetStatus.Ready)
                throw new Exception("Cannot add stems to a ready stem set.");

            if (_stems.Any(x => x.StemType == stem.StemType))
                throw new Exception("Stem type already exists in this stem set.");

            _stems.Add(stem);
        }

        public void MarkReady()
        {
            var required = new[]
            {
            StemType.Vocals,
            StemType.Drums,
            StemType.Bass,
            StemType.Other
        };

            var existing = _stems.Select(x => x.StemType).ToHashSet();

            if (!required.All(existing.Contains))
                throw new Exception("Stem set is missing required stems.");

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
                throw new Exception("Only ready stem sets can be activated.");

            IsActive = true;
        }

        public void Deactivate()
        {
            IsActive = false;
        }
    }
}
