using System.ComponentModel.DataAnnotations;

namespace RS1_2024_25.API.Controllers.V2.Requests;

public sealed class ListArtistTracksRequest : IValidatableObject
{
    private static readonly string[] SupportedSortFields =
        ["id", "title", "primaryRelease", "duration", "streams"];

    [Range(1, int.MaxValue)]
    public int PageNumber { get; set; } = 1;

    [Range(1, 100)]
    public int PageSize { get; set; } = 20;

    public string? Title { get; set; }

    public string? PrimaryReleaseTitle { get; set; }

    [Range(0, int.MaxValue)]
    public int? MinStreams { get; set; }

    [Range(0, int.MaxValue)]
    public int? MaxStreams { get; set; }

    [Range(0, int.MaxValue)]
    public int? MinDurationSeconds { get; set; }

    [Range(0, int.MaxValue)]
    public int? MaxDurationSeconds { get; set; }

    public string? SortBy { get; set; }

    public string? SortDirection { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (MinStreams > MaxStreams)
        {
            yield return new ValidationResult(
                "Minimum streams cannot exceed maximum streams.",
                [nameof(MinStreams), nameof(MaxStreams)]);
        }

        if (MinDurationSeconds > MaxDurationSeconds)
        {
            yield return new ValidationResult(
                "Minimum duration cannot exceed maximum duration.",
                [nameof(MinDurationSeconds), nameof(MaxDurationSeconds)]);
        }

        if (!string.IsNullOrWhiteSpace(SortBy) &&
            !SupportedSortFields.Contains(SortBy, StringComparer.OrdinalIgnoreCase))
        {
            yield return new ValidationResult(
                "Unsupported track sort field.",
                [nameof(SortBy)]);
        }

        if (!string.IsNullOrWhiteSpace(SortDirection) &&
            !string.Equals(SortDirection, "asc", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(SortDirection, "desc", StringComparison.OrdinalIgnoreCase))
        {
            yield return new ValidationResult(
                "Sort direction must be either asc or desc.",
                [nameof(SortDirection)]);
        }
    }
}

public sealed class SearchArtistsRequest
{
    public string? Query { get; set; }
    public int? ExcludeArtistId { get; set; }

    [Range(1, 25)]
    public int Limit { get; set; } = 10;
}

public sealed class SearchReleasesRequest
{
    [Range(1, int.MaxValue)]
    public int ArtistId { get; set; }

    public int? ExcludeTrackId { get; set; }
    public string? Title { get; set; }

    [Range(1, int.MaxValue)]
    public int PageNumber { get; set; } = 1;

    [Range(1, 50)]
    public int PageSize { get; set; } = 10;
}

public sealed class ReplaceFeaturedArtistsRequest
{
    public List<FeaturedArtistRequest> Artists { get; set; } = [];
}

public sealed class FeaturedArtistRequest
{
    [Range(1, int.MaxValue)]
    public int ArtistId { get; set; }

    public bool ShowOnProfile { get; set; } = true;
}

public sealed class ReplaceTrackReleasesRequest
{
    public List<TrackReleaseAssociationRequest> Releases { get; set; } = [];
}

public sealed class TrackReleaseAssociationRequest
{
    [Range(1, int.MaxValue)]
    public int ReleaseId { get; set; }

    [Range(1, int.MaxValue)]
    public int DiscNumber { get; set; } = 1;

    [Range(1, int.MaxValue)]
    public int TrackNumber { get; set; }

    [StringLength(200)]
    public string? TitleOverride { get; set; }

    public bool IsPrimaryRelease { get; set; }
}
