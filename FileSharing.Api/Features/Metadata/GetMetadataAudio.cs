using FileSharing.Api.Extensions;
using FileSharing.Api.Services;

namespace FileSharing.Api.Features.Metadata;

public static class GetMetadataAudio
{
    public sealed class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app) =>
            app.MapGet("internal/metadata/audio/{fileId:guid}", Handler).WithTags("Metadata Internal");
    }

    public static async Task<IResult> Handler(
        ILogger<Endpoint> logger, 
        IMetadataService metadataService, 
        Guid fileId)
    {
        var metadata = await metadataService.GetAudioMetadataByIdAsync(fileId);

        return metadata is null ? Results.NotFound() 
            : Results.Ok(metadata);
    }
}