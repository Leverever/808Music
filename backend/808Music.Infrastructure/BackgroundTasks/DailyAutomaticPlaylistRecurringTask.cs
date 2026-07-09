using _808Music.Application.AutomaticPlaylists;
using _808Music.Application.Common.Scheduling;
using _808Music.Infrastructure.AutomaticPlaylists;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace _808Music.Infrastructure.BackgroundTasks;

public sealed class DailyAutomaticPlaylistRecurringTask : IRecurringApplicationTask
{
    private readonly IGenerateDailyAutomaticPlaylistsHandler _handler;
    private readonly AutomaticPlaylistOptions _options;
    private readonly ILogger<DailyAutomaticPlaylistRecurringTask> _logger;

    public DailyAutomaticPlaylistRecurringTask(
        IGenerateDailyAutomaticPlaylistsHandler handler,
        IOptions<AutomaticPlaylistOptions> options,
        ILogger<DailyAutomaticPlaylistRecurringTask> logger)
    {
        _handler = handler;
        _options = options.Value;
        _logger = logger;
    }

    public string Name => "daily-automatic-playlists";
    public string CronExpression => _options.RecurringCronExpression;
    public bool IsEnabled => _options.RecurringEnabled;

    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var result = await _handler.Handle(
            new GenerateDailyAutomaticPlaylistsCommand(DateOnly.FromDateTime(DateTime.UtcNow)),
            cancellationToken);

        _logger.LogInformation(
            "Generated {PlaylistCount} automatic playlists for {PlaylistDate}",
            result.GeneratedPlaylistCount,
            result.PlaylistDate);
    }
}
