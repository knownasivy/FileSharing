using FastEndpoints;
using FileSharing.ApiService.Util;

namespace FileSharing.ApiService.Downloads.Endpoints;

public class GetDownloadEndpoint : EndpointWithoutRequest
{
    private readonly IDownloadService _downloadService;
    
    public GetDownloadEndpoint(IDownloadService downloadService)
    {
        _downloadService = downloadService;
    }
    
    public override void Configure()
    {
        Get("/download/{fileId}");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken token)
    {
        var fileIdStr = Route<string>("fileId");

        if (!Guid.TryParseExact(fileIdStr, "N", out var fileId))
        {
            await SendNotFoundAsync(token);
            return;
        }

        // TODO: Result pattern
        try
        {
            await using var download = await _downloadService.GetByIdAsync(fileId);

            if (download is null)
            {
                await SendNotFoundAsync(token);
                return;
            }

            await SendStreamAsync(
                stream: download.DownloadStream,
                fileName: download.FileName,
                fileLengthBytes: download.DownloadStream.Length,
                contentType: FileUtil.GetContentTypeMime(download.FileName), // Move to download service?
                cancellation: token);
        }
        catch(FileNotFoundException)
        {
            AddError("File not found internally.");
            await SendErrorsAsync(StatusCodes.Status500InternalServerError, token);
        }
    }
}