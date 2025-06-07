using Amazon.S3.Model;
using FileSharing.ApiService.Extensions;
using FileSharing.ApiService.Services;
using FileSharing.ApiService.Shared;

namespace FileSharing.ApiService.Features.Files;

public class GetFileDownload
{
    public sealed class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapGet("files/{fileId:guid}/download", Handler).WithTags("Files");
        }
    }
    
    public static async Task<IResult> Handler(
        HttpContext context,
        ILogger<Endpoint> logger, 
        IDownloadService downloadService,
        Guid fileId)
    {
        // TODO: Better errors
        var ipAddress = context.Connection.RemoteIpAddress?.ToString();
        if (ipAddress is null)
            return Results.InternalServerError();
            
        return await downloadService.GetByIdAsync(fileId, ipAddress);
    }
}