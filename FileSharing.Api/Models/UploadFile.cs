using FileSharing.Api.Shared;
using ZLinq;

namespace FileSharing.Api.Models;

public class UploadFile
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public required string Name { get; init; }
    public long Size { get; set; }
    public required FileType Type { get; init; }
    public FileStatus Status { get; set; } = FileStatus.Uploading;
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public byte[] Hash { get; set; } = [];
    public bool FakeFile { get; set; }
    public required string IpAddress { get; init; }
    public string? FilePath { get; set; }
    public string Extension => FileUtil.GetFileExtension(Name);
    private string GetCreatedAtFormated() => CreatedAt.ToString("MM-dd-yy");
    public string GetLocation()
    {
        var path = Path.Combine("Uploads", GetCreatedAtFormated());
        if (!Directory.Exists(path))
            Directory.CreateDirectory(path);
        return path;
    }
    public string GetThisFilePath() =>
        Path.Combine(GetLocation(), $"{Id:N}.{Extension}");

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