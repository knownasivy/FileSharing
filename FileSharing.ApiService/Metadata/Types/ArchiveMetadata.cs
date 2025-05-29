using System.Text.Json;
using Amazon.S3;
using FileSharing.ApiService.Files;
using ICSharpCode.SharpZipLib.Zip;

namespace FileSharing.ApiService.Metadata.Types;

public class ArchiveMetadata : IMetadata
{
    public Guid FileId { get; set; }
    public List<string> Files { get; } = [];
    public bool Password { get; set; }
    /*
    public string FilesJson
    {
        get => JsonSerializer.Serialize(Files);
        set
        {
            var list = JsonSerializer.Deserialize<List<string>>(value);
            Files.Clear();
            if (list is not null) Files.AddRange(list);
        }
    }
    */

    public async Task<IMetadata?> ProcessAsync(FileUpload file, string filePath, IAmazonS3? _ = null)
    {
        if (file.Type != FileType.Archive) throw new Exception("Impossible");
        if (file.Extension != "zip") return null;
        
        FileId = file.Id;
        
        await using var stream = File.OpenRead(filePath);
        using var zipFile = new ZipFile(stream);
        
        foreach (ZipEntry entry in zipFile)
        {
            Files.Add(entry.Name);
            if (entry.IsCrypted && !Password)
                Password = true;
        }
        
        return this;
    }
}