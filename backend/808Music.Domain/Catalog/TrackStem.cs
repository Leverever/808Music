using _808Music.Domain.Enums;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace _808Music.Domain.Catalog
{
    public class TrackStem
    {
        [Key]
        public Guid Id { get; set; }
        public Guid StemSetId { get; private set; }

        public StemType StemType { get; private set; }

        public string ObjectKey { get; private set; } = string.Empty;
        public string ContentType { get; private set; } = string.Empty;

        public long SizeBytes { get; private set; }

        public int? DurationMs { get; private set; }
        public int? SampleRate { get; private set; }
        public int? BitrateKbps { get; private set; }

        public string? Codec { get; private set; }
        public int? Channels { get; private set; }
        public string? ChecksumSha256 { get; private set; }

        public DateTime CreatedAt { get; private set; }

        private TrackStem() { } // EF Core

        public TrackStem(
            Guid stemSetId,
            StemType stemType,
            string storageProvider,
            string objectKey,
            string contentType,
            long sizeBytes,
            int? durationMs,
            int? sampleRate,
            int? bitrateKbps,
            string? codec,
            int? channels,
            string? checksumSha256)
        {
            Id = Guid.NewGuid();
            StemSetId = stemSetId;
            StemType = stemType;
            ObjectKey = objectKey;
            ContentType = contentType;
            SizeBytes = sizeBytes;
            DurationMs = durationMs;
            SampleRate = sampleRate;
            BitrateKbps = bitrateKbps;
            Codec = codec;
            Channels = channels;
            ChecksumSha256 = checksumSha256;
            CreatedAt = DateTime.UtcNow;
        }
    }
}
