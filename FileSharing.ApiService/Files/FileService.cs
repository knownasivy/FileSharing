﻿using System.Data;
using Amazon.S3;
using Dapper;
using InterpolatedSql.Dapper;
using Npgsql;

namespace FileSharing.ApiService.Files;

public class FileService : IFileService
{
    private readonly NpgsqlDataSource _dataSource;
    
    public FileService(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }
    
    // TODO: Do something with rows affected?
    public async Task<FileUpload> CreateAsync(FileUpload file)
    {
        // TODO: Validation?
        
        await using var connection = await _dataSource.OpenConnectionAsync();
        await connection.ExecuteAsync(
            """
            INSERT INTO Files (Id, Name, Size, Type, Status, CreatedAt, Hash, FakeFile, IPAddress)
            VALUES (@Id, @Name, @Size, @Type, @Status, @CreatedAt, @Hash, @FakeFile, @IPAddress)
            """, file);
        
        return file;
    }
    
    public async Task<FileUpload?> GetByIdAsync(Guid id)
    {
        await using var connection = await _dataSource.OpenConnectionAsync();
        var query = connection.SqlBuilder($"SELECT * FROM Files WHERE Id = {id} LIMIT 1");
        return await query.QueryFirstOrDefaultAsync<FileUpload>();
    }

    public async Task<FileUpload?> GetByHashAsync(byte[] hash)
    {
        await using var connection = await _dataSource.OpenConnectionAsync();
        var query = connection.SqlBuilder($"SELECT * FROM Files WHERE Hash = {hash} AND FakeFile = false LIMIT 1");
        return await query.QueryFirstOrDefaultAsync<FileUpload>();
    }
    
    public async Task<IEnumerable<FileUpload>> GetAllAsync()
    {
        await using var connection = await _dataSource.OpenConnectionAsync();
        return await connection.QueryAsync<FileUpload>("SELECT * FROM Files");
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
            UPDATE Files
            SET Status   = @Status,
                Hash     = @Hash,
                FakeFile = @FakeFile
            WHERE Id     = @Id
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
        var query = connection.SqlBuilder($"DELETE FROM Files WHERE id = {id}");
        var result = await query.ExecuteAsync();
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