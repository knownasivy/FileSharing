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
        IWebHostEnvironment env,
        IUploadFileService uploadFileService,
        string? uploadId = null)
    {
        // TODO: Better errors.
        if (uploadId is null)
        {
            return env.IsProduction() ? 
                Results.NotFound() : Results.Ok(await uploadFileService.GetAllAsync());
        }
        
        if (!Guid.TryParse(uploadId, out var id))
            return Results.BadRequest();

        var uploads = await uploadFileService.GetAllByUploadIdAsync(id);

        return uploads is not null ? 
            Results.Ok(uploads) : Results.NotFound();
    }
}