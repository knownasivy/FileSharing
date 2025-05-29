using FastEndpoints;
using FileSharing.ApiService.Util;

namespace FileSharing.ApiService.Downloads.Endpoints;

// TODO: Implement
public class GetDownloadArchiveFileEndpoint : EndpointWithoutRequest
{
    private readonly IDownloadService _downloadService;
    
    public GetDownloadArchiveFileEndpoint(IDownloadService downloadService)
    {
        _downloadService = downloadService;
    }
    
    public override void Configure()
    {
        Get("/download/{fileId}/{zipItem}");
        AllowAnonymous();
    }
    
    public override Task HandleAsync(CancellationToken token)
    {
        throw new Exception("Not implemented");
    }
}
