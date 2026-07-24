using _808Music.Domain.Enums;

namespace _808Music.Application.Tracks;

public interface ILegacyTrackMasterMigrationService
{
    Task<LegacyTrackMigrationDiscoveryResult> DiscoverAsync(
        bool dryRun,
        CancellationToken cancellationToken = default);

    Task<LegacyTrackMigrationRunResult> RunBatchAsync(
        int batchSize,
        bool queueAnalysis,
        bool queueStems,
        CancellationToken cancellationToken = default);

    Task<LegacyTrackMigrationRunResult> RetryAsync(
        int trackId,
        bool queueAnalysis,
        bool queueStems,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LegacyTrackMigrationStatusItem>> GetStatusAsync(
        TrackMasterMigrationStatus? status,
        int take,
        CancellationToken cancellationToken = default);

    Task<LegacyTrackMigrationCleanupResult> CleanupAsync(
        int retentionDays,
        int batchSize,
        CancellationToken cancellationToken = default);
}

public sealed record LegacyTrackMigrationDiscoveryResult(
    bool DryRun,
    int TotalTrackCount,
    int AlreadyObjectStorageCount,
    int ExistingMigrationCount,
    int CandidateCount,
    int CreatedCount,
    int MissingFileCount,
    int UnsupportedFileCount);

public sealed record LegacyTrackMigrationRunResult(
    int RequestedCount,
    int ProcessedCount,
    int CompletedCount,
    int WaitingForJobsCount,
    int FailedCount,
    IReadOnlyList<LegacyTrackMigrationStatusItem> Items);

public sealed record LegacyTrackMigrationStatusItem(
    Guid MigrationId,
    int TrackId,
    string LegacyRelativePath,
    string? TargetObjectKey,
    string Status,
    Guid? AnalysisId,
    string? AnalysisStatus,
    Guid? StemSetId,
    string? StemStatus,
    int AttemptCount,
    string? LastError,
    DateTime UpdatedAt,
    DateTime? CompletedAt,
    DateTime? LegacyDeletedAt);

public sealed record LegacyTrackMigrationCleanupResult(
    int CandidateCount,
    int DeletedCount,
    int FailedCount);
