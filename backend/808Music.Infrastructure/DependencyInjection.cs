using _808Music.Application.Abstractions;
using _808Music.Application.AudioAnalysis;
using _808Music.Application.AudioClustering;
using _808Music.Application.Common.Messaging;
using _808Music.Application.Common.Persistence;
using _808Music.Application.Common.Scheduling;
using _808Music.Application.Common.Search;
using _808Music.Application.Stems;
using _808Music.Application.Tracks;
using _808Music.Domain.Static;
using _808Music.Infrastructure.Ai;
using _808Music.Infrastructure.AudioAnalysis;
using _808Music.Infrastructure.Audio;
using _808Music.Infrastructure.AudioClustering;
using _808Music.Infrastructure.AutomaticPlaylists;
using _808Music.Infrastructure.BackgroundTasks;
using _808Music.Infrastructure.Messaging;
using _808Music.Infrastructure.Personalization;
using _808Music.Infrastructure.Persistence;
using _808Music.Infrastructure.Persistence.Repositories;
using _808Music.Infrastructure.Recommendations;
using _808Music.Infrastructure.Stems;
using _808Music.Infrastructure.Storage;
using _808Music.Infrastructure.TrackMigration;
using Amazon.S3;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace _808Music.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IAudioFeatureExtractor, DeterministicAudioFeatureExtractor>();
        services.AddScoped<IAudioAnalysisService, QueuedAudioAnalysisService>();
        services.AddScoped<IAudioAnalysisJobQueue, AudioAnalysisJobQueue>();
        services.AddScoped<IAudioClusteringService, QueuedAudioClusteringService>();
        services.AddScoped<IAudioClusteringJobQueue, AudioClusteringJobQueue>();
        services.AddScoped<IAutomaticPlaylistGenerationService, PersonalizedAutomaticPlaylistGenerationService>();
        services.AddScoped<IPersonalizedPlaylistThemeProvider, PersonalizedPlaylistThemeProvider>();
        services.AddScoped<IUserMusicProfileService, UserMusicProfileService>();
        services.AddScoped<IPersonalizedRecommendationService, PersonalizedRecommendationService>();
        services.AddScoped<ILegacyTrackMasterMigrationService, LegacyTrackMasterMigrationService>();
        services.AddScoped<IRecurringApplicationTask, AudioClusteringRecurringTask>();
        services.AddScoped<IRecurringApplicationTask, DailyAutomaticPlaylistRecurringTask>();
        services.AddScoped<IRecurringApplicationTask, DailyUserMusicProfileCacheRecurringTask>();
        services.AddScoped<IAudioMetadataReader, NAudioMetadataReader>();
        services.AddScoped<IRecommendationService, DeterministicRecommendationService>();
        services.AddScoped<IStemSeparationService, QueuedStemSeparationService>();
        services.AddScoped<IStemSeparationJobQueue, StemSeparationJobQueue>();
        services.AddScoped<IMessagePublisher, RabbitMqMessagePublisher>();
        services.AddScoped<IAiPlaylistGenerator, PromptPlaylistGenerator>();
        services.Configure<RabbitMqOptions>(
            configuration.GetSection(RabbitMqOptions.SectionName));
        services.Configure<StemSeparationOptions>(
            configuration.GetSection(StemSeparationOptions.SectionName));
        services.Configure<AudioAnalysisOptions>(
            configuration.GetSection(AudioAnalysisOptions.SectionName));
        services.Configure<AudioClusteringOptions>(
            configuration.GetSection(AudioClusteringOptions.SectionName));
        services.Configure<AutomaticPlaylistOptions>(
            configuration.GetSection(AutomaticPlaylistOptions.SectionName));
        services.Configure<UserMusicProfileOptions>(
            configuration.GetSection(UserMusicProfileOptions.SectionName));
        services.Configure<PersonalizedRecommendationOptions>(
            configuration.GetSection(PersonalizedRecommendationOptions.SectionName));
        services.Configure<LegacyTrackMigrationOptions>(
            configuration.GetSection(LegacyTrackMigrationOptions.SectionName));
        services.Configure<S3Options>(
            configuration.GetSection(S3Options.SectionName));

        services.AddSingleton<IAmazonS3>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<S3Options>>().Value;

            var s3Config = new AmazonS3Config
            {
                ServiceURL = options.ServiceUrl,
                ForcePathStyle = options.ForcePathStyle,
                AuthenticationRegion = options.Region,
                UseHttp = IsHttpUrl(options.ServiceUrl)
            };

            return new AmazonS3Client(
                options.AccessKey,
                options.SecretKey,
                s3Config);
        });

        services.AddScoped<IMediaStorage, S3MediaStorage>();

        return services;
    }

    private static bool IsHttpUrl(string url)
    {
        return Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
            uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase);
    }

     public static IServiceCollection AddEfCrudPersistence<TDbContext>(
        this IServiceCollection services)
        where TDbContext : DbContext
    {
        services.AddScoped<DbContext>(sp => sp.GetRequiredService<TDbContext>());
        services.AddScoped(typeof(IRepository<,>), typeof(EfRepository<,>));
        services.AddScoped(typeof(ISearchRepository<,,>), typeof(EfRepository<,,>));
        services.AddScoped<ISearchRepository<Genre, int, GenreSearchObject>, GenreRepository>();
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();

        return services;
    }
}
