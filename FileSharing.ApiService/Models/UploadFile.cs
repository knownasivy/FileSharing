using FileSharing.ApiService.Shared;
using ZLinq;

namespace FileSharing.ApiService.Models;

public class UploadFile
{
    public Guid Id { get; } = Guid.CreateVersion7();
    public required Guid UploadId { get; init; }
    public required string Name { get; set; }
    public required long Size { get; init; }
    public required FileType Type { get; init; }
    public FileStatus Status { get; set; } = FileStatus.Uploading;
    public DateTime CreatedAt { get; } = DateTime.UtcNow;
    public byte[] Hash { get; set; } = [];
    public bool FakeFile { get; set; }
    public required string IpAddress { get; init; }
    public string Extension => FileUtil.GetExtension(Name);
    
    public string CreatedAtFormated() => 
        CreatedAt.ToString("MM-dd-yy");
    
    public string GetLocation()
    {
        // If I keep using this check move to func
        if (FakeFile) throw new Exception("A FakeFile does not have a preview");
        
        return Path.Combine("Uploads", CreatedAtFormated());
    }
    
    // TODO: Might be able to use more than one place
    public string GetPreviewFilename()
    {
        if (FakeFile) throw new Exception("A FakeFile does not have a preview");
        
        return Type switch
        {
            FileType.Audio => $"{Id:N}_prev.m4a",
            FileType.Image => $"{Id:N}_img.webp",
            _ => throw new Exception("Impossible")
        };
    }
    
    public bool CanExtractMetadata()
    {
        if (FakeFile) throw new Exception("A FakeFile does not have metadata");

        const long maxAudioExtractSize = 250L * 1024 * 1024; // 250 MB
        const long maxImageExtractSize = 75L * 1024 * 1024; // 75 MB
        
        return Type switch
        {
            FileType.Audio when Size > maxAudioExtractSize => false,
            FileType.Image when Size > maxImageExtractSize => false,
            _ => true
        };
    }

    public static bool HashEquals(byte[] hash) => 
        hash.AsValueEnumerable().SequenceEqual(hash);
    
    public bool HashEquals(UploadFile other) => 
        HashEquals(other.Hash);
}

public enum FileType
{
    Audio,
    Archive,
    Image,
    Unsupported
}

public enum FileStatus
{
    Uploading,
    Uploaded
    //Deleted
}

