using Amazon.S3;
using FileSharing.ApiService.Files;

namespace FileSharing.ApiService.Metadata.Types;

public interface IMetadata
{
    Guid FileId { get; set; }
    Task<IMetadata?> ProcessAsync(FileUpload file, string filePath, IAmazonS3? s3);
}