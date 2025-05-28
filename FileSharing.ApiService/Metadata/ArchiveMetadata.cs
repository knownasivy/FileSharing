
using Amazon.S3;
using FileSharing.ApiService.Files;
using ICSharpCode.SharpZipLib.Zip;

namespace FileSharing.ApiService.Metadata;

public class ArchiveMetadata : IMetadata
{
    public bool Password { get; set; }
    public List<KeyValuePair<string, long>> files = [];

    public async Task<IMetadata?> ProcessAsync(FileUpload file, string filePath, IAmazonS3? _ = null)
    {
        await using var stream = File.OpenRead(filePath);
        using var zipFile = new ZipFile(stream);
        
        foreach (ZipEntry entry in zipFile)
        {
            files.Add(new KeyValuePair<string, long>(entry.Name, entry.Size));
            if (entry.IsCrypted && !Password)
                Password = true;
        }
        
        return this;
    }
}