using _808Music.Domain.Catalog;
using _808Music.Domain.Enums;
using RS1_2024_25.API.Services;

namespace RS1_2024_25.Tests.Application;

public sealed class TrackMasterMigrationTests
{
    [Fact]
    public void Migration_tracks_verified_upload_jobs_and_cleanup()
    {
        var createdAt = new DateTime(2026, 7, 12, 10, 0, 0, DateTimeKind.Utc);
        var migration = new TrackMasterMigration(42, "legacy-song.mp3", createdAt);

        migration.MarkUploading(
            "tracks/42/masters/checksum.mp3",
            1_024,
            new string('a', 64),
            "audio/mpeg",
            createdAt.AddMinutes(1));
        migration.MarkObjectVerified(createdAt.AddMinutes(2));
        migration.MarkDatabaseUpdated(createdAt.AddMinutes(3));

        var analysisId = Guid.NewGuid();
        var stemSetId = Guid.NewGuid();
        migration.RecordJobs(analysisId, stemSetId, createdAt.AddMinutes(4));
        migration.MarkCompleted(createdAt.AddMinutes(5));
        migration.MarkLegacyDeleted(createdAt.AddDays(14));

        Assert.Equal(TrackMasterMigrationStatus.Completed, migration.Status);
        Assert.Equal(1, migration.AttemptCount);
        Assert.Equal(analysisId, migration.AnalysisId);
        Assert.Equal(stemSetId, migration.StemSetId);
        Assert.Equal(createdAt.AddMinutes(5), migration.CompletedAt);
        Assert.Equal(createdAt.AddDays(14), migration.LegacyDeletedAt);
        Assert.Null(migration.LastError);
    }

    [Fact]
    public void Retry_clears_failure_without_losing_migration_identity()
    {
        var now = DateTime.UtcNow;
        var migration = new TrackMasterMigration(7, "old.wav", now);
        migration.MarkFailed("upload failed", now.AddMinutes(1));

        migration.PrepareRetry(now.AddMinutes(2));

        Assert.Equal(TrackMasterMigrationStatus.Pending, migration.Status);
        Assert.Null(migration.LastError);
        Assert.Null(migration.CompletedAt);
        Assert.Equal(7, migration.TrackId);
        Assert.Equal("old.wav", migration.LegacyRelativePath);
    }

    [Fact]
    public void Recurring_task_coordinator_allows_only_one_run_per_task()
    {
        var coordinator = new RecurringTaskExecutionCoordinator();

        Assert.True(coordinator.TryBegin("daily-automatic-playlists"));
        Assert.False(coordinator.TryBegin("DAILY-AUTOMATIC-PLAYLISTS"));
        Assert.True(coordinator.IsRunning("daily-automatic-playlists"));

        coordinator.End("daily-automatic-playlists");

        Assert.False(coordinator.IsRunning("daily-automatic-playlists"));
        Assert.True(coordinator.TryBegin("daily-automatic-playlists"));
    }
}
