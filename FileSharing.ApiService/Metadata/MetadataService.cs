using Amazon.S3;
using Dapper;
using FileSharing.ApiService.Files;
using FileSharing.ApiService.Metadata.Factories;
using FileSharing.ApiService.Metadata.Types;
using InterpolatedSql.Dapper;
using Npgsql;

namespace FileSharing.ApiService.Metadata;

public class MetadataService : IMetadataService
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly IAmazonS3 _s3;
    private readonly ILogger<MetadataService> _logger;
    private readonly Dictionary<FileType, IMetadataProcessorFactory> _factories;
    
    public MetadataService(NpgsqlDataSource dataSource, ILogger<MetadataService> logger, IAmazonS3 s3)
    {
        _dataSource = dataSource;
        _s3 = s3;
        _logger = logger;
        _factories = new Dictionary<FileType, IMetadataProcessorFactory>
        {
            { FileType.Audio, new AudioMetadataProcessorFactory() },
            { FileType.Image, new ImageMetadataProcessorFactory() },
            { FileType.Archive, new ArchiveMetadataProcessorFactory() }
        };
    }

    public async Task<IMetadata?> CreateAsync(FileUpload file, string filePath)
    {
        if (file.Status == FileStatus.Uploading) throw new Exception("Impossible");

        if (!_factories.TryGetValue(file.Type, out var factory))
        {
            throw new Exception("Unknown file type");
        }
        
        var metadata = await factory.CreateAsync(file, filePath, file.Type != FileType.Archive ? _s3 : null);
        if (metadata is null)
        {
            _logger.LogError("Metadata is null");
            return null;
        }
        

        if (metadata is AudioMetadata audio)
        {
            await using var connection = await _dataSource.OpenConnectionAsync();
            
            await connection.ExecuteAsync(
                """
                INSERT INTO AudioMetadata (FileId, Title, Album, Artist, AttachedPic)
                VALUES (@FileId, @Title, @Album, @Artist, @AttachedPic)
                """, audio);
            
            return audio;
        }

        if (metadata is ArchiveMetadata archive)
        {
            await using var connection = await _dataSource.OpenConnectionAsync();
            
            await connection.ExecuteAsync(
                """
                INSERT INTO ArchiveMetadata (FileId, Password, Files)
                VALUES (@FileId, @Password, @Files)
                """, archive);
            
            return archive;
        }
        
        if (metadata is ImageMetadata image)
        {
            await using var connection = await _dataSource.OpenConnectionAsync();
            
            await connection.ExecuteAsync(
                """
                INSERT INTO ImageMetadata (FileId, Size)
                VALUES (@FileId, @Size)
                """, image);
            
            return image;
        }
        
        throw new Exception("Impossible");
    }
    
    public async Task<IMetadata?> GetByFileAsync(FileUpload file)
    {
        await using var connection = await _dataSource.OpenConnectionAsync();
        var type = GetMetadataType(file.Type);
        Console.WriteLine(type);
        
        return file.Type switch
        {
            FileType.Audio => await connection
                    .SqlBuilder($"SELECT * FROM AudioMetadata WHERE FileId = {file.Id} LIMIT 1")
                    .QueryFirstOrDefaultAsync<AudioMetadata>(),
            
            FileType.Archive => await connection
                .SqlBuilder($"SELECT * FROM ArchiveMetadata WHERE FileId = {file.Id} LIMIT 1")
                .QueryFirstOrDefaultAsync<ArchiveMetadata>(),
            
            FileType.Image => await connection
                .SqlBuilder($"SELECT * FROM ImageMetadata WHERE FileId = {file.Id} LIMIT 1")
                .QueryFirstOrDefaultAsync<ImageMetadata>(),
            
            FileType.Unknown => throw new Exception("Unknown file type"),
            _ => throw new Exception("Impossible")
        };
    }

    private static string GetMetadataType(FileType type)
    {
        return type switch
        {
            FileType.Audio => nameof(AudioMetadata),
            FileType.Archive => nameof(ArchiveMetadata),
            FileType.Image => nameof(ImageMetadata),
            FileType.Unknown => throw new Exception("Unknown file type"),
            _ => throw new Exception("Impossible")
        };
    }
}

public interface IMetadataService
{
    // TODO: Better name?
    Task<IMetadata?> CreateAsync(FileUpload file, string filePath);
    
    Task<IMetadata?> GetByFileAsync(FileUpload file);
}

// TODO: Might not need this
