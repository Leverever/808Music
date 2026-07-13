namespace _808Music.Application.Releases;

public static class ReleaseTrackAssociationValidation
{
    public static void ValidatePosition(int discNumber, int trackNumber)
    {
        if (discNumber < 1)
        {
            throw new InvalidOperationException("Disc number must be greater than zero.");
        }

        if (trackNumber < 1)
        {
            throw new InvalidOperationException("Track number must be greater than zero.");
        }
    }

    public static string? NormalizeTitleOverride(string? titleOverride)
    {
        var normalized = string.IsNullOrWhiteSpace(titleOverride) ? null : titleOverride.Trim();
        if (normalized?.Length > 200)
        {
            throw new InvalidOperationException("A release title override cannot exceed 200 characters.");
        }

        return normalized;
    }
}
