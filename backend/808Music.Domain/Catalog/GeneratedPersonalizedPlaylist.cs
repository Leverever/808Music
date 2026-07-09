namespace _808Music.Domain.Catalog;

public class GeneratedPersonalizedPlaylist
{
    private readonly List<GeneratedPersonalizedPlaylistTrack> _tracks = new();

    private GeneratedPersonalizedPlaylist()
    {
        ThemeKey = string.Empty;
        Name = string.Empty;
        Description = string.Empty;
    }

    public GeneratedPersonalizedPlaylist(
        int userId,
        string themeKey,
        string name,
        string description,
        DateOnly playlistDate,
        DateTime createdAt)
    {
        if (userId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(userId), "User id must be positive.");
        }

        UserId = userId;
        ThemeKey = NormalizeRequired(themeKey, nameof(themeKey), 100);
        Name = NormalizeRequired(name, nameof(name), 200);
        Description = NormalizeOptional(description, 500);
        PlaylistDate = playlistDate;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; } = Guid.NewGuid();
    public int UserId { get; private set; }
    public string ThemeKey { get; private set; }
    public string Name { get; private set; }
    public string Description { get; private set; }
    public DateOnly PlaylistDate { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public IReadOnlyCollection<GeneratedPersonalizedPlaylistTrack> Tracks => _tracks.AsReadOnly();

    public void RefreshMetadata(string name, string description)
    {
        Name = NormalizeRequired(name, nameof(name), 200);
        Description = NormalizeOptional(description, 500);
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
