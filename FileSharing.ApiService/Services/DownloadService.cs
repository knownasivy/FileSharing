using System.Buffers;
using System.Collections.Concurrent;
using FileSharing.ApiService.Shared;
using FileSharing.Constants;
using Microsoft.Extensions.Caching.Memory;

namespace FileSharing.ApiService.Services;

public interface IDownloadService
{
    Task<IResult> GetByIdAsync(Guid id, string ipAddress);
}

public class DownloadService : IDownloadService
{
    private readonly ILogger<DownloadService> _logger;
    private readonly IUploadFileService _uploadFileService;
    private readonly IMetricsService _metricsService;
    private readonly IMemoryCache _cache;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks;
    
    public DownloadService(
        ILogger<DownloadService> logger, 
        IUploadFileService uploadFileService, 
        IMetricsService metricsService, 
        IMemoryCache cache)
    {
        _logger = logger;
        _uploadFileService = uploadFileService;
        _metricsService = metricsService;
        _cache = cache;
        _locks = new ConcurrentDictionary<string, SemaphoreSlim>();
    }
    
    // TODO: Use result pattern
    public async Task<IResult> GetByIdAsync(Guid id, string ipAddress)
    {
        // TODO: Max cache per ipfiles
        var downloadFile = await _uploadFileService.GetDownloadFileByIdAsync(id);
        if (downloadFile is null)
        {
            _logger.LogError("File not found");
            return Results.NotFound();
        }

        if (string.IsNullOrEmpty(downloadFile.FilePath))
        {
            _logger.LogError("File still uploading");
            return Results.BadRequest();
        }

        var contentType = FileUtil.GetContentTypeMime(downloadFile.Name);
        
        var bufferSize = FileUtil.GetBufferSize(downloadFile.Size);
        if (downloadFile.Size > Storage.MaxCachedFileSize)
        {
            return GetFileDownload(downloadFile, contentType, bufferSize, ipAddress);
        }

        _logger.LogInformation("Caching file: {fileId}", downloadFile.RealId);
        
        try
        {
            var cachedFile = await GetOrCreateAsync(
                $"download:{downloadFile.RealId}",
                downloadFile.Size,
                async () =>
                {
                    _logger.LogInformation("Cache add.");
                    if (!File.Exists(downloadFile.FilePath))
                        throw new FileNotFoundException();
                
                    return await ReadFileForCache(
                        downloadFile.FilePath,
                        downloadFile.Size,
                        bufferSize);
                });
        
            if (cachedFile is not null)
            {
                _logger.LogInformation("Cache hit.");
                _metricsService.RecordDownload(ipAddress, cachedFile.Length);
                return Results.Bytes(cachedFile, contentType, downloadFile.Name);
            }
        }
        catch(FileNotFoundException)
        {
            return Results.NotFound();
        }
        
        // Fallback
        _logger.LogError("Cache miss.");
        return GetFileDownload(downloadFile, contentType, bufferSize, ipAddress);
    }

    private IResult GetFileDownload(
        DownloadFile downloadFile, 
        string contentType, 
        int bufferSize, 
        string ipAddress)
    {
        if (!File.Exists(downloadFile.FilePath)) 
            return Results.NotFound();

        try
        {
            var fs = new FileStream(
                downloadFile.FilePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize,
                FileOptions.SequentialScan);
        
            var countingStream = new CountingStream(fs, bytesRead => 
                _metricsService.RecordDownload(ipAddress, bytesRead)
            );
                    
            return Results.File(countingStream, contentType, downloadFile.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create file stream for {filePath}", downloadFile.FilePath);
            return Results.InternalServerError();
        }
    }

    private static async Task<byte[]> ReadFileForCache(
        string filePath, 
        long fileSize, 
        int bufferSize)
    {
        var result = new byte[fileSize];
        var rentedBuffer = ArrayPool<byte>.Shared.Rent(bufferSize);
    
        try
        {
            await using var fileStream = new FileStream(filePath, 
                FileMode.Open, 
                FileAccess.Read, 
                FileShare.Read, 
                bufferSize, 
                FileOptions.SequentialScan | FileOptions.Asynchronous);
            
            var totalBytesRead = 0;

            while (totalBytesRead < fileSize)
            {
                var maxRead = (int) Math.Min(bufferSize, fileSize - totalBytesRead);
                var bytesRead = await fileStream.ReadAsync(rentedBuffer.AsMemory(0, maxRead));
            
                if (bytesRead == 0) break;
                
                Array.Copy(rentedBuffer, 0, result, totalBytesRead, bytesRead);
                totalBytesRead += bytesRead;
            }
        
            if (totalBytesRead != fileSize)
            {
                Array.Resize(ref result, totalBytesRead);
            }
            
            return result;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rentedBuffer);
        }
    }

    // TODO: Simplify
    private async Task<T?> GetOrCreateAsync<T>(string key, long size, Func<Task<T>> factory)
    {
        if (_cache.TryGetValue(key, out T? value)) return value;

        var myLock = _locks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));

        await myLock.WaitAsync();
        try
        {
            if (_cache.TryGetValue(key, out value)) return value;

            value = await factory();
            _cache.Set(key, value, new MemoryCacheEntryOptions
            {
                SlidingExpiration = TimeSpan.FromMinutes(2),
                Size = size
            });
            return value;
        }
        finally
        {
            myLock.Release();
            if (_locks.TryRemove(key, out var removedLock) && removedLock.CurrentCount == 1)
                removedLock.Dispose();
        }
    }
}

