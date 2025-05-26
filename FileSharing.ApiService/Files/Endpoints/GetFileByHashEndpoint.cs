using FastEndpoints;
using FileSharing.ApiService.Contracts;
using FileSharing.ApiService.Contracts.Responses;

namespace FileSharing.ApiService.Files.Endpoints;

public class GetFileByHashEndpoint : EndpointWithoutRequest<FileResponse>
{
    private readonly IFileService _fileService;

    public GetFileByHashEndpoint(IFileService fileService)
    {
        _fileService = fileService;
    }
    
    public override void Configure()
    {
        // TODO: Think about groups
        Get("/file/hash/{fileHash}");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var fileHash = Route<string>("fileHash");
        
        if (fileHash is null)
        {
            await SendNotFoundAsync(ct);
            return;
        }
        
        var file = await _fileService.GetByHashAsync(fileHash);
        if (file is null)
        {
            await SendNotFoundAsync(ct);
            return;
        }

        await SendAsync(file.MapToResponse(), cancellation: ct);
    }
}