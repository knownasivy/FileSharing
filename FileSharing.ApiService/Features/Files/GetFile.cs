using FileSharing.ApiService.Extensions;
using FileSharing.ApiService.Services;

namespace FileSharing.ApiService.Features.Files;

public class GetFile
{
    public sealed class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapGet("files/{fileId:guid}", Handler).WithTags("Files");
        }
    }
    
    public static async Task<IResult> Handler(
        ILogger<Endpoint> logger, 
        IUploadFileService uploadFileService, 
        Guid fileId)
    {
        var file = await uploadFileService.GetByIdAsync(fileId);

        // TODO: Better errors
        return file is null ? Results.NotFound() 
            : Results.Ok(file);
    }
}