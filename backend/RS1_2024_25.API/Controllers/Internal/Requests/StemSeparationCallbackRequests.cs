namespace RS1_2024_25.API.Controllers.Internal.Requests;

public sealed class CompleteStemSeparationRequest
{
    public List<CompletedStemRequest> Stems { get; set; } = [];
}

public sealed class CompletedStemRequest
{
    public string StemType { get; set; } = string.Empty;
    public string ObjectKey { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public int? DurationMs { get; set; }
    public int? SampleRate { get; set; }
    public int? BitrateKbps { get; set; }
    public string? Codec { get; set; }
    public int? Channels { get; set; }
    public string? ChecksumSha256 { get; set; }
}

public sealed class FailStemSeparationRequest
{
    public string ErrorMessage { get; set; } = string.Empty;
}
