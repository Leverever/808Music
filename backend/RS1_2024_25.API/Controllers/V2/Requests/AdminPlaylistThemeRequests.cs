using _808Music.Domain.Enums;
using System.Text.Json.Serialization;

namespace RS1_2024_25.API.Controllers.V2.Requests;

public sealed class CreateAdminPlaylistThemeRequest
{
    public string ThemeKey { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public int TrackCount { get; set; } = 25;
    public int SortOrder { get; set; }
    public IReadOnlyList<AdminPlaylistThemeLabelRequest> Labels { get; set; } = [];
}

public sealed class UpdateAdminPlaylistThemeRequest
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public int TrackCount { get; set; } = 25;
    public int SortOrder { get; set; }
    public IReadOnlyList<AdminPlaylistThemeLabelRequest> Labels { get; set; } = [];
}

public sealed class AdminPlaylistThemeLabelRequest
{
    public string Label { get; set; } = string.Empty;

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public PersonalizedPlaylistThemeLabelPolarity Polarity { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public PersonalizedPlaylistThemeLabelSource Source { get; set; }

    public string? TagNamespace { get; set; }

    public decimal Weight { get; set; } = 1m;
}

public sealed class SetAdminPlaylistThemeActiveRequest
{
    public bool IsActive { get; set; }
}
