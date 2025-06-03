using FileSharing.ApiService.Extensions;
using FileSharing.ApiService.Services;

namespace FileSharing.ApiService.Features.Metadata;

public class GetMetadataZip
{
    public sealed class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapGet("metadata/zip/{fileId}", Handler).WithTags("Metadata");
        }
    }

    public static async Task<IResult> Handler(
        ILogger<Endpoint> logger, 
        IMetadataService metadataService, 
        string fileId)
    {
        // For using in swagger or whatever
        fileId = fileId.Replace("-", "");
        if (!Guid.TryParseExact(fileId, "N", out var id))
            return Results.NotFound();
        
        var metadata = await metadataService.GetZipMetadataByFileId(id);

        return metadata is null ? Results.NotFound() 
            : Results.Ok(metadata);
    }
}