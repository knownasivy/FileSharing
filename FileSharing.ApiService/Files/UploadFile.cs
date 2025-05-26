using System.Diagnostics;

namespace FileSharing.ApiService.Files;

public class UploadFile
{
    public required Guid Id { get; init; }
    
    public required string Name { get; set; }

    // TODO: Remove... File ext is saved in Name
    public required string FileExtension { get; init; }
    
    // In bytes
    public required long Size { get; init; }
    
    // Other, Audio
    public required FileType Type { get; init; }
    
    // Uploading, Uploaded, Deleted
    public FileStatus Status { get; init; } = FileStatus.Uploading;
    
    // Should 
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    
    public string Hash { get; init; } = string.Empty;

    public string GetLocation()
    {
        // If i keep using this check move to func
        if (Type == FileType.Hash)
            throw new Exception("Impossible");
        return Path.Combine("Uploads", GetCreated());
    }

    public string GetCreated()
    {
        return CreatedAt.ToString("MM-dd-yy");
    }
    
    // TODO: Might be able to use more than one place
    public string GetPreviewFilename()
    {
        if (Type == FileType.Hash)
            throw new Exception("Impossible");
        return $"{Id:N}_prev.m4a";
    }
    
    // TODO: Move Hash file type to bool?
    public enum FileType
    {
        Audio,
        Other,
        Hash,
        Unknown
    }

    public enum FileStatus
    {
        Uploading,
        Uploaded,
        Deleted
    }
}