using Amazon.S3;
using FileSharing.ApiService.Files;
using ICSharpCode.SharpZipLib.Zip;

namespace FileSharing.ApiService.Metadata.Types;

public record ZipItem(string Name, long size);

public class ArchiveMetadata : IMetadata
{
    public Guid FileId { get; set; }
    public List<ZipItem> Files { get; } = [];
    public bool Password { get; set; }

    public async Task<IMetadata?> ProcessAsync(FileUpload file, string filePath, IAmazonS3? _ = null)
    {
        if (file.Type != FileType.Archive) throw new Exception("Impossible");
        if (file.Extension != "zip") return null;
        
        FileId = file.Id;
        
        await using var stream = File.OpenRead(filePath);
        using var zipFile = new ZipFile(stream);
        
        foreach (ZipEntry entry in zipFile)
        {
            Files.Add(new ZipItem(entry.Name, entry.Size));
            if (entry.IsCrypted && !Password)
                Password = true;
        }
        
        return this;
    }
}