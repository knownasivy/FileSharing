using FastEndpoints;
using FileSharing.ApiService.Contracts;
using FileSharing.ApiService.Contracts.Responses;

namespace FileSharing.ApiService.Files.Endpoints;

public class GetFileEndpoint : EndpointWithoutRequest<FileResponse>
{
    private readonly IFileService _fileService;

    public GetFileEndpoint(IFileService fileService)
    {
        _fileService = fileService;
    }
    
    public override void Configure()
    {
        // TODO: Think about groups
        Get("/file/{fileId}");
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
            AddError("File not found");
            await SendNotFoundAsync(ct);
            return;
        }

        await SendAsync(file.MapToResponse(), cancellation: ct);
    }
}