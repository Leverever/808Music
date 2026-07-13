using _808Music.Domain.Enums;

namespace _808Music.Domain.Catalog;

public class TrackMasterMigration
{
    private TrackMasterMigration()
    {
        LegacyRelativePath = string.Empty;
    }

    public TrackMasterMigration(int trackId, string legacyRelativePath, DateTime createdAt)
    {
        if (trackId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(trackId));
        }

        if (string.IsNullOrWhiteSpace(legacyRelativePath))
        {
            throw new ArgumentException("Legacy relative path is required.", nameof(legacyRelativePath));
        }

        Id = Guid.NewGuid();
        TrackId = trackId;
        LegacyRelativePath = Normalize(legacyRelativePath, 500);
        Status = TrackMasterMigrationStatus.Pending;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public Guid Id { get; private set; }
    public int TrackId { get; private set; }
    public string LegacyRelativePath { get; private set; }
    public string? TargetObjectKey { get; private set; }
    public long? SourceSizeBytes { get; private set; }
    public string? SourceChecksumSha256 { get; private set; }
    public string? ContentType { get; private set; }
    public TrackMasterMigrationStatus Status { get; private set; }
    public Guid? AnalysisId { get; private set; }
    public Guid? StemSetId { get; private set; }
    public int AttemptCount { get; private set; }
    public string? LastError { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public DateTime? LegacyDeletedAt { get; private set; }

    public void MarkUploading(
        string targetObjectKey,
        long sourceSizeBytes,
        string checksumSha256,
        string contentType,
        DateTime now)
    {
        TargetObjectKey = Normalize(targetObjectKey, 500);
        SourceSizeBytes = sourceSizeBytes;
        SourceChecksumSha256 = Normalize(checksumSha256, 64);
        ContentType = Normalize(contentType, 100);
        Status = TrackMasterMigrationStatus.Uploading;
        AttemptCount++;
        LastError = null;
        UpdatedAt = now;
    }

    public void MarkObjectVerified(DateTime now)
    {
        Status = TrackMasterMigrationStatus.ObjectVerified;
        LastError = null;
        UpdatedAt = now;
    }

    public void MarkDatabaseUpdated(DateTime now)
    {
        Status = TrackMasterMigrationStatus.DatabaseUpdated;
        LastError = null;
        UpdatedAt = now;
    }

    public void RecordJobs(Guid? analysisId, Guid? stemSetId, DateTime now)
    {
        AnalysisId = analysisId;
        StemSetId = stemSetId;
        Status = TrackMasterMigrationStatus.JobsQueued;
        LastError = null;
        UpdatedAt = now;
    }

    public void MarkCompleted(DateTime now)
    {
        Status = TrackMasterMigrationStatus.Completed;
        LastError = null;
        CompletedAt = now;
        UpdatedAt = now;
    }

    public void MarkFailed(string error, DateTime now)
    {
        Status = TrackMasterMigrationStatus.Failed;
        LastError = Normalize(error, 2_000);
        UpdatedAt = now;
    }

    public void PrepareRetry(DateTime now)
    {
        Status = TrackMasterMigrationStatus.Pending;
        LastError = null;
        CompletedAt = null;
        UpdatedAt = now;
    }

    public void MarkLegacyDeleted(DateTime now)
    {
        LegacyDeletedAt = now;
        UpdatedAt = now;
    }

    private static string Normalize(string value, int maxLength)
    {
        var normalized = value.Trim();
        return normalized.Length <= maxLength ? normalized : normalized[..maxLength];
    }
}
