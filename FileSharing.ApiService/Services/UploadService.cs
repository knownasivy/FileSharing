using Dapper;
using FileSharing.ApiService.Models;
using InterpolatedSql.Dapper;
using Npgsql;

namespace FileSharing.ApiService.Services;

public interface IUploadService
{
    Task<Upload> CreateAsync(string ipAddress);
    Task<Upload?> GetByIdAsync(Guid id);
    Task<IEnumerable<Upload>> GetAllAsync();
    Task<bool> DeleteByIdAsync(Guid id);
}

public class UploadService : IUploadService
{
    private readonly NpgsqlDataSource _dataSource;
    
    public UploadService(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }
    
    public async Task<Upload> CreateAsync(string ipAddress)
    {
        var upload = new Upload
        {
            IpAddress = ipAddress
        };
        
        await using var connection = await _dataSource.OpenConnectionAsync();
        await connection.ExecuteAsync(
            """
            INSERT INTO Uploads (Id, CreatedAt, Status, IpAddress)
            VALUES (@Id, @CreatedAt, @Status, @IpAddress)
            """, upload);
        
        return upload;
    }

    public async Task<Upload?> GetByIdAsync(Guid id)
    {
        await using var connection = await _dataSource.OpenConnectionAsync();
        var query = connection.SqlBuilder($"SELECT * FROM Uploads WHERE Id = {id} LIMIT 1");
        return await query.QueryFirstOrDefaultAsync<Upload>();
    }

    public async Task<IEnumerable<Upload>> GetAllAsync()
    {
        await using var connection = await _dataSource.OpenConnectionAsync();
        return await connection.QueryAsync<Upload>("SELECT * FROM Uploads");
    }

    public Task<bool> DeleteByIdAsync(Guid id)
    {
        throw new NotImplementedException();
    }
}