namespace _808Music.Domain.Enums;

public enum TrackMasterMigrationStatus
{
    Pending = 1,
    Uploading = 2,
    ObjectVerified = 3,
    DatabaseUpdated = 4,
    JobsQueued = 5,
    Completed = 6,
    Failed = 7
}
