using FileSharing.ApiService.Database;
using FileSharing.ApiService.Extensions;
using FileSharing.ApiService.Middleware;
using FileSharing.Constants;
using Microsoft.AspNetCore.Http.Features;

var builder = WebApplication.CreateBuilder(args);

// builder.Services.AddAntiforgery(options =>
// {
//     options.HeaderName = "X-CSRF-TOKEN";
// });

builder.AddServiceDefaults();

builder.AddNpgsqlDataSource(ProjectNames.GetConnectionString(builder.Environment.IsDevelopment()));

builder.Services.AddHttpClient();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options => options.CustomSchemaIds(t => t.FullName?.Replace('+', '.')));

builder.Services.AddMemoryCache(o => o.SizeLimit = Storage.MaxMemCacheSize);
builder.Services.AddHybridCache();

builder.Services.AddServices(builder.Configuration, builder.Environment);
builder.Services.AddServicesConfiguration(builder.Environment);

builder.Services.AddEndpoints();

if (builder.Environment.IsDevelopment())
{
    builder.Services.AddCors(options =>
    {
        options.AddDefaultPolicy(policy =>
        {
            policy.AllowAnyOrigin()
                .AllowAnyMethod()
                .AllowAnyHeader();
        });
    });
}

// builder.Services.AddValidatorsFromAssembly(typeof(Program).Assembly);

builder.Services.AddProblemDetails();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options => options.InjectStylesheet("/swagger/style.css"));
}

app.UseExceptionHandler();
app.MapDefaultEndpoints();
app.MapEndpoints();
app.UseStaticFiles();
app.UseExceptionHandler();

app.UseMiddleware<Protection>();
app.UseCors();

app.Services.GetRequiredService<DatabaseInitializer>()
    .Initialize();

app.Run();