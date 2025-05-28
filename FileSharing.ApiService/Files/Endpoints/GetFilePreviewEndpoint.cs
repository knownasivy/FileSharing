using System.Net;
using Amazon.S3;
using Amazon.S3.Model;
using FastEndpoints;
using FileSharing.ApiService.Contracts.Responses;
using FileSharing.Constants;
using Microsoft.Extensions.Caching.Hybrid;

namespace FileSharing.ApiService.Files.Endpoints;

public class GetPreviewFileEndpoint : EndpointWithoutRequest<FilePreviewResponse>
{
    private readonly IFileService _fileService;
    private readonly HybridCache _cache;
    private readonly IAmazonS3 _s3;

    public GetPreviewFileEndpoint(IFileService fileService, HybridCache cache, IAmazonS3 s3)
    {
        _fileService = fileService;
        _cache = cache;
        _s3 = s3;
    }
    
    public override void Configure()
    {
        // TODO: Think about groups
        Get("/file/{fileId}/preview");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken token)
    {
        var fileIdStr = Route<string>("fileId");

        if (!Guid.TryParseExact(fileIdStr, "N", out var fileId))
        {
            AddError("File not found");
            await SendNotFoundAsync(token);
            return;
        }

        var file = await _fileService.GetByIdAsync(fileId);
        if (file is null)
        {
            await SendNotFoundAsync(token);
            return;
        }

        if (file.FakeFile)
        {
            file = await _fileService.GetByHashAsync(file.Hash);
            if (file is null)
            {
                await SendNotFoundAsync(token);
                return;
            }
        }
        
        var fileName = file.GetPreviewFilename();
        
        // TODO: Move cache to service?
        var result = await _cache.GetOrCreateAsync<string?>(
            key: $"preview:{fileName}",
            factory: async ct => await PreviewFactory(fileName, ct),
            options: new HybridCacheEntryOptions
            {
                Expiration = TimeSpan.FromMinutes(55),
                LocalCacheExpiration = TimeSpan.FromMinutes(55)
            },
            cancellationToken: token
        );

        if (result is null)
        {
            AddError("r2 preview url is null.");
            await SendErrorsAsync(StatusCodes.Status500InternalServerError, token);
            return;
        }
        
        await SendAsync(new FilePreviewResponse
        {
            Link = result
        }, cancellation: token);
    }

    private async Task<string?> PreviewFactory(string fileName, CancellationToken token)
    {
        try
        {
            await _s3.GetObjectMetadataAsync(new GetObjectMetadataRequest
            {
                BucketName = Storage.Bucket,
                Key = fileName
            }, token);
     
            var request = new GetPreSignedUrlRequest
            {
                BucketName = Storage.Bucket,
                Key = fileName,
                Expires = DateTime.UtcNow.AddMinutes(60),
                Verb = HttpVerb.GET
            };
                    
            return await _s3.GetPreSignedURLAsync(request);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    } 
}