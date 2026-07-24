namespace _808Music.Infrastructure.TrackMigration;

public sealed class LegacyTrackMigrationOptions
{
    public const string SectionName = "LegacyTrackMigration";

    public string LegacyTrackRoot { get; set; } = "TrackFiles";
    public string ObjectKeyPrefix { get; set; } = "tracks";
    public int DefaultBatchSize { get; set; } = 10;
    public int MaxBatchSize { get; set; } = 100;
    public int DefaultRetentionDays { get; set; } = 14;
}
