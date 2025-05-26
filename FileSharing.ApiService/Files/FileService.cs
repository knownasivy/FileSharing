using System.Data;
using Amazon.S3;
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

    public async Task<UploadFile> CreateAsync(UploadFile file)
    {
        const string sql = """
                           INSERT INTO files (file_id, 
                                              file_name,
                                              file_ext,
                                              file_size, 
                                              file_type, 
                                              file_status, 
                                              created_at,
                                              file_hash)
                           VALUES (@p1, @p2, @p3, @p4, @p5, @p6, @p7, @p8)
                           """;
        
        await using var connection = await _dataSource.OpenConnectionAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.Add(new NpgsqlParameter("p1", file.Id));
        command.Parameters.Add(new NpgsqlParameter("p2", file.Name));
        command.Parameters.Add(new NpgsqlParameter("p3", file.FileExtension));
        command.Parameters.Add(new NpgsqlParameter("p4", file.Size));
        command.Parameters.Add(new NpgsqlParameter("p5", file.Type.ToString()));
        command.Parameters.Add(new NpgsqlParameter("p6", file.Status.ToString()));
        command.Parameters.Add(new NpgsqlParameter("p7", file.CreatedAt));
        command.Parameters.Add(new NpgsqlParameter("p8", string.Empty));

        await using var rowsAffected = await command.ExecuteReaderAsync();
        // TODO: Check rowsAffected amount? 
        
        return file;
    }

    public async Task<UploadFile?> CompleteAsync(Guid id, string hash, string filePath)
    {
        await using var connection = await _dataSource.OpenConnectionAsync();
        await using var tx = await connection.BeginTransactionAsync();

        const string updateSql = """
                                 UPDATE files
                                 SET file_status = @p1
                                 WHERE file_id   = @p2
                                 """;

        await using (var command = new NpgsqlCommand(updateSql, connection, tx))
        {
            command.Parameters.Add(new("p1", nameof(UploadFile.FileStatus.Uploaded)));
            command.Parameters.Add(new("p2", id));

            await command.ExecuteNonQueryAsync();
        }
        
        var existing = await GetByHashAsync(hash);

        if (existing is not null && existing.Id != id)
        {
            const string typeSql = """
                                   UPDATE files
                                   SET file_type = @p1,
                                       file_hash = @p2
                                   WHERE file_id = @p3
                                   """;
            await using var command = new NpgsqlCommand(typeSql, connection, tx);
            command.Parameters.Add(new("p1", nameof(UploadFile.FileType.Hash)));
            command.Parameters.Add(new("p2", hash));
            command.Parameters.Add(new("p3", id));
            
            await command.ExecuteNonQueryAsync();
            
            File.Delete(filePath);
        }
        else
        {
            const string hashSql = """
                                   UPDATE files
                                   SET file_hash = @p1
                                   WHERE file_id = @p2
                                   """;
            await using var command = new NpgsqlCommand(hashSql, connection, tx);
            command.Parameters.Add(new("p1", hash));
            command.Parameters.Add(new("p2", id));
            
            await command.ExecuteNonQueryAsync();
        }

        await tx.CommitAsync();
        
        return await GetByIdAsync(id);
    }

    public async Task<UploadFile?> GetByIdAsync(Guid id)
    {
        const string sql = """
                           SELECT *
                           FROM files
                           WHERE file_id = @p1
                           """;
        
        await using var connection = await _dataSource.OpenConnectionAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.Add(new NpgsqlParameter("p1", id));

        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            Console.WriteLine("No file found somehow.");
            return null;
        }
        
        /*
            file_id UUID PRIMARY KEY,
            file_name VARCHAR(255) NOT NULL,
            file_size BIGINT NOT NULL,
            file_type file_type_enum NOT NULL,
            file_status file_status_enum NOT NULL,
            created_at DATE NOT NULL
        */
        
        var type = Enum.Parse<UploadFile.FileType>(reader.GetString(4));
        var status = Enum.Parse<UploadFile.FileStatus>(reader.GetString(5));

        return new UploadFile
        {
            Id = reader.GetGuid(0),
            Name = reader.GetString(1),
            FileExtension = reader.GetString(2),
            Size = reader.GetInt64(3),
            Type = type,
            Status = status,
            CreatedAt = reader.GetDateTime(6),
            Hash = reader.GetString(7)
        };
    }

    public async Task<UploadFile?> GetByHashAsync(string hash)
    {
        const string sql = """
                           SELECT *
                           FROM files
                           WHERE file_hash = @p1
                             AND file_type != 'Hash';
                           """;
        
        await using var connection = await _dataSource.OpenConnectionAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.Add(new NpgsqlParameter("p1", hash));

        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
            return null;
        
        /*
            file_id UUID PRIMARY KEY,
            file_name VARCHAR(255) NOT NULL,
            file_size BIGINT NOT NULL,
            file_type file_type_enum NOT NULL,
            file_status file_status_enum NOT NULL,
            created_at DATE NOT NULL
        */
        
        var type = Enum.Parse<UploadFile.FileType>(reader.GetString(4));
        var status = Enum.Parse<UploadFile.FileStatus>(reader.GetString(5));

        return new UploadFile
        {
            Id = reader.GetGuid(0),
            Name = reader.GetString(1),
            FileExtension = reader.GetString(2),
            Size = reader.GetInt64(3),
            Type = type,
            Status = status,
            CreatedAt = reader.GetDateTime(6),
            Hash = reader.GetString(7)
        };
    }

    // TODO: For deduplication?
    /*public async Task<bool> ExistsBySizeAsync(long size)
    {
        const string sql = """
                           SELECT file_id
                           FROM files
                           WHERE file_size = @p1
                           LIMIT 1
                           """;
        
        await using var connection = await _dataSource.OpenConnectionAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.Add(new NpgsqlParameter("p1", size));

        await using var reader = await command.ExecuteReaderAsync();
        
        return await reader.ReadAsync();
    }*/

    public Task<bool> DeleteByIdAsync(Guid id)
    {
        throw new NotImplementedException();
    }
}

public interface IFileService
{
    Task<UploadFile> CreateAsync(UploadFile uploadFile);
    
    Task<UploadFile?> CompleteAsync(Guid id, string hash, string filePath);
    
    Task<UploadFile?> GetByIdAsync(Guid id);
    
    Task<UploadFile?> GetByHashAsync(string hash);
    
    //Task<bool> ExistsBySizeAsync(long size);
    
    Task<bool> DeleteByIdAsync(Guid id);
}