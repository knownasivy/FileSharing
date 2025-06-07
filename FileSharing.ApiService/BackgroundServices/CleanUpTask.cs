using Dapper;
using FileSharing.ApiService.Services;
using InterpolatedSql.Dapper;
using Npgsql;

namespace FileSharing.ApiService.BackgroundServices;

public class CleanUpTask : IHostedService
{
    private readonly ILogger<CleanUpTask> _logger;
    private readonly NpgsqlDataSource _dataSource;
    private readonly IUploadFileService _uploadFileService;
    private readonly IUploadService _uploadService;
    
    public CleanUpTask(ILogger<CleanUpTask> logger, NpgsqlDataSource dataSource, IUploadFileService uploadFileService, IUploadService uploadService)
    {
        _logger = logger;
        _dataSource = dataSource;
        _uploadFileService = uploadFileService;
        _uploadService = uploadService;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        // TODO: Check if path exists first
        _logger.LogInformation("Running startup task...");
        
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        var anyTablesExist = await connection.ExecuteScalarAsync<bool>(
            """
            SELECT EXISTS (
                SELECT 1
                FROM information_schema.tables
                WHERE table_schema = 'public'
            );
            """);
        

        string[] files = [];
        if (Path.Exists("Uploads"))
        {
            files = Directory.GetFiles("Uploads", "*.*", SearchOption.AllDirectories);
        }
        
        var uploadsExist = files.Length > 0;
        
        if (!uploadsExist)
        {
            if (anyTablesExist)
                await DeleteAllTables(cancellationToken);    
            
            return;
        }

        _logger.LogInformation("API Has Uploads");
        
        if (!anyTablesExist)
        {
            DeleteAllFiles(files);
            return;
        }

        _logger.LogInformation("DB Has Tables");
        
        _logger.LogInformation("Files: {files}", files.Length);
        
        // TODO: Check per file then check per file on db
        foreach (var file in files)
        {
            var idStr = Path.GetFileNameWithoutExtension(file);

            if (!Guid.TryParseExact(idStr, "N", out var id))
            {
                _logger.LogError("Could not parse fild id: {id}", idStr);
                continue;
            }
            
            _logger.LogInformation("Fetching upload by Id: {id}", id);
            var uploadFile = await _uploadFileService.GetByIdAsync(id);
            
            if (uploadFile is null)
            {
                var uploadsExistQuery = connection.SqlBuilder(
                    $"""
                     SELECT EXISTS (
                        SELECT 1
                        FROM   UploadFiles
                        WHERE  FilePath = {file}
                     );
                     """);
                
                var uploadsFound = await uploadsExistQuery.QuerySingleAsync<bool>(
                    cancellationToken: cancellationToken);
                
                if (uploadsFound) 
                    continue;
                
                _logger.LogError("File with path not found: {filePath}", file);
                _logger.LogWarning("Deleting unreferenced: {file}", file);
                File.Delete(file);
                continue;
            }

            if (!uploadFile.FakeFile) continue;
            
            _logger.LogError("Fake file shouldn't have a physical file");
            File.Delete(file);
        }

        var uploads = await _uploadService.GetAllAsync();
        
        foreach (var upload in uploads)
        {
            // TODO: Check if file count is wrong and change?
            var realFileCountQuery = connection.SqlBuilder(
                $"""
                 SELECT COUNT(*)
                 FROM   Uploads
                 WHERE  Id = {upload.Id}
                 """);
            
            var realFileCount = await realFileCountQuery.ExecuteScalarAsync<int>(
                cancellationToken: cancellationToken);

            if (upload.FilesCount != realFileCount)
            {
                _logger.LogError("Files count mismatch: {uploadFiles} != {realFiles}",
                    upload.FilesCount, realFileCount);
                
                _logger.LogInformation("Setting real file count");
                var setFileCountQuery = connection.SqlBuilder(
                    $"""
                     UPDATE Uploads
                     SET FilesCount = {realFileCount}
                     WHERE Id = {upload.Id}
                     """);

                var result = await setFileCountQuery.ExecuteAsync(
                    cancellationToken: cancellationToken);
                
                if (result <= 0)
                {
                    _logger.LogError("Could not set real file count");
                }
            }
            
            if (realFileCount > 0) continue;
            
            _logger.LogWarning("Deleting empty upload: {upload.Id}", upload.Id);
            await _uploadService.DeleteByIdAsync(upload.Id);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task DeleteAllTables(CancellationToken cancellationToken)
    {
        _logger.LogWarning("Clearing up all tables!");
        
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(
            """
            DELETE FROM Uploads;
            DELETE FROM UploadFiles;
            DELETE FROM AudioMetadata;
            DELETE FROM ZipMetadata;
            """);
        
        _logger.LogInformation("PostgreSQL Tables finished deletion.");
    }
    
    private void DeleteAllFiles(string[] files)
    {
        _logger.LogWarning("Deleting all upload files!");
        
        foreach (var file in files)
        {
            _logger.LogWarning("Deleting: {file}", file);    
            File.Delete(file);
        }
        
        _logger.LogInformation("Uploads Directory finished deletion.");
    }
}