using FileSharing.ApiService.Files;
using Microsoft.AspNetCore.StaticFiles;

namespace FileSharing.ApiService.Util;

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

        return FileType.Unknown;
    }

    public static FileType GetFileType(this FileUpload fileUpload)
    {
        return GetFileType(fileUpload.Name);        
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