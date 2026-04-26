using FileSharing.Api.BackgroundServices;
using FileSharing.Api.Database;
using FileSharing.Api.Services;

namespace FileSharing.Api.Extensions;

public static class DependencyInjection
{
    public static IServiceCollection AddServices(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.AddSingleton<IDbConnectionFactory, NpgsqlDbConnectionFactory>();
        services.AddSingleton(_ => new DatabaseInitializer(
            configuration.GetConnectionString("DefaultConnection")
        ));
        services.AddSingleton<ICloudService, CloudService>();
        services.AddSingleton<IUploadFileService, UploadFileService>();
        services.AddSingleton<IMetadataService, MetadataService>();
        services.AddSingleton<IDownloadService, DownloadService>();
        services.AddSingleton<IMetricsService, MetricsService>();

        services.AddSingleton<IMetadataProcessor, MetadataProcessor>();
        services.AddHostedService<MetadataProcessor>(provider =>
            (MetadataProcessor)provider.GetRequiredService<IMetadataProcessor>()
        );

        return services;
    }
}
