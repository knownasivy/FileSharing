using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using FastEndpoints;
using FastEndpoints.Swagger;
using FileSharing.ApiService.Files;
using FileSharing.Constants;
using Microsoft.AspNetCore.Server.Kestrel.Core;

var builder = WebApplication.CreateBuilder(args);

if (builder.Environment.IsDevelopment())
{
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

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();
builder.AddNpgsqlDataSource(ProjectNames.GetConnectionString(builder.Environment.IsDevelopment()));
builder.AddRedisDistributedCache(connectionName: ProjectNames.Redis);

// TODO: Versioning?
//builder.Services.AddEndpointsApiExplorer();

builder.Services.AddFastEndpoints()
    .SwaggerDocument(o =>
    {
        o.DocumentSettings = s =>
        {
            s.Title = "Files Sharing API";
            s.Version = "v1";
        };
    });

var credentials = new BasicAWSCredentials(
    builder.Configuration["R2:AccessKey"], 
    builder.Configuration["R2:SecretKey"]);

var accountId = builder.Configuration["R2:AccountId"];
var config = new AmazonS3Config
{
    ServiceURL = $"https://{accountId}.r2.cloudflarestorage.com",
    RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
    ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED
};

var s3Client = new AmazonS3Client(credentials, config);

builder.Services.AddSingleton<IAmazonS3>(s3Client);
builder.Services.AddSingleton<IFileService, FileService>();

// TODO: NGL this is basically vibe coded in
builder.Services.Configure<KestrelServerOptions>(options =>
{
    options.Limits.MaxRequestBodySize = Limits.MaxFileSize;
});

builder.Services.AddProblemDetails();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler();
}

app.UseStaticFiles();

app.MapDefaultEndpoints();

app.UseFastEndpoints()
    .UseSwaggerGen(c =>
    {
        c.Path = "/swagger/v1/swagger.json";
    });

app.UseCors();

app.Run();
