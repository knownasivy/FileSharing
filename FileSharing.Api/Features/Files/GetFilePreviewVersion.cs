using Microsoft.Net.Http.Headers;
using FileSharing.Api.Extensions;
using FileSharing.Api.Models;
using FileSharing.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace FileSharing.Api.Features.Files;

public static class GetFilePreviewVersion
{
    public record Response(string Version);
    
    public sealed class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app) =>
            app.MapGet("internal/files/{fileId:guid}/preview/version", Handler).WithTags("Files Internal");
    }
    
    public static async Task<IResult> Handler(
        ILogger<Endpoint> logger, 
        IUploadFileService uploadFileService,
        IDownloadService downloadService,
        HttpContext context,
        Guid fileId,
        CancellationToken ct = default)
    {
        var result = await downloadService.GetPreviewVersionByIdAsync(fileId);
        if (result is null)
        {
            logger.LogError("Result is null");
            return Results.NotFound();
        }
            
        return Results.Ok(new Response(result));
    }
}