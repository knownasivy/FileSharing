using FileSharing.ApiService.Extensions;
using FileSharing.ApiService.Models;
using FileSharing.ApiService.Services;
using Microsoft.Net.Http.Headers;

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
        IUploadFileService uploadFileService,
        ICloudService cloudService,
        HttpContext context,
        string fileId)
    {
        // For using in swagger or whatever
        fileId = fileId.Replace("-", "");
        if (!Guid.TryParseExact(fileId, "N", out var id))
            return Results.NotFound();
        
        var file = await uploadFileService.GetByIdAsync(id);
        if (file is null)
            return Results.NotFound();
        
        if (file.Type != FileType.Audio) return Results.BadRequest();
        
        if (file.FakeFile)
            file = await uploadFileService.GetRealByHashAsync(file.Hash);
        
        if (file is null)
            return Results.NotFound();

        var coverFileUrl = await cloudService.GetPreviewFileUrl($"{file.Id:N}_cover.webp");
        if (coverFileUrl is null)
            return Results.NotFound();
        
        var client = httpClientFactory.CreateClient();
        
        try
        {
            var response = await client.GetAsync(
                coverFileUrl,
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
                        bufferSize: 4 * 1024,
                        context.RequestAborted);
                },
                "image/webp",
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