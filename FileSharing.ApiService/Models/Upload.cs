namespace FileSharing.ApiService.Models;

public class Upload
{
    public Guid Id { get; } = Guid.CreateVersion7();
    public DateTime CreatedAt { get; } = DateTime.UtcNow;
    public int FilesCount { get; init; }
    public required string IpAddress { get; init; }
    public bool UploadFilesExpired => DateTime.UtcNow >= CreatedAt.AddMinutes(15);
    public bool CanUploadFile(string ipAddress)
    {
        return IpAddress == ipAddress && !UploadFilesExpired;
    }

    // TODO:
    // Files amount
}