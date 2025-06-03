namespace FileSharing.ApiService.Models;

public class Upload
{
    public Guid Id { get; } = Guid.CreateVersion7();
    public UploadStatus Status { get; set; } = UploadStatus.Started;
    public DateTime CreatedAt { get; } = DateTime.UtcNow;
    public required string IpAddress { get; init; }
}

public enum UploadStatus
{
    Started,
    Finished
}