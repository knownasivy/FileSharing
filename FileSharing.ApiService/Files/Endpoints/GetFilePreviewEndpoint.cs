using System.Text;
using Amazon.S3;
using Amazon.S3.Model;
using FastEndpoints;
using FileSharing.ApiService.Contracts.Responses;
using FileSharing.Constants;
using Microsoft.Extensions.Caching.Distributed;

namespace FileSharing.ApiService.Files.Endpoints;

public class GetPreviewFileEndpoint : EndpointWithoutRequest<FilePreviewResponse>
{
    private readonly IFileService _fileService;
    private readonly IDistributedCache _cache;
    private readonly IAmazonS3 _s3;

    public GetPreviewFileEndpoint(IFileService fileService, IDistributedCache cache, IAmazonS3 s3)
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

    public override async Task HandleAsync(CancellationToken ct)
    {
        var fileIdStr = Route<string>("fileId");

        if (!Guid.TryParseExact(fileIdStr, "N", out var fileId))
        {
            AddError("File not found");
            await SendNotFoundAsync(ct);
            return;
        }

        var file = await _fileService.GetByIdAsync(fileId);
        if (file is null)
        {
            await SendNotFoundAsync(ct);
            return;
        }

        if (file.FakeFile)
        {
            file = await _fileService.GetByHashAsync(file.Hash);
            if (file is null)
            {
                await SendNotFoundAsync(ct);
                return;
            }
        }
        
        var cachedBytes = await _cache.GetAsync(fileIdStr, ct);

        if (cachedBytes is not null)
        {
            var cachedUrl = Encoding.UTF8.GetString(cachedBytes);
            
            await SendAsync(new FilePreviewResponse
            {
                Link = cachedUrl
            }, cancellation: ct);
            return;
        }
        
        var request = new GetPreSignedUrlRequest
        {
            BucketName = Storage.Bucket,
            Key = file.GetPreviewFilename(),
            Expires = DateTime.UtcNow.AddMinutes(60),
            Verb = HttpVerb.GET
        };

        var newUrl = await _s3.GetPreSignedURLAsync(request);

        if (newUrl is null)
        {
            AddError("r2 preview url is null.");
            await SendErrorsAsync(StatusCodes.Status500InternalServerError, ct);
            return;
        }

        var bytes = Encoding.UTF8.GetBytes(newUrl);
        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(55)
        };
        
        await _cache.SetAsync(file.GetPreviewFilename(), bytes, options, ct);

        await SendAsync(new FilePreviewResponse
        {
            Link = newUrl
        }, cancellation: ct);
    }
}