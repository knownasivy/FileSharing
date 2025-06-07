using Dapper;
using FileSharing.ApiService.Models;
using InterpolatedSql.Dapper;
using Microsoft.Extensions.Caching.Hybrid;
using Npgsql;

namespace FileSharing.ApiService.Services;

public record DownloadFile(string RealId, string Name, long Size, string FilePath)
{
    public DownloadFile(string name, int size, string filePath)
        : this(
            Path.GetFileNameWithoutExtension(filePath),
            name,
            size,
            filePath)
    {
    }
}

public interface IUploadFileService
{
    Task<UploadFile> CreateAsync(UploadFile file);
    Task<UploadFile?> GetByIdAsync(Guid id);
    Task<DownloadFile?> GetDownloadFileByIdAsync(Guid id);
    Task<UploadFile?> GetRealByHashAsync(byte[] hash);
    Task<IEnumerable<UploadFile>> GetAllByFilePathAsync(string filePath);
    Task<IEnumerable<UploadFile>?> GetAllByUploadIdAsync(Guid id);
    Task<IEnumerable<UploadFile>> GetAllAsync();
    Task<UploadFile?> CompleteAsync(UploadFile file, byte[] hash);
    Task<bool> DeleteByIdAsync(Guid id);
}

public class UploadFileService : IUploadFileService
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly HybridCache _cache;
    private readonly IUploadService _uploadService;
    
    public UploadFileService(NpgsqlDataSource dataSource, HybridCache cache, IUploadService uploadService)
    {
        _dataSource = dataSource;
        _cache = cache;
        _uploadService = uploadService;
    }
    
    // TODO: Do something with rows affected?
    public async Task<UploadFile> CreateAsync(UploadFile file)
    {
        await using var connection = await _dataSource.OpenConnectionAsync();
        await connection.ExecuteAsync(
            """
            INSERT INTO UploadFiles(
              Id, UploadId, Name, Size, Type, Status, 
              CreatedAt, Hash, FakeFile, IpAddress, 
              FilePath
            ) 
            VALUES 
              (
                @Id, @UploadId, @Name, @Size, @Type, 
                @Status, @CreatedAt, @Hash, @FakeFile, 
                @IpAddress, @FilePath
              )
            """, file);
        
        return file;
    }
    
    public async Task<UploadFile?> GetByIdAsync(Guid id)
    {
        return await _cache.GetOrCreateAsync(
            $"{nameof(GetDownloadFileByIdAsync)}:{id}",
            async token => await FromDb(token),
            options: new HybridCacheEntryOptions
            {
                LocalCacheExpiration = TimeSpan.FromMinutes(5),
                Expiration = TimeSpan.FromMinutes(5)
            },
            cancellationToken: CancellationToken.None
        );

        async Task<UploadFile?> FromDb(CancellationToken ct)
        {
            await using var connection = await _dataSource.OpenConnectionAsync(ct);
            var query = connection.SqlBuilder($"SELECT * FROM UploadFiles WHERE Id = {id} LIMIT 1");
            return await query.QueryFirstOrDefaultAsync<UploadFile>(cancellationToken: ct);
        }
    }
    
    public async Task<DownloadFile?> GetDownloadFileByIdAsync(Guid id)
    {
        await using var connection = await _dataSource.OpenConnectionAsync();
        var query = connection.SqlBuilder(
            $"""
             SELECT Name, Size, FilePath
             FROM UploadFiles
             WHERE Id = {id}
             LIMIT 1                              
             """);
        
        return await query.QueryFirstOrDefaultAsync<DownloadFile>();
    }
    
    public async Task<UploadFile?> GetRealByHashAsync(byte[] hash)
    {
        await using var connection = await _dataSource.OpenConnectionAsync();
        var query = connection.SqlBuilder(
            $"""
             SELECT *
             FROM UploadFiles
             WHERE Hash     = {hash}
               AND FakeFile = false 
             LIMIT 1                              
             """);
        
        return await query.QueryFirstOrDefaultAsync<UploadFile>();
    }
    
    public async Task<IEnumerable<UploadFile>> GetAllByFilePathAsync(string filePath)
    {
        await using var connection = await _dataSource.OpenConnectionAsync();
        var query = connection.SqlBuilder(
            $"""
             SELECT *
             FROM UploadFiles
             WHERE FilePath = {filePath}
             ORDER BY CreatedAt
             """);
        
        return await query.QueryAsync<UploadFile>();
    }
    
    // TODO: Result types instead null?
    public async Task<IEnumerable<UploadFile>?> GetAllByUploadIdAsync(Guid uploadId)
    {
        return await _cache.GetOrCreateAsync(
            $"{nameof(GetAllByUploadIdAsync)}:{uploadId}",
            async token => await FromDb(token),
            options: new HybridCacheEntryOptions
            {
                LocalCacheExpiration = TimeSpan.FromMinutes(5),
                Expiration = TimeSpan.FromMinutes(5)
            },
            cancellationToken: CancellationToken.None
        );

        async Task<IEnumerable<UploadFile>?> FromDb(CancellationToken ct)
        {
            await using var connection = await _dataSource.OpenConnectionAsync(ct);
            var existsQuery = connection.SqlBuilder(
                $"""
                 SELECT EXISTS (
                     SELECT 1
                     FROM   Uploads
                     WHERE  Id = {uploadId}
                 );
                 """);

            var exists = await existsQuery.QuerySingleAsync<bool>(
                cancellationToken: ct);
        
            if (!exists) return null;
        
            var query = connection.SqlBuilder(
                $"""
                 SELECT * 
                 FROM UploadFiles
                 WHERE UploadId = {uploadId}
                 ORDER BY CreatedAt
                 """);
        
            return await query.QueryAsync<UploadFile>(
                cancellationToken: ct);
        }
    }
    
    public async Task<IEnumerable<UploadFile>> GetAllAsync()
    {
        await using var connection = await _dataSource.OpenConnectionAsync();
        return await connection.QueryAsync<UploadFile>("SELECT * FROM UploadFiles ORDER BY CreatedAt");
    }

    // TODO: Do something with rows affected?
    public async Task<UploadFile?> CompleteAsync(UploadFile file, byte[] hash)
    {
        await using var connection = await _dataSource.OpenConnectionAsync();
        
        var existsQuery = connection.SqlBuilder(
            $"""
             SELECT EXISTS (
                 SELECT 1
                 FROM   UploadFiles
                 WHERE  Id = {file.Id}
             );
             """);
        
        var fileExists = await existsQuery.QuerySingleAsync<bool>();
        
        if (!fileExists) return null;
        
        var hashFilePathQuery = connection.SqlBuilder(
            $"""
             SELECT FilePath 
             FROM UploadFiles 
             WHERE Hash     = {hash} 
               AND FakeFile = false 
             LIMIT 1
             """);
        
        var hashFilePath = await hashFilePathQuery.QueryFirstOrDefaultAsync<string>();

        file.Hash = hash;
        file.Status = FileStatus.Uploaded;
        file.FakeFile = hashFilePath is not null;

        file.FilePath = file.FakeFile ?
            hashFilePath : file.GetThisFilePath();
        
        await connection.ExecuteAsync(
            """
            UPDATE UploadFiles
            SET Status   = @Status,
                Hash     = @Hash,
                FakeFile = @FakeFile,
                FilePath = @FilePath
            WHERE Id     = @Id
            """, file);
        
        return file;
    }
    
    public async Task<bool> DeleteByIdAsync(Guid id)
    {
        var uploadFile = await GetByIdAsync(id);
        if (uploadFile is null) return false;
        
        await using var connection = await _dataSource.OpenConnectionAsync();
        if (!uploadFile.FakeFile)
        {
            var fakeFilesQuery = connection.SqlBuilder(
                $"""
                SELECT COUNT(*) 
                FROM UploadFiles 
                WHERE Hash     = {uploadFile.Hash} 
                  AND FakeFile = true
                """);
            
            var fakeFiles = await fakeFilesQuery.ExecuteScalarAsync<int>();
            
            if (fakeFiles <= 0)
            {
                if (File.Exists(uploadFile.FilePath))
                    File.Delete(uploadFile.FilePath);
            }
        }
        
        await _uploadService.DecreaseFileCountByIdAsync(uploadFile.UploadId, 1);
        
        var query = connection.SqlBuilder($"DELETE FROM UploadFiles WHERE Id = {id}");
        var result = await query.ExecuteAsync();
        return result > 0;
    }
}