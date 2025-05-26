using FastEndpoints;
using Microsoft.AspNetCore.StaticFiles;

namespace FileSharing.ApiService.Files.Endpoints;

public class GetFileDownloadEndpoint : EndpointWithoutRequest
{
    private readonly IFileService _fileService;

    public GetFileDownloadEndpoint(IFileService fileService)
    {
        _fileService = fileService;
    }
    
    public override void Configure()
    {
        Get("/file/{fileId}/download");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var fileIdStr = Route<string>("fileId");

        if (!Guid.TryParseExact(fileIdStr, "N", out var fileId))
        {
            await SendNotFoundAsync(ct);
            return;
        }

        var file = await GetFile(fileId);
        if (file == null)
        {
            await SendNotFoundAsync(ct);
            return;
        }
        
        var newFileName = $"{file.Id:N}.{file.FileExtension}";
        var filePath = Path.Combine(file.GetLocation(), newFileName);
        
        if (!File.Exists(filePath))
        {
            AddError("File not found internally");
            await SendErrorsAsync(StatusCodes.Status500InternalServerError, ct);
            return;
        }
        
        var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
        
        await SendStreamAsync(
            stream: fileStream,
            fileName: file.Name,
            fileLengthBytes: fileStream.Length,
            contentType: GetContentTypeMime(file.Name),
            cancellation: ct);
    }

    private async Task<UploadFile?> GetFile(Guid fileId)
    {
        var file = await _fileService.GetByIdAsync(fileId);
        if (file is null)
        {
            AddError("File not found");
            return file;
        }

        if (file.Type != UploadFile.FileType.Hash)
        {
            return file;
        }

        var fileName = file.Name;
        file = await _fileService.GetByHashAsync(file.Hash);
        if (file is null) return null;

        file.Name = fileName;
        
        return file;
    }

    private static string GetContentTypeMime(string fileName)
    {
        var provider = new FileExtensionContentTypeProvider();
        if (!provider.TryGetContentType(fileName, out var contentType))
        {
            contentType = "application/octet-stream"; // Default MIME type if unknown
        }

        return contentType;
    }
}