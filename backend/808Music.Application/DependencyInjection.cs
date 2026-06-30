using _808Music.Application.Ai;
using _808Music.Application.Recommendations;
using _808Music.Application.Stems;
using _808Music.Application.Tracks;
using Microsoft.Extensions.DependencyInjection;

namespace _808Music.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<IExtractTrackFeaturesHandler, ExtractTrackFeaturesHandler>();
        services.AddScoped<IGetTrackRecommendationsHandler, GetTrackRecommendationsHandler>();
        services.AddScoped<ISeparateTrackStemsHandler, SeparateTrackStemsHandler>();
        services.AddScoped<IGetTrackStemsHandler, GetTrackStemsHandler>();
        services.AddScoped<IGenerateAiPlaylistHandler, GenerateAiPlaylistHandler>();

        return services;
    }
}
