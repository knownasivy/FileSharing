using System.Collections.Concurrent;
using FileSharing.ApiService.Files;
using FileSharing.Constants;
using Microsoft.Extensions.Caching.Memory;

namespace FileSharing.ApiService.Downloads;

public class DownloadService : IDownloadService
{
    private readonly ILogger<Download> _logger;
    private readonly IFileService _fileService;
    private readonly IMemoryCache _cache;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks;

    public DownloadService(ILogger<Download> logger, IFileService fileService, IMemoryCache cache)
    {
        _logger = logger;
        _fileService = fileService;
        _cache = cache;
        _locks = new ConcurrentDictionary<string, SemaphoreSlim>();
    }

    // TODO: Use result pattern
    public async Task<Download?> GetByIdAsync(Guid id)
    {
        // TODO: Max cache per ipfiles
        var file = await GetRealFile(id);
        if (file == null) return null;
        
        // TODO: fix?
        if (file.Status == FileStatus.Uploading) return null;
        
        var filePath = Path.Combine(file.GetLocation(), $"{file.Id:N}.{file.Extension}");
        
        if (file.Size > Storage.MaxCachedFileSize)
        {
            if (!new FileInfo(filePath).Exists)
            {
                throw new FileNotFoundException(filePath);
            }

            return GetRegularDownload(filePath, file.Name);
        }

        var cachedFile = await GetOrCreateAsync(
            $"download:{file.Id}",
            file.Size,
            async () =>
            {
                _logger.LogInformation("Cache add.");
                if (!new FileInfo(filePath).Exists)
                {
                    throw new FileNotFoundException(filePath);
                }
                // If it becomes an issue don't read all at once
                return await File.ReadAllBytesAsync(filePath);
            });
        
        if (cachedFile is not null)
        {
            _logger.LogInformation("Cache hit.");
            return new Download
            {
                DownloadStream = new MemoryStream(cachedFile),
                FileName = file.Name
            };
        }

        // Fallback
        _logger.LogError("Cache miss.");
        return GetRegularDownload(filePath, file.Name);
    }
    
    private async Task<FileUpload?> GetRealFile(Guid fileId)
    {
        var file = await _fileService.GetByIdAsync(fileId);
        if (file is null || !file.FakeFile)
        {
            return file;
        }

        var fileName = file.Name;
        file = await _fileService.GetByHashAsync(file.Hash);
        
        if (file is null) return null;

        file.Name = fileName;
        
        return file;
    }

    private static Download GetRegularDownload(string filePath, string fileName)
    {
        var stream = new FileStream(filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: Storage.BufferSize,
            useAsync: true);
        
        return new Download
        {
            DownloadStream = stream,
            FileName = fileName
        };
    }
    
    // TODO: Simplify
    private async Task<T?> GetOrCreateAsync<T>(string key, long size, Func<Task<T>> factory)
    {
        if (_cache.TryGetValue(key, out T? value))
        {
            return value;
        }

        var myLock = _locks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));

        await myLock.WaitAsync();
        try
        {
            if (_cache.TryGetValue(key, out value))
            {
                return value;
            }

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
            {
                removedLock.Dispose();
            }
        }
    }
}

public interface IDownloadService
{
    Task<Download?> GetByIdAsync(Guid id);
}