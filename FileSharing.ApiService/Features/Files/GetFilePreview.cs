using Microsoft.Net.Http.Headers;
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
            app.MapGet("files/{fileId:guid}/preview", Handler).WithTags("Files");
        }
    }
    
    public static async Task<IResult> Handler(
        IHttpClientFactory httpClientFactory,
        ILogger<Endpoint> logger, 
        IUploadFileService uploadFileService,
        ICloudService cloudService, 
        HttpContext context,
        Guid fileId)
    {
        // TODO: Better errors.        
        var file = await uploadFileService.GetByIdAsync(fileId);
        if (file is null)
            return Results.NotFound();
        
        if (file.Type == FileType.Archive) return Results.BadRequest();

        if (file.FakeFile)
            file = await uploadFileService.GetRealByHashAsync(file.Hash);
        
        if (file is null)
            return Results.NotFound();
        
        var previewUrl = await cloudService.GetPreviewFileUrl(file.GetPreviewFilename());
        if (previewUrl is null)
            return Results.NotFound();
        
        var client = httpClientFactory.CreateClient();
        try
        {
            var response = await client.GetAsync(
                previewUrl,
                HttpCompletionOption.ResponseHeadersRead
            );
                
            response.EnsureSuccessStatusCode();

            var remoteStream = await response.Content.ReadAsStreamAsync();
            
            var headers = context.Response.GetTypedHeaders();
            headers.CacheControl = new CacheControlHeaderValue
            {
                Public = true,
                MaxAge = TimeSpan.FromHours(3)
            };
            
            return Results.Stream(
                async outputStream =>
                {
                    await remoteStream.CopyToAsync(
                        outputStream, 
                        bufferSize: 8 * 1024,
                        context.RequestAborted);
                },
                "audio/mp4",
                file.GetPreviewFilename()
            );
        }
        catch(Exception ex)
        {
            logger.LogError(ex, "Error getting preview file");
        }
        
        return Results.InternalServerError();
    }
}