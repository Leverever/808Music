namespace RS1_2024_25.API.Controllers.V2.Requests;

public sealed class RunAudioClusteringRequest
{
    public string? AlgorithmName { get; set; }
    public string? EmbeddingSource { get; set; }
    public string? ParametersJson { get; set; }
}
