using Amazon.S3.Model;
using FileSharing.Api.Extensions;
using FileSharing.Api.Services;
using FileSharing.Api.Shared;

namespace FileSharing.Api.Features.Files;

public class GetFileDownload
{
    public sealed class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app) 
            => app.MapGet("site/files/{fileId:guid}/download", Handler).WithTags("Files Site");
    }
    
    public static async Task<IResult> Handler(
        HttpContext context,
        ILogger<Endpoint> logger, 
        IDownloadService downloadService,
        Guid fileId,
        CancellationToken ct)
    {
        // TODO: Better errors
        var ip = context.Connection.RemoteIpAddress;
        if (ip is null)
            return Results.Problem("Could not determine IP address");

        var ipAddress = ip.MapToIPv4().ToString();
            
        return await downloadService.GetByIdAsync(fileId, ipAddress, ct);
    }
}