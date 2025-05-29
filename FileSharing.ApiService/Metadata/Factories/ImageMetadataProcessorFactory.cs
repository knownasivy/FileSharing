using Amazon.S3;
using FileSharing.ApiService.Files;
using FileSharing.ApiService.Metadata.Types;

namespace FileSharing.ApiService.Metadata.Factories;

public class ImageMetadataProcessorFactory : IMetadataProcessorFactory
{
    public async Task<IMetadata?> CreateAsync(FileUpload file, string filePath, IAmazonS3? s3)
    {
        return await new ImageMetadata().ProcessAsync(file, filePath, s3);
    }
}