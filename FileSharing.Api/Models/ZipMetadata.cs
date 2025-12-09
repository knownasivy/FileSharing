namespace FileSharing.Api.Models;

public record ZipItem(string Name, long Size);

public class ZipMetadata
{
    public required byte[] FileHash { get; init; }
    public List<ZipItem> Files { get; set; } = [];
    public bool Password { get; set; }
    public bool Truncated => Files.Count >= 250;
}