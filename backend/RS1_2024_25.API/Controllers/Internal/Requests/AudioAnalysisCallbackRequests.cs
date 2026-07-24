namespace RS1_2024_25.API.Controllers.Internal.Requests;

public sealed class CompleteAudioAnalysisRequest
{
    public int TrackId { get; set; }
    public string EmbeddingModel { get; set; } = string.Empty;
    public List<double> Embedding { get; set; } = [];
    public List<AudioAnalysisTagRequest> Tags { get; set; } = [];
}

public sealed class AudioAnalysisTagRequest
{
    public string Namespace { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public decimal Score { get; set; }
    public string ModelName { get; set; } = string.Empty;
}

public sealed class FailAudioAnalysisRequest
{
    public string ErrorMessage { get; set; } = string.Empty;
}
