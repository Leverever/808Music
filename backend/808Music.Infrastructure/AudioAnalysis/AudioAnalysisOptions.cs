namespace _808Music.Infrastructure.AudioAnalysis;

public sealed class AudioAnalysisOptions
{
    public const string SectionName = "AudioAnalysis";

    public string DefaultProvider { get; set; } = "essentia";
    public string DefaultModelName { get; set; } = "mtg-jamendo-discogs-effnet";
    public string DefaultModelVersion { get; set; } = "1";
    public string QueueName { get; set; } = "ml.audio.analysis";
    public string RoutingKey { get; set; } = "ml.audio.analyze";
}
