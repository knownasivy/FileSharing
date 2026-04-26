using FileSharing.Api.Shared;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Server.Kestrel.Core;

namespace FileSharing.Api.Extensions;

public static class Configuration
{
    public static void AddServicesConfiguration(this IServiceCollection services)
    {
        services.Configure<FormOptions>(options =>
        {
            options.MultipartBodyLengthLimit = StorageConfig.MaxFileSize;
            options.ValueLengthLimit = int.MaxValue;
            options.MemoryBufferThreshold = 2 * BytesSize.MiB;
        });

        services.Configure<KestrelServerOptions>(options =>
        {
            options.Limits.MaxRequestBodySize = StorageConfig.MaxFileSize;
            options.Limits.RequestHeadersTimeout = TimeSpan.FromMinutes(5);
            options.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(5);
        });

        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders =
                ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

            options.KnownProxies.Clear();
        });

        services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
            {
                policy
                    .WithOrigins(
                        "https://monkifiles.org",
                        "https://www.monkifiles.org",
                        "https://botmert.dev",
                        "https://www.botmert.dev"
                    )
                    .AllowAnyHeader()
                    .AllowAnyMethod();
            });
        });
    }
}
