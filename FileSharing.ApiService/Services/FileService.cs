using Dapper;
using FileSharing.ApiService.Models;
using InterpolatedSql.Dapper;
using Npgsql;

namespace FileSharing.ApiService.Services;

public interface IFileService
{
    Task<UploadFile> CreateAsync(UploadFile file);
    Task<UploadFile?> GetByIdAsync(Guid id);
    Task<IEnumerable<UploadFile>> GetAllByUploadIdAsync(Guid id);
    Task<UploadFile?> GetByHashAsync(byte[] hash);
    Task<IEnumerable<UploadFile>> GetAllAsync();
    Task<UploadFile?> CompleteAsync(Guid id, byte[] hash);
    Task<bool> DeleteByIdAsync(Guid id);
}

public class FileService : IFileService
{
    private readonly NpgsqlDataSource _dataSource;
    
    public FileService(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }
    
    // TODO: Do something with rows affected?
    public async Task<UploadFile> CreateAsync(UploadFile file)
    {
        await using var connection = await _dataSource.OpenConnectionAsync();
        await connection.ExecuteAsync(
            """
            INSERT INTO UploadFiles (Id, UploadId, Name, Size, Type, Status, CreatedAt, Hash, FakeFile, IpAddress)
            VALUES (@Id, @UploadId, @Name, @Size, @Type, @Status, @CreatedAt, @Hash, @FakeFile, @IpAddress)
            """, file);
        
        return file;
    }
    
    public async Task<UploadFile?> GetByIdAsync(Guid id)
    {
        await using var connection = await _dataSource.OpenConnectionAsync();
        var query = connection.SqlBuilder($"SELECT * FROM UploadFiles WHERE Id = {id} LIMIT 1");
        return await query.QueryFirstOrDefaultAsync<UploadFile>();
    }

    public async Task<IEnumerable<UploadFile>> GetAllByUploadIdAsync(Guid id)
    {
        await using var connection = await _dataSource.OpenConnectionAsync();
        var query = connection.SqlBuilder($"SELECT * FROM UploadFiles WHERE UploadId = {id}");
        return await query.QueryAsync<UploadFile>();
    }
    
    public async Task<UploadFile?> GetByHashAsync(byte[] hash)
    {
        await using var connection = await _dataSource.OpenConnectionAsync();
        var query = connection.SqlBuilder($"SELECT * FROM UploadFiles WHERE Hash = {hash} AND FakeFile = false LIMIT 1");
        return await query.QueryFirstOrDefaultAsync<UploadFile>();
    }
    
    public async Task<IEnumerable<UploadFile>> GetAllAsync()
    {
        await using var connection = await _dataSource.OpenConnectionAsync();
        return await connection.QueryAsync<UploadFile>("SELECT * FROM UploadFiles");
    }

    // TODO: Do something with rows affected?
    public async Task<UploadFile?> CompleteAsync(Guid id, byte[] hash)
    {
        await using var connection = await _dataSource.OpenConnectionAsync();
        
        var existingFile = GetByIdAsync(id);
        var existingHashFile = GetByHashAsync(hash);
        await Task.WhenAll(existingFile, existingHashFile);
        
        if (existingFile.Result is null)
        {
            return null;
            // TODO: I think result pattern returns: default(UploadFile?);
        }
        
        var file = existingFile.Result;
        
        file.Hash = hash;
        file.Status = FileStatus.Uploaded;
        file.FakeFile = existingHashFile.Result is not null;
        
        await connection.ExecuteAsync(
            """
            UPDATE UploadFiles
            SET Status   = @Status,
                Hash     = @Hash,
                FakeFile = @FakeFile
            WHERE Id     = @Id
            """, file);
        
        return file;
    }
    
    public async Task<bool> DeleteByIdAsync(Guid id)
    {
        await using var connection = await _dataSource.OpenConnectionAsync();
        var query = connection.SqlBuilder($"DELETE FROM UploadFiles WHERE id = {id}");
        var result = await query.ExecuteAsync();
        return result > 0;
    }
}