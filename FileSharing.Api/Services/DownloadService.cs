using System.Buffers;
using System.Collections.Concurrent;

using FileSharing.Api.Shared;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Net.Http.Headers;

namespace FileSharing.Api.Services;

public interface IDownloadService
{
    Task<IResult> GetByIdAsync(Guid id, string ipAddress, CancellationToken cancellationToken);
    Task<IResult> GetPreviewByIdAsync(Guid id, string ip, string type, string version, CancellationToken token);
    Task<string?> GetPreviewVersionByIdAsync(Guid id);
    Task<IResult> GetCoverByIdAsync(Guid id, CancellationToken token);
}

public class DownloadService : IDownloadService
{
    private readonly ILogger<DownloadService> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ICloudService _cloudService;
    private readonly IUploadFileService _uploadFileService;
    private readonly IMetricsService _metricsService;
    private readonly IMemoryCache _cache;
    private readonly ConcurrentDictionary<string, Lazy<Task<byte[]?>>> _ongoingOperations = new();
    
    public DownloadService(
        ILogger<DownloadService> logger, 
        IHttpClientFactory httpClientFactory,
        ICloudService cloudService,
        IUploadFileService uploadFileService, 
        IMetricsService metricsService, 
        IMemoryCache cache)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _cloudService = cloudService;
        _uploadFileService = uploadFileService;
        _metricsService = metricsService;
        _cache = cache;
    }
    
    // TODO: Use result pattern
    public async Task<IResult> GetByIdAsync(
        Guid id, 
        string ipAddress, 
        CancellationToken ct = default)
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
        if (downloadFile.Size > StorageConfig.MaxCachedFileSize)
            return GetFileDownload(downloadFile, contentType, bufferSize, ipAddress);
        
        try
        {
            var cachedFile = await GetOrCreateAsync(
                $"download:{downloadFile.RealId}",
                async () =>
                {
                    if (!File.Exists(downloadFile.FilePath))
                        throw new FileNotFoundException();

                    return await File.ReadAllBytesAsync(downloadFile.FilePath, ct);
                });
        
            if (cachedFile is not null)
            {
                _metricsService.RecordDownload(ipAddress, cachedFile.Length);
                return Results.Bytes(
                    cachedFile, 
                    contentType, 
                    downloadFile.Name, 
                    enableRangeProcessing: true);
            }
        }
        catch(FileNotFoundException)
        {
            return Results.NotFound();
        }
        
        // Fallback
        return GetFileDownload(downloadFile, contentType, bufferSize, ipAddress);
    }


    public async Task<string?> GetPreviewVersionByIdAsync(Guid id)
    {
        var downloadFile = await _uploadFileService.GetDownloadFileByIdAsync(id);
        if (downloadFile is null)
        {
            _logger.LogError("File not found");
            return null;
        }
        
        var previewFileName = $"{downloadFile.RealId}_prev";
        if (await _cloudService.GetExistsAsync(previewFileName))
        {
            return "normal";
        }
        
        var tempPreview = $"{downloadFile.FilePath.Split('.')[0]}_prev.mp4";
        return File.Exists(tempPreview) ? 
            "fast" : null;
    }
    public async Task<IResult> GetPreviewByIdAsync(Guid id, string ip, string type, string version, CancellationToken token)
    {
        var contentType = type switch
        {
            "m4a" => "audio/mp4",
            "mp4" => "video/mp4",
            _ => throw new Exception("Impossible")
        };
        
        var downloadFile = await _uploadFileService.GetDownloadFileByIdAsync(id);
        if (downloadFile is null)
        {
            _logger.LogError("File not found");
            return Results.NotFound();
        }
        
        var previewFileName = $"{downloadFile.RealId}_prev";
        var fastCacheKey = $"fast:{nameof(GetPreviewByIdAsync)}:{downloadFile.RealId}";
        var normalCacheKey = $"normal:{nameof(GetPreviewByIdAsync)}:{downloadFile.RealId}";
        var tempPreview = $"{downloadFile.FilePath.Split('.')[0]}_prev.mp4";
        
        if (version == "fast")
        {
            _logger.LogInformation("Grabbing file {TempPreview}", tempPreview);
            
            // could fall back to "normal" even though "fast" is technically a fallback
            if (!File.Exists(tempPreview)) return await GetPreviewByIdAsync(id, ip, type, "normal", token);
            
            var fileInfo = new FileInfo(tempPreview);

            // Add to cache so fast file doesnt get deleted when being used
            await _cache.GetOrCreateAsync(
                fastCacheKey,
                entry =>
                {
                    entry.SlidingExpiration = TimeSpan.FromMinutes(3);
                    entry.Size = 0;
                    return Task.FromResult(true);
                });
                
            return Results.Stream(
                stream: new FileStream(
                    tempPreview, 
                    FileMode.Open, 
                    FileAccess.Read, 
                    FileShare.Read, 
                    BytesSize.KiB * 128),
                contentType: contentType,
                fileDownloadName: $"{previewFileName}.{type}",
                lastModified: fileInfo.LastWriteTime,
                entityTag: new EntityTagHeaderValue($"\"{fileInfo.LastWriteTime.Ticks:X}\""),
                enableRangeProcessing: true
            );
        }

        if (version != "normal") return Results.InternalServerError();
        
        var bytes = await GetOrCreateAsync(
            normalCacheKey,
            async () =>
            {
                var url = await _cloudService.GetPreviewFileUrl(previewFileName);
                if (url is null) return null;

                var success = true;
                
                try
                {
                    var client = _httpClientFactory.CreateClient();
                    await using var responseStream = await client.GetStreamAsync(url, token);
                    var outStream = new MemoryStream();
                    await responseStream.CopyToAsync(outStream, token);
                    return outStream.ToArray();
                }
                catch (HttpRequestException)
                {
                    success = false;
                    return null;
                }
                finally
                {
                    if (success && !_cache.TryGetValue(fastCacheKey, out _) && File.Exists(tempPreview))
                        File.Delete(tempPreview);
                }
            });

        if (bytes is null)
        {
            if (File.Exists(tempPreview))
                return await GetPreviewByIdAsync(id, ip, type, "fast", token);
            
            return Results.NotFound("bytes null");
        } 
        
        _metricsService.RecordDownload(ip, bytes.Length);
        var stream = new MemoryStream(bytes, writable: false);
        
        return Results.Stream(
            stream: stream,
            contentType: contentType,
            fileDownloadName: $"{previewFileName}.{type}",
            entityTag: new EntityTagHeaderValue($"\"{id}\""),
            enableRangeProcessing: true
        );
    }

    public async Task<IResult> GetCoverByIdAsync(Guid id, CancellationToken token)
    {
        var downloadFile = await _uploadFileService.GetDownloadFileByIdAsync(id);
        if (downloadFile is null)
        {
            _logger.LogError("File not found");
            return Results.NotFound();
        }

        var coverFileName = $"{downloadFile.RealId}_cover";
        var cacheKey = $"{nameof(GetCoverByIdAsync)}:{downloadFile.RealId}";

        var bytes = await GetOrCreateAsync(
            cacheKey,
            async () =>
            {
                var url = await _cloudService.GetPreviewFileUrl(coverFileName);
                if (url is null) return null;
                
                try
                {
                    var client = _httpClientFactory.CreateClient();
                    await using var responseStream = await client.GetStreamAsync(url, token);
                    var outStream = new MemoryStream();
                    await responseStream.CopyToAsync(outStream, token);
                    return outStream.ToArray();
                }
                catch (HttpRequestException) { return null; }
            });

        if (bytes is null)
        {
            _logger.LogInformation("Bytes null");
            return Results.NotFound();
        }
        
        var stream = new MemoryStream(bytes, writable: false);
        
        return Results.Stream(
            stream: stream,
            contentType: "image/webp",
            fileDownloadName: $"{coverFileName}.webp",
            entityTag: new EntityTagHeaderValue($"\"{id}\"")
        );
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
            return Results.File(
                new FileStream(
                    downloadFile.FilePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    bufferSize,
                    useAsync: true), 
                contentType, 
                downloadFile.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create file stream for {filePath}", downloadFile.FilePath);
            return Results.InternalServerError();
        }
    }
    
    // TODO: Simplify?
    private async Task<byte[]?> GetOrCreateAsync(string key, Func<Task<byte[]?>> factory, int minutes = 5)
    {
        if (_cache.TryGetValue(key, out byte[]? cachedValue)) 
            return cachedValue;
        
        var lazy = _ongoingOperations.GetOrAdd(key, _ =>
            new Lazy<Task<byte[]?>>(() =>
                    _cache.GetOrCreateAsync(key, async entry =>
                    {
                        byte[]? val;
                        try
                        {
                            val = await factory();
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to create file stream for {filePath}", key);
                            val = null;
                        }

                        if (val is null)
                        {
                            entry.SlidingExpiration = TimeSpan.FromMinutes(1);
                            entry.Size = 0;
                            return val;
                        }
                        
                        entry.SlidingExpiration = TimeSpan.FromMinutes(minutes);
                        entry.Size = val.Length;
                        
                        return val;
                    })
                , LazyThreadSafetyMode.ExecutionAndPublication)
        );
        
        try
        {
            return await lazy.Value;
        }
        finally
        {
            _ongoingOperations.TryRemove(key, out _);
        }
    }
}

