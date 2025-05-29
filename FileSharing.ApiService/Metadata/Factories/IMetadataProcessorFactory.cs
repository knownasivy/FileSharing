using Amazon.S3;
using FileSharing.ApiService.Files;
using FileSharing.ApiService.Metadata.Types;

namespace FileSharing.ApiService.Metadata.Factories;

public interface IMetadataProcessorFactory
{
    Task<IMetadata?> CreateAsync(FileUpload file, string filePath, IAmazonS3? s3);
}