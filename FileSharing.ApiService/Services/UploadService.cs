using Dapper;
using FileSharing.ApiService.Models;
using InterpolatedSql.Dapper;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Options;
using Npgsql;

namespace FileSharing.ApiService.Services;

public interface IUploadService
{
    Task<Upload> CreateAsync(string ipAddress);
    Task<Upload?> GetByIdAsync(Guid id);
    Task<IEnumerable<Upload>> GetAllAsync();
    Task<bool> IncreaseFileCountByIdAsync(Guid id, int incAmount);
    Task<bool> DecreaseFileCountByIdAsync(Guid id, int decAmount);
    Task<bool> DeleteByIdAsync(Guid id);
}

public class UploadService : IUploadService
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly HybridCache _cache;
    
    public UploadService(NpgsqlDataSource dataSource, HybridCache cache)
    {
        _dataSource = dataSource;
        _cache = cache;
    }
    
    // TODO: Abstract out caching?
    
    public async Task<Upload> CreateAsync(string ipAddress)
    {
        var upload = new Upload
        {
            IpAddress = ipAddress
        };
        
        await using var connection = await _dataSource.OpenConnectionAsync();
        await connection.ExecuteAsync(
            """
            INSERT INTO Uploads (
              Id, CreatedAt, FilesCount, IpAddress
            ) 
            VALUES 
              (
                @Id, @CreatedAt, @FilesCount, @IpAddress
              )
            """, upload);
        
        return upload;
    }

    public async Task<Upload?> GetByIdAsync(Guid id)
    {
        return await _cache.GetOrCreateAsync(
            $"{nameof(GetByIdAsync)}:{id}",
            async token => await FromDb(token),
            options: new HybridCacheEntryOptions
            {
                LocalCacheExpiration = TimeSpan.FromMinutes(5),
                Expiration = TimeSpan.FromMinutes(5)
            },
            cancellationToken: CancellationToken.None
        );
        
        async Task<Upload?> FromDb(CancellationToken ct)
        {
            await using var connection = await _dataSource.OpenConnectionAsync(ct);
            var query = connection.SqlBuilder($"SELECT * FROM Uploads WHERE Id = {id} LIMIT 1");
            return await query.QueryFirstOrDefaultAsync<Upload>(
                cancellationToken: ct);
        }
    }

    public async Task<IEnumerable<Upload>> GetAllAsync()
    {
        await using var connection = await _dataSource.OpenConnectionAsync();
        return await connection.QueryAsync<Upload>("SELECT * FROM Uploads");
    }
    
    public async Task<bool> IncreaseFileCountByIdAsync(Guid id, int incAmount)
    {
        await using var connection = await _dataSource.OpenConnectionAsync();
        var query = connection.SqlBuilder(
            $"""
             UPDATE Uploads
             SET FilesCount = FilesCount + {incAmount}
             WHERE Id = {id}
             """);
        var result = await query.ExecuteAsync();
        return result > 0;
    }
    
    public async Task<bool> DecreaseFileCountByIdAsync(Guid id, int decAmount)
    {
        await using var connection = await _dataSource.OpenConnectionAsync();
        var query = connection.SqlBuilder(
            $"""
             UPDATE Uploads
             SET FilesCount = FilesCount - {decAmount}
             WHERE Id = {id}
             """);
        
        var result = await query.ExecuteAsync();
        
        // TODO: Delete if 0 ?
        
        return result > 0;
    }
    
    public async Task<bool> DeleteByIdAsync(Guid id)
    {
        await using var connection = await _dataSource.OpenConnectionAsync();
        var query = connection.SqlBuilder($"DELETE FROM Uploads WHERE Id = {id}");
        var result = await query.ExecuteAsync();
        return result > 0;
    }
}