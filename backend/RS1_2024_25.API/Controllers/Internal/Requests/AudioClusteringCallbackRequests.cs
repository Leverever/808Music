namespace RS1_2024_25.API.Controllers.Internal.Requests;

public sealed class CompleteAudioClusteringRequest
{
    public string AlgorithmName { get; set; } = string.Empty;
    public string EmbeddingSource { get; set; } = string.Empty;
    public List<AudioClusterRequest> Clusters { get; set; } = [];
    public List<TrackClusterAssignmentRequest> Assignments { get; set; } = [];
}

public sealed class AudioClusterRequest
{
    public string ClusterKey { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Size { get; set; }
    public List<AudioClusterTagRequest> TopTags { get; set; } = [];
}

public sealed class AudioClusterTagRequest
{
    public string Namespace { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public decimal Score { get; set; }
}

public sealed class TrackClusterAssignmentRequest
{
    public int TrackId { get; set; }
    public string ClusterKey { get; set; } = string.Empty;
    public bool IsNoise { get; set; }
    public decimal? DistanceToCenter { get; set; }
    public decimal? MembershipScore { get; set; }
}

public sealed class FailAudioClusteringRequest
{
    public string ErrorMessage { get; set; } = string.Empty;
}
