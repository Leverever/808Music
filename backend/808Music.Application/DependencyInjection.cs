using _808Music.Application.Ai;
using _808Music.Application.AudioAnalysis;
using _808Music.Application.Common.Crud.Contracts;
using _808Music.Application.Playback;
using _808Music.Application.Recommendations;
using _808Music.Application.Stems;
using _808Music.Application.Tracks;
using AutoMapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace _808Music.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddSingleton(sp =>
        {
            var loggerFactory = sp.GetService<ILoggerFactory>();

            return new MapperConfiguration(
                cfg => cfg.AddMaps(typeof(DependencyInjection).Assembly),
                loggerFactory);
        });
        services.AddSingleton<IMapper>(sp =>
            sp.GetRequiredService<MapperConfiguration>().CreateMapper(sp.GetService));

        services.AddScoped<IExtractTrackFeaturesHandler, ExtractTrackFeaturesHandler>();
        services.AddScoped<IGetTrackRecommendationsHandler, GetTrackRecommendationsHandler>();
        services.AddScoped<IUploadTrackHandler, UploadTrackHandler>();
        services.AddScoped<IUpdateTrackMetadataHandler, UpdateTrackMetadataHandler>();
        services.AddScoped<IReplaceTrackMasterHandler, ReplaceTrackMasterHandler>();
        services.AddScoped<ITrackArtistAccessQuery, TrackArtistAccessQuery>();
        services.AddScoped<IGetTrackPlaybackManifestHandler, GetTrackPlaybackManifestHandler>();
        services.AddScoped<ISeparateTrackStemsHandler, SeparateTrackStemsHandler>();
        services.AddScoped<IGetTrackStemsHandler, GetTrackStemsHandler>();
        services.AddScoped<IUploadManualStemSetHandler, UploadManualStemSetHandler>();
        services.AddScoped<IAnalyzeTrackAudioHandler, AnalyzeTrackAudioHandler>();
        services.AddScoped<IMarkAudioAnalysisProcessingHandler, MarkAudioAnalysisProcessingHandler>();
        services.AddScoped<ICompleteAudioAnalysisHandler, CompleteAudioAnalysisHandler>();
        services.AddScoped<IFailAudioAnalysisHandler, FailAudioAnalysisHandler>();
        services.AddScoped<IMarkStemSeparationProcessingHandler, MarkStemSeparationProcessingHandler>();
        services.AddScoped<ICompleteStemSeparationHandler, CompleteStemSeparationHandler>();
        services.AddScoped<IFailStemSeparationHandler, FailStemSeparationHandler>();
        services.AddScoped<IGenerateAiPlaylistHandler, GenerateAiPlaylistHandler>();
        services.AddCrudHandlersFromAssembly(typeof(DependencyInjection).Assembly);

        return services;
    }

    private static IServiceCollection AddCrudHandlersFromAssembly(
        this IServiceCollection services,
        System.Reflection.Assembly assembly)
    {
        var crudHandlerInterfaceDefinitions = new[]
        {
            typeof(IReadOnlyCrudHandler<,,>),
            typeof(ICrudHandler<,,,,>),
            typeof(ICreateHandler<,>),
            typeof(IUpdateHandler<,,>),
            typeof(IDeleteHandler<>),
            typeof(IGetByIdHandler<,>),
            typeof(IListHandler<,>)
        };

        var implementationTypes = assembly
            .GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false });

        foreach (var implementationType in implementationTypes)
        {
            var serviceTypes = implementationType
                .GetInterfaces()
                .Where(type =>
                    type.IsGenericType &&
                    crudHandlerInterfaceDefinitions.Contains(type.GetGenericTypeDefinition()));

            foreach (var serviceType in serviceTypes)
            {
                services.AddScoped(serviceType, implementationType);
            }
        }

        return services;
    }
}
