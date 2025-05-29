using Amazon.S3;
using FileSharing.ApiService.Files;

namespace FileSharing.ApiService.Metadata.Factories;

public interface IMetadataProcessorFactory
{
    Task<IMetadata?> CreateAsync(FileUpload file, string filePath, IAmazonS3? s3);
}