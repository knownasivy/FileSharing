using Amazon.S3;
using Amazon.S3.Model;
using FFMpegCore;
using FileSharing.ApiService.Files;
using FileSharing.Constants;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

namespace FileSharing.ApiService.Metadata;

public class ImageMetadata : IMetadata
{
    
    
    public async Task<IMetadata?> ProcessAsync(FileUpload file, string filePath, IAmazonS3? s3)
    {
        if (s3 == null) return null;
        
        var tempPreviewPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.webp");
        
        using var image = await Image.LoadAsync(filePath);

        await image.SaveAsWebpAsync(tempPreviewPath, new WebpEncoder { Quality = 75 });

        try
        {
            var upload = new PutObjectRequest
            {
                BucketName = Storage.Bucket,
                Key = $"{file.Id}.webp",
                FilePath = filePath,
                DisablePayloadSigning = true
            };
            
            await s3.PutObjectAsync(upload);
        }
        finally
        {
            if (File.Exists(tempPreviewPath))
                File.Delete(tempPreviewPath);
        }

        return this;
    }
}