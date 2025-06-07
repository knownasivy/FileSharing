namespace FileSharing.ApiService.Models;

public class AudioMetadata
{
    public required byte[] FileHash { get; init; }
    public string Title { get; set; } = string.Empty;
    public string Album { get; set; } = string.Empty;
    public string Artist { get; set; } = string.Empty;
    public bool AttachedPic { get; set; }
}