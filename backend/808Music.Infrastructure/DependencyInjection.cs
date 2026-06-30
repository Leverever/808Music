using _808Music.Application.Abstractions;
using _808Music.Infrastructure.Ai;
using _808Music.Infrastructure.Audio;
using _808Music.Infrastructure.Recommendations;
using _808Music.Infrastructure.Stems;
using _808Music.Infrastructure.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace _808Music.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services)
    {
        services.AddScoped<IMediaStorage, LocalMediaStorage>();
        services.AddScoped<IAudioFeatureExtractor, DeterministicAudioFeatureExtractor>();
        services.AddScoped<IRecommendationService, DeterministicRecommendationService>();
        services.AddScoped<IStemSeparationService, ManifestStemSeparationService>();
        services.AddScoped<IAiPlaylistGenerator, PromptPlaylistGenerator>();

        return services;
    }
}
