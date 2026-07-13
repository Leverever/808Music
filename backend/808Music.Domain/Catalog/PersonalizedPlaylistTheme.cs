namespace _808Music.Domain.Catalog;

public class PersonalizedPlaylistTheme
{
    private readonly List<PersonalizedPlaylistThemeLabel> _labels = new();

    private PersonalizedPlaylistTheme()
    {
        ThemeKey = string.Empty;
        Name = string.Empty;
        Description = string.Empty;
    }

    public PersonalizedPlaylistTheme(
        string themeKey,
        string name,
        string description,
        int trackCount,
        int sortOrder,
        DateTime createdAt)
    {
        ThemeKey = NormalizeRequired(themeKey, nameof(themeKey), 100);
        Name = NormalizeRequired(name, nameof(name), 200);
        Description = NormalizeOptional(description, 500);
        TrackCount = ValidateTrackCount(trackCount);
        SortOrder = sortOrder;
        IsActive = true;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public Guid Id { get; private set; } = Guid.NewGuid();
    public string ThemeKey { get; private set; }
    public string Name { get; private set; }
    public string Description { get; private set; }
    public bool IsActive { get; private set; }
    public int TrackCount { get; private set; }
    public int SortOrder { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public IReadOnlyCollection<PersonalizedPlaylistThemeLabel> Labels => _labels.AsReadOnly();

    public void Update(
        string name,
        string description,
        int trackCount,
        int sortOrder,
        DateTime updatedAt)
    {
        Name = NormalizeRequired(name, nameof(name), 200);
        Description = NormalizeOptional(description, 500);
        TrackCount = ValidateTrackCount(trackCount);
        SortOrder = sortOrder;
        UpdatedAt = updatedAt;
    }

    public void SetActive(bool isActive, DateTime updatedAt)
    {
        IsActive = isActive;
        UpdatedAt = updatedAt;
    }

    private static int ValidateTrackCount(int trackCount)
    {
        if (trackCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(trackCount), "Track count must be positive.");
        }

        return trackCount;
    }

    private static string NormalizeRequired(string value, string parameterName, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value is required.", parameterName);
        }

        return NormalizeOptional(value, maxLength);
    }

    private static string NormalizeOptional(string? value, int maxLength)
    {
        var normalized = value?.Trim() ?? string.Empty;

        return normalized.Length <= maxLength
            ? normalized
            : normalized[..maxLength];
    }
}
