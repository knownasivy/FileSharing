namespace FileSharing.ApiService.Contracts.Responses;

public class FileResponse
{
    public required string Id { get; init; }
    
    public required string Name { get; set; }
    
    public required long Size { get; set; }
    
    public required string Type { get; set; }
    
    public required string Status { get; set; }
    
    public required string CreatedAt { get; set; }
    
    public required string Hash { get; set; }
}