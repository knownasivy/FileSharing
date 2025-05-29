using System.Text.Json.Serialization;
using FileSharing.ApiService.Metadata;
using FileSharing.ApiService.Metadata.Types;

namespace FileSharing.ApiService.Contracts.Responses;

public class MetadataResponse
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public AudioMetadata? AudioMetadata { get; set; }
    
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ArchiveMetadata? ArchiveMetadata { get; set; }
    
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ImageMetadata? ImageMetadata { get; set; }
}