using FileSharing.ApiService.Shared;
using ZLinq;

namespace FileSharing.ApiService.Models;

public class UploadFile
{
    public Guid Id { get; } = Guid.CreateVersion7();
    public required Guid UploadId { get; init; }
    public required string Name { get; init; }
    public required long Size { get; init; }
    public required FileType Type { get; init; }
    public FileStatus Status { get; set; } = FileStatus.Uploading;
    public DateTime CreatedAt { get; } = DateTime.UtcNow;
    public byte[] Hash { get; set; } = [];
    public bool FakeFile { get; set; }
    public required string IpAddress { get; init; }
    public string? FilePath { get; set; }
    public string Extension => FileUtil.GetFileExtension(Name);
    private string GetCreatedAtFormated() => CreatedAt.ToString("MM-dd-yy");
    private string GetLocation()
    {
        var path = Path.Combine("Uploads", GetCreatedAtFormated());
        if (!Directory.Exists(path))
            Directory.CreateDirectory(path);
        return path;
    }
    public string GetThisFilePath() =>
        Path.Combine(GetLocation(), $"{Id:N}.{Extension}");
    
    public string GetPreviewFilename() => 
        Type switch
        {
            _ when FakeFile => throw new Exception("Impossible"),
            _ when Type != FileType.Audio => throw new Exception("Impossible"),
            _ => $"{Id:N}_prev.m4a"
        };

    private const long MaxAudioExtractSize = 250L * 1024 * 1024; // 250 MB
    public bool CanExtractMetadata() =>
        Type switch
        {
            _ when FakeFile => throw new Exception("Impossible"),
            FileType.Audio when Size > MaxAudioExtractSize => false,
            _ => true
        };

    public static bool HashEquals(byte[] hash) => 
        hash.AsValueEnumerable().SequenceEqual(hash);
    public bool HashEquals(UploadFile other) => 
        HashEquals(other.Hash);
}

public enum FileType
{
    Audio,
    Archive,
    Unsupported
}

public enum FileStatus
{
    Uploading,
    Uploaded
}