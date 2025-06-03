using FileSharing.ApiService.Extensions;
using FileSharing.ApiService.Models;
using FileSharing.ApiService.Services;

namespace FileSharing.ApiService.Features.Files;

public class GetFilePreview
{
    public sealed class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapGet("files/{fileId}/preview", Handler).WithTags("Files");
        }
    }
    
    public static async Task<IResult> Handler(
        IHttpClientFactory httpClientFactory,
        ILogger<Endpoint> logger, 
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
        
        if (file.Type == FileType.Archive) return Results.BadRequest();

        if (file.FakeFile)
            file = await fileService.GetByHashAsync(file.Hash);
        
        if (file is null)
            return Results.NotFound();
        
        var preview = await cloudService.GetPreviewFile(file.GetPreviewFilename());
        var client = httpClientFactory.CreateClient();
        
        try
        {
            var contentType = file.Type == FileType.Audio 
                ? "audio/mp4" : "image/webp";
        
            using var response = await client.GetAsync(preview);
            response.EnsureSuccessStatusCode();

            var previewBytes = await response.Content.ReadAsByteArrayAsync();
            return Results.Bytes(previewBytes, contentType, file.GetPreviewFilename());
        }
        catch(Exception ex)
        {
            logger.LogError(ex, "Error getting preview file");
        }
        
        return Results.InternalServerError();
    }
}