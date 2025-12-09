using System.Text.Json.Serialization;
using Dapper;
using FileSharing.Api.Database;
using FileSharing.Api.Models;
using FileSharing.Api.Shared;
using InterpolatedSql.Dapper;
using Microsoft.Extensions.Caching.Hybrid;
using Npgsql;
using ZLinq;

namespace FileSharing.Api.Services;

public class UploadFileService : IUploadFileService
{
    private const string CachePrefix = nameof(UploadFileService);
    
    private readonly ILogger<UploadFileService> _logger;
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly HybridCache _cache;
    
    public UploadFileService(
        ILogger<UploadFileService> logger,
        IDbConnectionFactory connectionFactory,
        HybridCache cache)
    {
        _logger = logger;
        _connectionFactory = connectionFactory;
        _cache = cache;
    }
    
    // TODO: Do something with rows affected?
    public async Task<UploadFile?> CreateAsync(UploadFile file)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync();
        await connection.ExecuteAsync(
            """
            INSERT INTO UploadFiles(Id, Name, Size, Type, Status, CreatedAt, Hash, FakeFile, IpAddress, FilePath) 
            VALUES (@Id, @Name, @Size, @Type, @Status, @CreatedAt, @Hash, @FakeFile, @IpAddress, @FilePath)
            """, file);
        
        return file;
    }

    public async Task<UploadFile?> GetByIdAsync(Guid id)
        => await _cache.WithHybridCacheAsync(
            $"{CachePrefix}:by_id:{id}",
            async ct =>
            {
                await using var connection = await _connectionFactory.CreateConnectionAsync(ct);
                var query = connection.SqlBuilder(
                    $"SELECT * FROM UploadFiles WHERE Id = {id} LIMIT 1");
                return await query.QueryFirstOrDefaultAsync<UploadFile>(cancellationToken: ct);
            }, TimeSpan.FromMinutes(5));
    
    public async Task<DownloadFile?> GetDownloadFileByIdAsync(Guid id)
        => await _cache.WithHybridCacheAsync(
            $"{CachePrefix}:download_file_by_id:{id}",
            async ct =>
            {
                await using var connection = await _connectionFactory.CreateConnectionAsync(ct);
                var query = connection.SqlBuilder(
                    $"SELECT Name, Size, FilePath FROM UploadFiles WHERE Id = {id} LIMIT 1");
                return await query.QueryFirstOrDefaultAsync<DownloadFile>(cancellationToken: ct);
            }, TimeSpan.FromMinutes(3));
    
    public async Task<IEnumerable<UploadFile>> GetAllPaginatedAsync(int offset, int limit)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync();
        var query = connection.SqlBuilder(
            $"SELECT * FROM UploadFiles ORDER BY CreatedAt OFFSET {offset} LIMIT {limit}");
        return await query.QueryAsync<UploadFile>();
    }

    public async Task<bool> GetIsFileTypeById(Guid id, FileType type)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync();
        var existsQuery = connection.SqlBuilder(
            $"""
             SELECT EXISTS (
                 SELECT 1
                 FROM UploadFiles
                 WHERE Id   = {id} 
                   AND Type = {type}
             );
             """);
        
        return await existsQuery.QuerySingleAsync<bool>();
    }
    
    public async Task<UploadFile?> CompleteAsync(UploadFile file, byte[] hash, long size)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync();
        
        var query = connection.SqlBuilder(
            $"""
             UPDATE UploadFiles
             SET
                 Size     = {size},
                 Status   = {FileStatus.Uploaded},
                 Hash     = {hash},
                 FakeFile = EXISTS(
                     SELECT 1
                     FROM UploadFiles
                     WHERE Hash     = {hash}
                       AND FakeFile = false
                 ),
                 FilePath = COALESCE(
                     (SELECT FilePath
                      FROM UploadFiles
                      WHERE Hash     = {hash}
                        AND FakeFile = false
                      LIMIT 1),
                     {file.GetThisFilePath()}
                 )
             WHERE Id = {file.Id}
             RETURNING *;
             """);

        var result = await query.QuerySingleAsync<UploadFile>();

        await _cache.RemoveAsync($"{CachePrefix}:{nameof(GetByIdAsync)}:{file.Id}");
        await _cache.RemoveAsync($"{CachePrefix}:{nameof(GetDownloadFileByIdAsync)}:{file.Id}");
        
        return result;
    }
    
    public async Task<bool> DeleteByIdAsync(Guid id)
    {
        var uploadFile = await GetByIdAsync(id);
        if (uploadFile is null) return false;
        
        await using var connection = await _connectionFactory.CreateConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
    
        try
        {
            var query = connection.SqlBuilder($"DELETE FROM UploadFiles WHERE Id = {id}");
            var result = await query.ExecuteAsync(
                transaction: transaction);
        
            if (result > 0 && !uploadFile.FakeFile)
            {
                var existsQuery = connection.SqlBuilder(
                    $"""
                     SELECT EXISTS (
                         SELECT 1
                         FROM UploadFiles
                         WHERE Hash = {uploadFile.Hash} 
                           AND FakeFile = true
                     );
                     """);
            
                var fakeFileExists = await existsQuery.QuerySingleAsync<bool>(
                    transaction: transaction);

                if (!fakeFileExists && File.Exists(uploadFile.FilePath))
                {
                    try
                    {
                        File.Delete(uploadFile.FilePath);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to delete file {FilePath}", uploadFile.FilePath);
                    }
                }
            }
        
            await transaction.CommitAsync();

            if (result <= 0) return false;
            
            await _cache.RemoveAsync($"{CachePrefix}:{nameof(GetByIdAsync)}:{id}");
            await _cache.RemoveAsync($"{CachePrefix}:{nameof(GetDownloadFileByIdAsync)}:{id}");
            
            return true;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
}

public interface IUploadFileService
{
    Task<UploadFile?> CreateAsync(UploadFile file);
    Task<UploadFile?> GetByIdAsync(Guid id);
    Task<DownloadFile?> GetDownloadFileByIdAsync(Guid id);
    Task<IEnumerable<UploadFile>> GetAllPaginatedAsync(int offset, int limit);
    Task<bool> GetIsFileTypeById(Guid id, FileType fileType);
    Task<UploadFile?> CompleteAsync(UploadFile file, byte[] hash, long size);
    Task<bool> DeleteByIdAsync(Guid id);
}

[method: JsonConstructor]
public record DownloadFile(string RealId, string Name, long Size, string FilePath)
{
    public string RealId { get; set; } = RealId;
    public string Name { get; set; } = Name;
    public long Size { get; set; } = Size;
    public string FilePath { get; set; } = FilePath;

    public DownloadFile(string name, int size, string filePath)
        : this(Path.GetFileNameWithoutExtension(filePath), name, size, filePath)
    {
    }
}