namespace FileSharing.ApiService.Models;

public record ZipItem(string Name, long Size);

public class ZipMetadata
{
    public required byte[] FileHash { get; init; }
    public List<ZipItem> Files { get; set; } = [];
    public bool Password { get; set; }
}