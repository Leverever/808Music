namespace RS1_2024_25.API.Controllers.V2.Requests;

public sealed class DiscoverTrackMasterMigrationsRequest
{
    public bool DryRun { get; set; } = true;
}

public sealed class RunTrackMasterMigrationsRequest
{
    public int BatchSize { get; set; } = 10;
    public bool QueueAnalysis { get; set; } = true;
    public bool QueueStems { get; set; } = true;
}

public sealed class RetryTrackMasterMigrationRequest
{
    public bool QueueAnalysis { get; set; } = true;
    public bool QueueStems { get; set; } = true;
}

public sealed class CleanupTrackMasterMigrationsRequest
{
    public int RetentionDays { get; set; } = 14;
    public int BatchSize { get; set; } = 100;
}
