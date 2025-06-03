using FileSharing.ApiService.Extensions;
using FileSharing.ApiService.Models;
using FileSharing.ApiService.Services;

namespace FileSharing.ApiService.Features.Metadata;

public static class GetMetadataAudioCover
{
    public sealed class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapGet("metadata/audio/{fileId}/cover", Handler).WithTags("Metadata");
        }
    }

    public static async Task<IResult> Handler(
        ILogger<Endpoint> logger,
        IHttpClientFactory httpClientFactory,
        IFileService fileService,
        ICloudService cloudService,
        string fileId)
    {
        // For using in swagger or whatever
        fileId = fileId.Replace("-", "");
        if (!Guid.TryParseExact(fileId, "N", out var id))
            return Results.NotFound();
        
        var file = await fileService.GetByIdAsync(id);
        if (file is null)
            return Results.NotFound();
        
        if (file.Type != FileType.Audio) return Results.BadRequest();
        
        if (file.FakeFile)
            file = await fileService.GetByHashAsync(file.Hash);
        
        if (file is null)
            return Results.NotFound();

        var coverName = $"{file.Id:N}_cover.webp";
        
        var coverFile = await cloudService.GetPreviewFile(coverName);
        if (coverFile is null)
            return Results.InternalServerError();
        
        var client = httpClientFactory.CreateClient();

        try
        {
            using var response = await client.GetAsync(coverFile);
            response.EnsureSuccessStatusCode();

            var imageBytes = await response.Content.ReadAsByteArrayAsync();
            return Results.Bytes(imageBytes, "image/webp", coverName);
        }
        catch(Exception ex)
        {
            logger.LogError(ex, "Error getting cover");
        }
        
        return Results.InternalServerError();
    }
}