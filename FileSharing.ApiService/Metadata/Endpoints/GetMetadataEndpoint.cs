using FastEndpoints;
using FileSharing.ApiService.Contracts;
using FileSharing.ApiService.Contracts.Responses;
using FileSharing.ApiService.Files;

namespace FileSharing.ApiService.Metadata.Endpoints;

public class GetMetadataEndpoint : EndpointWithoutRequest<MetadataResponse>
{
    private readonly IMetadataService _metadataService;
    private readonly IFileService _fileService;

    public GetMetadataEndpoint(IMetadataService metadataService, IFileService fileService)
    {
        _metadataService = metadataService;
        _fileService = fileService;
    }

    public override void Configure()
    {
        // TODO: Think about groups
        Get("/metadata/{fileId}");
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
        if (file == null)
        {
            AddError("File not found");
            await SendNotFoundAsync(ct);
            return;
        }
        
        var metadata = await _metadataService.GetByFileAsync(file);
        if (metadata is null)
        {
            AddError("Metadata not found");
            await SendNotFoundAsync(ct);
            return;
        }
        
        metadata.FileId = fileId;

        await SendAsync(metadata.MapToResponse(), cancellation: ct);
    }
}