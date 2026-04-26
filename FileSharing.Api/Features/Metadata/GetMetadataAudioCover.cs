using FileSharing.Api.Extensions;
using FileSharing.Api.Models;
using FileSharing.Api.Services;

namespace FileSharing.Api.Features.Metadata;

public static class GetMetadataAudioCover
{
    public sealed class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app) =>
            app.MapGet("site/metadata/audio/{fileId:guid}/cover", Handler)
                .WithTags("Metadata Site");
    }

    public static async Task<IResult> Handler(
        ILogger<Endpoint> logger,
        IUploadFileService uploadFileService,
        IDownloadService downloadService,
        HttpContext context,
        Guid fileId,
        CancellationToken ct = default
    )
    {
        var validFile = await uploadFileService.GetIsFileTypeById(fileId, FileType.Audio);
        if (!validFile)
        {
            logger.LogError("File is not valid");
            return Results.NotFound();
        }

        var ip = context.Connection.RemoteIpAddress;
        if (ip is null)
            return Results.Problem("Could not determine IP address");

        var ipAddress = ip.MapToIPv4().ToString();

        // context.Response.Headers.CacheControl = "public, max-age=3600";

        return await downloadService.GetCoverByIdAsync(fileId, ct);
    }
}
