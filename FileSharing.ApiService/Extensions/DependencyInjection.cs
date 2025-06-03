using FileSharing.ApiService.BackgroundServices;
using FileSharing.ApiService.Database;
using FileSharing.ApiService.Services;
using FileSharing.Constants;

namespace FileSharing.ApiService.Extensions;

public static class DependencyInjection
{
    public static IServiceCollection AddServices(
        this IServiceCollection services, 
        IConfiguration configuration,
        IWebHostEnvironment env)
    {
        services.AddSingleton(_ => new DatabaseInitializer(configuration
            .GetConnectionString(ProjectNames.GetConnectionString(env.IsDevelopment()))));
        services.AddSingleton<ICloudService, CloudService>();
        services.AddSingleton<IUploadService, UploadService>();
        services.AddSingleton<IFileService, FileService>();
        services.AddSingleton<IMetadataService, MetadataService>();
        services.AddSingleton<IDownloadService, DownloadService>();
        
        services.AddSingleton<IMetadataProcessor, MetadataProcessor>();
        services.AddHostedService<MetadataProcessor>(provider => (MetadataProcessor) 
            provider.GetRequiredService<IMetadataProcessor>());

        return services;
    }
}