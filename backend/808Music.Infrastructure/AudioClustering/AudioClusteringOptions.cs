namespace _808Music.Infrastructure.AudioClustering;

public sealed class AudioClusteringOptions
{
    public const string SectionName = "AudioClustering";

    public string DefaultAlgorithmName { get; set; } = "kmeans";
    public string DefaultEmbeddingSource { get; set; } = "essentia";
    public string DefaultParametersJson { get; set; } = "{\"nClusters\":12,\"randomState\":42}";
    public string QueueName { get; set; } = "ml.audio.clustering";
    public string RoutingKey { get; set; } = "ml.audio.cluster";
    public bool RecurringEnabled { get; set; } = false;
    public string RecurringCronExpression { get; set; } = "0 2 * * *";
}
