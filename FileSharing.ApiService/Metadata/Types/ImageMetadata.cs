using Amazon.S3;
using Amazon.S3.Model;
using FileSharing.ApiService.Files;
using FileSharing.Constants;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;

namespace FileSharing.ApiService.Metadata.Types;

public class ImageMetadata : IMetadata
{
    public Guid FileId { get; set; }
    public long Size { get; set; }
    public async Task<IMetadata?> ProcessAsync(FileUpload file, string filePath, IAmazonS3? s3)
    {
        if (s3 == null)
        {

            return null;
        }
        if (file.Type != FileType.Image) throw new Exception("Impossible");
        
        FileId = file.Id;
        
        var tempPreviewPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.webp");
        
        using var image = await Image.LoadAsync(filePath);

        await image.SaveAsWebpAsync(tempPreviewPath, new WebpEncoder { Quality = 75 });

        Size = new FileInfo(tempPreviewPath).Length;
        
        try
        {
            var upload = new PutObjectRequest
            {
                BucketName = Storage.Bucket,
                Key = file.GetPreviewFilename(),
                FilePath = tempPreviewPath,
                ContentType = "image/webp",
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