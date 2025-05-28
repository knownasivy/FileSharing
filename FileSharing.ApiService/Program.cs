using FastEndpoints;
using FastEndpoints.Swagger;
using FileSharing.ApiService;
using FileSharing.ApiService.Downloads;
using FileSharing.ApiService.Files;
using FileSharing.ApiService.Middleware;
using FileSharing.Constants;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Server.Kestrel.Core;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();
builder.AddNpgsqlDataSource(ProjectNames.GetConnectionString(builder.Environment.IsDevelopment()));

if (builder.Environment.IsProduction())
{
    // For nginx
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        options.KnownNetworks.Clear(); // Allow all networks
        options.KnownProxies.Clear();  // Allow all proxies
    });
}

builder.Services.AddMemoryCache(o =>
{
    o.SizeLimit = Storage.MaxMemCacheSize;
});

builder.Services.AddHybridCache();

var bfp = builder.Services.AddFastEndpoints();

if (builder.Environment.IsDevelopment())
{
    bfp.SwaggerDocument(o =>
    {
        o.DocumentSettings = s =>
        {
            s.Title = "Files Sharing API";
            s.Version = "v1";
        };
    });
    
    builder.Services.AddCors(options =>
    {
        options.AddDefaultPolicy(policy =>
        {
            policy.AllowAnyOrigin()
                .AllowAnyHeader()
                .AllowAnyMethod();
        });
    });
}

builder.Services.AddSingleton(R2Service.GetR2Config(new R2Config
{
    AccessKey = builder.Configuration["R2:AccessKey"] ?? "",
    SecretKey = builder.Configuration["R2:SecretKey"] ?? "",
    AccountId = builder.Configuration["R2:AccountId"] ?? ""
}));

builder.Services.AddSingleton<IFileService, FileService>();

builder.Services.AddSingleton<IDownloadService, DownloadService>();

builder.Services.Configure<KestrelServerOptions>(options =>
{
    options.Limits.MaxRequestBodySize = Storage.MaxFileSize;
});

builder.Services.AddProblemDetails();

var app = builder.Build();

app.MapDefaultEndpoints();
app.UseExceptionHandler();

var afp = app.UseFastEndpoints();

if (app.Environment.IsDevelopment())
{
    afp.UseSwaggerGen(c =>
    {
        c.Path = "/swagger/v1/swagger.json";
    });
}

if (app.Environment.IsProduction())
{
    app.UseForwardedHeaders();
}

app.UseMiddleware<Protection>();

app.UseCors();
app.Run();
