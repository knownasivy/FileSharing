using FileSharing.ApiService.Extensions;
using FileSharing.ApiService.Services;

namespace FileSharing.ApiService.Features.Files;

public class GetFiles
{
    public sealed class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapGet("files", Handler).WithTags("Files");
        }
    }
    
    public static async Task<IResult> Handler(
        ILogger<Endpoint> logger, 
        IFileService fileService,
        string? uploadId)
    {
        if (uploadId is null)
            return Results.Ok(await fileService.GetAllAsync());
        
        return Guid.TryParseExact(uploadId, "N", out var id) ? 
            Results.Ok(await fileService.GetAllByUploadIdAsync(id)) : Results.InternalServerError();
    }
}