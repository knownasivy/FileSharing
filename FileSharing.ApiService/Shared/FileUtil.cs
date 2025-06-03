using FileSharing.ApiService.Downloads;
using FileSharing.ApiService.Models;
using Microsoft.AspNetCore.StaticFiles;

namespace FileSharing.ApiService.Shared;

public static class FileUtil
{
    private static readonly HashSet<string> ArchiveExtensions = new (StringComparer.OrdinalIgnoreCase)
    {
        "zip", "rar", "7z", "dmg"
    };
    
    private static readonly HashSet<string> AudioExtensions = new (StringComparer.OrdinalIgnoreCase)
    {
        "wav", "flac",
        "mp3", "m4a",
        "opus", "ogg", "aac", "aif", "aiff"
    };
    
    private static readonly HashSet<string> ImageExtensions = new (StringComparer.OrdinalIgnoreCase)
    {
        "png", "webp", "jpeg", "jpg"
    };
    
    public static string GetExtension(string fileName)
    {
        return fileName.Split('.').Last();
    }
    
    public static FileType GetFileType(string fileName)
    {
        var ext = GetExtension(fileName);
        
        if (ArchiveExtensions.Contains(ext))
        {
            return FileType.Archive; // TODO: Maybe should change to list of file ext called other
        }
        
        if (AudioExtensions.Contains(ext))
        {
            return FileType.Audio;
        }
        
        if (ImageExtensions.Contains(ext))
        {
            return FileType.Image;
        }

        return FileType.Unsupported;
    }

    public static FileType GetFileType(this UploadFile file)
    {
        return GetFileType(file.Name);
    }
    
    public static FileType GetFileType(this IFormFile file)
    {
        return GetFileType(file.FileName);
    }
    
    public static string GetContentTypeMime(this Download download)
    {
        return GetContentTypeMime(download.FileName);
    }
    
    public static string GetContentTypeMime(string fileName)
    {
        var provider = new FileExtensionContentTypeProvider();
        if (!provider.TryGetContentType(fileName, out var contentType))
        {
            contentType = "application/octet-stream"; // Default MIME type if unknown
        }

        return contentType;
    }
}