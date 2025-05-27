using System.Data;
using Amazon.S3;
using Dapper;
using Npgsql;

namespace FileSharing.ApiService.Files;

public class FileService : IFileService
{
    // TODO: Switch to dapper and use InterpolatedSql?
    
    private readonly NpgsqlDataSource _dataSource;
    private readonly IAmazonS3 _s3;
    
    public FileService(NpgsqlDataSource dataSource, IAmazonS3 s3)
    {
        _dataSource = dataSource;
        _s3 = s3;
    }
    
    // TODO: Do something with rows affected?
    public async Task<FileUpload> CreateAsync(FileUpload file)
    {
        // TODO: Validation?
        
        await using var connection = await _dataSource.OpenConnectionAsync();
        await connection.ExecuteAsync(
            """
            insert into files (id, name, size, type, status, createdat, hash, fakefile)
            values (@Id, @Name, @Size, @Type, @Status, @CreatedAt, @Hash, @FakeFile)
            """, file);
        
        return file;
    }



    public async Task<FileUpload?> GetByIdAsync(Guid id)
    {
        await using var connection = await _dataSource.OpenConnectionAsync();
        var file = await connection.QueryFirstOrDefaultAsync<FileUpload>(
            "select * from files where id=@id limit 1", new { id });
        return file;
    }

    public async Task<FileUpload?> GetByHashAsync(byte[] hash)
    {

        await using var connection = await _dataSource.OpenConnectionAsync();
        var file = await connection.QueryFirstOrDefaultAsync<FileUpload>(
            "select * from files where hash=@hash and fakefile = false limit 1", new { hash });
        return file;
    }
    
    public async Task<IEnumerable<FileUpload>> GetAllAsync()
    {
        await using var connection = await _dataSource.OpenConnectionAsync();
        return await connection.QueryAsync<FileUpload>("select * from files");
    }

    // TODO: Do something with rows affected?
    public async Task<FileUpload?> CompleteAsync(Guid id, byte[] hash, string filePath)
    {
        await using var connection = await _dataSource.OpenConnectionAsync();
        
        var existingFile = GetByIdAsync(id);
        var existingHashFile = GetByHashAsync(hash);
        await Task.WhenAll(existingFile, existingHashFile);
        
        if (existingFile.Result is null)
        {
            return null;
            // TODO: I think result pattern returns: default(FileUpload?);
        }
        
        var file = existingFile.Result;
        
        file.Hash = hash;
        file.Status = FileStatus.Uploaded;
        file.FakeFile = existingHashFile.Result is not null;
        
        await connection.ExecuteAsync(
            """
            update files
            set status=@Status,
                hash=@Hash,
                fakefile=@FakeFile
            where id=@Id
            """, file);

        if (file.FakeFile && File.Exists(filePath))
        {
            File.Delete(filePath);
        }

        return file;
    }
    
    public async Task<bool> DeleteByIdAsync(Guid id)
    {
        await using var connection = await _dataSource.OpenConnectionAsync();
        var result = await connection.ExecuteAsync("delete from files where id = @id", new { id });
        return result > 0;
    }
}

public interface IFileService
{
    Task<FileUpload> CreateAsync(FileUpload fileUpload);
    
    Task<FileUpload?> GetByIdAsync(Guid id);
    
    Task<FileUpload?> GetByHashAsync(byte[] hash);

    Task<IEnumerable<FileUpload>> GetAllAsync(); // TODO: Filter?
    
    Task<FileUpload?> CompleteAsync(Guid id, byte[] hash, string filePath);
    
    Task<bool> DeleteByIdAsync(Guid id);
}