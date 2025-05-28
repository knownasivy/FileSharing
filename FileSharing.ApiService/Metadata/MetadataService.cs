using Amazon.S3;
using FileSharing.ApiService.Files;

namespace FileSharing.ApiService.Metadata;

public class MetadataService : IMetadataService
{
    private readonly IAmazonS3 _s3;

    public MetadataService(IAmazonS3 s3)
    {
        _s3 = s3;
    }

    public async Task<IMetadata?> CreateMetadataAsync(FileUpload file, string filePath)
    {
        var metadata = file.Type switch
        {
            FileType.Archive => await new ArchiveMetadata().ProcessAsync(file, filePath),
            FileType.Audio => await new AudioMetadata().ProcessAsync(file, filePath, _s3),
            FileType.Image => await new ImageMetadata().ProcessAsync(file, filePath, _s3),
            FileType.Unknown => null,
            _ => throw new Exception("Impossible")
        };
        
        return metadata;
    }
}

public interface IMetadataService
{
    // TODO: Better name?
    Task<IMetadata?> CreateMetadataAsync(FileUpload file, string filePath);
}

public interface IMetadata
{
    Task<IMetadata?> ProcessAsync(FileUpload file, string filePath, IAmazonS3? s3);
}