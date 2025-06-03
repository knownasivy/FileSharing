using FileSharing.Constants;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Server.Kestrel.Core;

namespace FileSharing.ApiService.Extensions;

public static class Configuration
{
    public static void AddServicesConfiguration(
        this IServiceCollection services,
        IWebHostEnvironment env)
    {
        services.Configure<FormOptions>(options =>
        {
            options.MultipartBodyLengthLimit = Storage.MaxFileSize;
        });
        
        services.Configure<KestrelServerOptions>(options =>
        {
            options.Limits.MaxRequestBodySize = Storage.MaxFileSize;
        });
        
        if (env.IsProduction())
        {
            // For nginx
            services.Configure<ForwardedHeadersOptions>(options =>
            {
                options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
                options.KnownNetworks.Clear(); // Allow all networks
                options.KnownProxies.Clear();  // Allow all proxies
            });
        }
    }
}