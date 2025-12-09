using FileSharing.Api.Extensions;
using FileSharing.Api.Services;

namespace FileSharing.Api.Features.Files;

public class GetFile
{
    public sealed class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app) => 
            app.MapGet("internal/files/{fileId:guid}", Handler).WithTags("Files Internal");
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