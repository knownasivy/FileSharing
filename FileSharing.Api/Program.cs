
using FileSharing.Api.Database;
using FileSharing.Api.Extensions;
using FileSharing.Api.Shared;
using FluentValidation;
using Hellang.Middleware.ProblemDetails;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpClient();
builder.Services.ConfigureHttpClientDefaults(http =>
    http.AddStandardResilienceHandler(options =>
    {
        options.Retry.Delay = TimeSpan.FromSeconds(1);
        options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(60);
    }));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options => options.CustomSchemaIds(t => t.FullName?.Replace('+', '.')));

builder.Services.AddMemoryCache(o => o.SizeLimit = StorageConfig.MaxMemCacheSize);
builder.Services.AddHybridCache();

builder.Services.AddServices(builder.Configuration);
builder.Services.AddServicesConfiguration(builder.Environment);

builder.Services.AddEndpoints();
builder.Services.AddValidatorsFromAssembly(typeof(Program).Assembly);

builder.Services.AddControllers();

builder.Services.AddProblemDetails(opts =>
{
    opts.IncludeExceptionDetails = (_, _) => builder.Environment.IsDevelopment();

    opts.Map<ValidationException>(_ =>
        new ValidationProblemDetails { Status = StatusCodes.Status400BadRequest });
});

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy
            .WithOrigins(
                "https://monkifiles.org",
                "https://www.monkifiles.org",
                "https://botmert.dev", 
                "https://www.botmert.dev")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.InjectStylesheet("/swagger/style.css");
    });
}

app.UseForwardedHeaders();

app.MapGet("/health", () => Results.Ok("Healthy"));
var v1ApiGroup = app.MapGroup("/api/v1");
app.MapEndpoints(v1ApiGroup);

if (app.Environment.IsProduction())
{
    app.UseExceptionHandler(errorApp =>
    {
        errorApp.Run(async context =>
        {
            var feature = context.Features.Get<IExceptionHandlerFeature>();
            var ex      = feature?.Error;

            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/json";

            await context.Response.WriteAsJsonAsync(new
            {
                type    = "https://httpstatuses.com/500",
                title   = "An unexpected error occurred.",
            });
        });
    });
    
    app.UseProblemDetails();
}
else
{
    app.UseExceptionHandler();
}

app.MapControllers();

app.Services.GetRequiredService<DatabaseInitializer>()
    .Initialize();

app.UseCors();
app.Run();