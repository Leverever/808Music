namespace RS1_2024_25.API.Controllers.V2.Requests;

public sealed class RecordUserTrackInteractionRequest
{
    public int TrackId { get; set; }
    public string InteractionType { get; set; } = string.Empty;
    public long? PlayedMs { get; set; }
    public long? TrackDurationMs { get; set; }
    public string? ContextType { get; set; }
    public string? ClientEventId { get; set; }
    public DateTime? OccurredAt { get; set; }
}
