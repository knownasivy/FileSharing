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
            app.MapGet("files/{fileId}/download", Handler).WithTags("Files");
        }
    }
    
    public static async Task<IResult> Handler(
        ILogger<Endpoint> logger, 
        IDownloadService downloadService, 
        string fileId)
    {
        // For using in swagger or whatever
        fileId = fileId.Replace("-", "");
        if (!Guid.TryParseExact(fileId, "N", out var id))
            return Results.NotFound();
        
        try
        {
            var download = await downloadService.GetByIdAsync(id);

            if (download is null)
            {
                logger.LogInformation("Download not found");
                return Results.NotFound();
            }
            
            return Results.File(
                fileStream: download.DownloadStream,
                fileDownloadName: download.FileName,
                contentType: download.GetContentTypeMime());
        }
        catch(FileNotFoundException)
        {
            return Results.InternalServerError();
        }
    }
}