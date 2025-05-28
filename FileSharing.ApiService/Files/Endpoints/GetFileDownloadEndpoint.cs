using FastEndpoints;
using FileSharing.Constants;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Caching.Hybrid;

namespace FileSharing.ApiService.Files.Endpoints;

public class GetFileDownloadEndpoint : EndpointWithoutRequest
{
    private readonly IFileService _fileService;
    private readonly HybridCache _cache;

    public GetFileDownloadEndpoint(IFileService fileService, HybridCache cache)
    {
        _fileService = fileService;
        _cache = cache;
    }
    
    public override void Configure()
    {
        Get("/file/{fileId}/download");
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

        var file = await GetFile(fileId);
        if (file == null)
        {
            await SendNotFoundAsync(token);
            return;
        }
        
        var newFileName = $"{file.Id:N}.{file.Extension}";
        var filePath = Path.Combine(file.GetLocation(), newFileName);
        
        var fileInfo = new FileInfo(filePath);
        if (!fileInfo.Exists)
        {
            AddError("File not found internally");
            await SendErrorsAsync(StatusCodes.Status500InternalServerError, token);
            return;
        }
        
        // TODO: Max cache per ip
        if (fileInfo.Length > Limits.MaxCachedFileSize)
        {
            await using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            
            await SendStreamAsync(
                stream: fileStream,
                fileName: file.Name,
                fileLengthBytes: fileStream.Length,
                contentType: GetContentTypeMime(file.Name),
                cancellation: token);
            return;
        }
        
        var cachedKey = $"download:{file.Name}";
        var cachedFile = await _cache.GetOrCreateAsync(
            key: cachedKey,
            factory: async ct => await File.ReadAllBytesAsync(filePath, ct),
            options: new HybridCacheEntryOptions 
            { 
                Expiration = TimeSpan.FromMinutes(3),
                LocalCacheExpiration = TimeSpan.FromMinutes(3)
            },
            cancellationToken: token
        );

        await SendStreamAsync(
            stream: new MemoryStream(cachedFile), 
            fileName: file.Name, 
            fileLengthBytes: cachedFile.Length, 
            cancellation: token);
    }

    private async Task<FileUpload?> GetFile(Guid fileId)
    {
        var file = await _fileService.GetByIdAsync(fileId);
        if (file is null)
        {
            AddError("File not found");
            return file;
        }

        if (!file.FakeFile)
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