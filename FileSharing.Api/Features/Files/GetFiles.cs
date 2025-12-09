using FileSharing.Api.Extensions;
using FileSharing.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace FileSharing.Api.Features.Files;

public class GetFiles
{
    public sealed class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app) =>
            app.MapGet("internal/files", Handler).WithTags("Files Internal");
    }
    
    public static async Task<IResult> Handler(
        ILogger<Endpoint> logger, 
        IWebHostEnvironment env,
        IUploadFileService uploadFileService,
        [FromQuery] int offset,
        [FromQuery] int limit)
    {
        if (offset <= 0 || limit <= 0)
            return Results.BadRequest("Make sure offset and/or limit are set to a number above 0.");
        if (limit > 500) limit = 500;
        
        // TODO: Pagination
        return Results.Ok(await uploadFileService.GetAllPaginatedAsync(offset, limit));
    }
}