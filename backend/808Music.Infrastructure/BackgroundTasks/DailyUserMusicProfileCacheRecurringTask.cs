using _808Music.Application.Abstractions;
using _808Music.Application.Common.Scheduling;
using _808Music.Infrastructure.Personalization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace _808Music.Infrastructure.BackgroundTasks;

public sealed class DailyUserMusicProfileCacheRecurringTask : IRecurringApplicationTask
{
    private readonly IUserMusicProfileService _profileService;
    private readonly UserMusicProfileOptions _options;
    private readonly ILogger<DailyUserMusicProfileCacheRecurringTask> _logger;

    public DailyUserMusicProfileCacheRecurringTask(
        IUserMusicProfileService profileService,
        IOptions<UserMusicProfileOptions> options,
        ILogger<DailyUserMusicProfileCacheRecurringTask> logger)
    {
        _profileService = profileService;
        _options = options.Value;
        _logger = logger;
    }

    public string Name => "daily-user-music-profile-cache";
    public string CronExpression => _options.RecurringCronExpression;
    public bool IsEnabled => _options.RecurringEnabled;

    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var result = await _profileService.RefreshActiveUserProfilesAsync(
            DateOnly.FromDateTime(DateTime.UtcNow),
            cancellationToken);

        _logger.LogInformation(
            "Refreshed {ProfileCount} daily user music profiles for {ProfileDate}",
            result.RefreshedUserCount,
            result.ProfileDate);
    }
}
