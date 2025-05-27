using FastEndpoints;
using FastEndpoints.Swagger;
using FileSharing.ApiService;
using FileSharing.ApiService.Files;
using FileSharing.Constants;
using Microsoft.AspNetCore.Server.Kestrel.Core;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();
builder.AddNpgsqlDataSource(ProjectNames.GetConnectionString(builder.Environment.IsDevelopment()));

builder.Services.AddHybridCache(options =>
{
    options.MaximumPayloadBytes = Limits.MaxCachedFileSize; // 20 MB
    options.MaximumKeyLength = 512;
    options.DisableCompression = true;
});

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

builder.Services.Configure<KestrelServerOptions>(options =>
{
    options.Limits.MaxRequestBodySize = Limits.MaxFileSize;
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

app.UseCors();
app.Run();
