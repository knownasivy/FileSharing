using FileSharing.ApiService.Contracts.Requests;
using FileSharing.ApiService.Contracts.Responses;
using FileSharing.ApiService.Files;

namespace FileSharing.ApiService.Contracts;

public static class ContractMapping
{
    

    /*
    private static readonly Dictionary<string, FileUpload.FileType> ExtensionToTypeMap = new(StringComparer.OrdinalIgnoreCase)
    {
        { "wav", FileUpload.FileType.Audio }, { "flac", FileUpload.FileType.Audio }, // etc.
        { "zip", FileUpload.FileType.Other }, { "rar", FileUpload.FileType.Other }
    };
    */
    
    private static readonly HashSet<string> ArchiveExtensions = new (StringComparer.OrdinalIgnoreCase)
    {
        // Archives
        "zip", "rar"
    };
    
    private static readonly HashSet<string> AudioExtensions = new (StringComparer.OrdinalIgnoreCase)
    {
        // Audio
        "wav", "flac",
        "mp3", "m4a",
        "opus", "ogg", "aac", "aif", "aiff"
    };
    
    public static UploadFile MapToFile(this CreateFileRequest request)
    {
        var fileName = request.File.FileName;
        var fileExt = fileName.Split('.').Last();
        
        UploadFile.FileType fileType;
        if (ArchiveExtensions.Contains(fileExt))
        {
            fileType = UploadFile.FileType.Other; // TODO: Maybe should change to list of file ext called other
        }
        else if (AudioExtensions.Contains(fileExt))
        {
            fileType = UploadFile.FileType.Audio;
        }
        else
        {
            fileType = UploadFile.FileType.Unknown;
        }
        
        // TODO: Check file type matches actual filetype
        return new UploadFile
        {
            Id = Guid.NewGuid(),
            Name = fileName,
            FileExtension = fileExt,
            Size = request.File.Length,
            Type = fileType
        };
    }

    public static FileResponse MapToResponse(this UploadFile file)
    {
        return new FileResponse
        {
            Id = file.Id.ToString("N"),
            Name = file.Name,
            Size = file.Size,
            Type = file.Type.ToString(),
            Status = file.Status.ToString(),
            CreatedAt = file.GetCreated(),
            Hash = file.Hash
        };
    }
}