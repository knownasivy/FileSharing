using Microsoft.Net.Http.Headers;
using FileSharing.Api.Extensions;
using FileSharing.Api.Models;
using FileSharing.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace FileSharing.Api.Features.Files;

public static class GetFilePreview
{
    public sealed class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app) =>
            app.MapGet("site/files/{fileId:guid}/preview", Handler).WithTags("Files Site");
    }
    
    public static async Task<IResult> Handler(
        ILogger<Endpoint> logger, 
        IUploadFileService uploadFileService,
        IDownloadService downloadService,
        HttpContext context,
        Guid fileId,
        [FromQuery] string? type = "default",
        [FromQuery] string? version = "normal",
        CancellationToken ct = default)
    {
        var fixedType = type switch
        {
            // TODO: enum?
            "video" => "mp4",
            "default" => "m4a",
            null => "m4a",
            _ => null
        };
        
        var fixedVersion = version switch
        {
            null => "normal",
            "fast" or "normal" => version,
            _ => null
        };


        if (fixedType is null || fixedVersion is null) 
            return Results.NotFound();
        
        // TODO: Better errors.
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
        
        return await downloadService.GetPreviewByIdAsync(
            id: fileId, 
            ip: ipAddress, 
            type: fixedType, 
            version: fixedVersion, 
            token: ct);
    }
}