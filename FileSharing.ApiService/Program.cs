using FileSharing.ApiService.Database;
using FileSharing.ApiService.Extensions;
using FileSharing.ApiService.Middleware;
using FileSharing.Constants;
using FluentValidation;
using Microsoft.AspNetCore.HttpOverrides;

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

builder.Services.AddValidatorsFromAssembly(typeof(Program).Assembly);

builder.Services.AddProblemDetails();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger(options =>
    {
        options.RouteTemplate = "api/docs/{documentName}/swagger.json";
    });

    app.UseSwaggerUI(options =>
    {
        options.RoutePrefix = "api/docs";
        options.SwaggerEndpoint("/api/docs/v1/swagger.json", "My API V1");
        options.InjectStylesheet("/api/swagger/style.css");
    });
}

app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});

app.UseStaticFiles();

var v1ApiGroup = app.MapGroup("/api/v1");

app.MapEndpoints(v1ApiGroup);
app.UseExceptionHandler();
app.MapDefaultEndpoints();

//app.UseMiddleware<Protection>();

app.Services.GetRequiredService<DatabaseInitializer>()
    .Initialize();

//app.UseCors();
app.Run();