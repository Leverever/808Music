namespace RS1_2024_25.API.Services;

public sealed class CleanArchitectureBackgroundServiceOptions
{
    public const string SectionName = "CleanBackgroundTasks";

    public bool Enabled { get; set; } = true;
    public int PollIntervalSeconds { get; set; } = 30;
}
