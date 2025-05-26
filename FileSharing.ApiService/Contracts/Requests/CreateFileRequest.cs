namespace FileSharing.ApiService.Contracts.Requests;

public class CreateFileRequest
{
    public required IFormFile File { get; init; }
}