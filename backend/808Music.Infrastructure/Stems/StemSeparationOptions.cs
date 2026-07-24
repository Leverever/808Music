namespace _808Music.Infrastructure.Stems;

public sealed class StemSeparationOptions
{
    public const string SectionName = "StemSeparation";

    public string DefaultProvider { get; set; } = "demucs";
    public string DefaultModelName { get; set; } = "htdemucs";
    public string DefaultModelVersion { get; set; } = "v4";
    public string DefaultStemProfile { get; set; } = "four-stem";
    public string QueueName { get; set; } = "ml.stems.separation";
    public string RoutingKey { get; set; } = "ml.stems.separate";
}
