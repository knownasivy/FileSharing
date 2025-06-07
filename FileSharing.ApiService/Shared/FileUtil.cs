using FileSharing.ApiService.Models;
using FileSharing.Constants;
using Microsoft.AspNetCore.StaticFiles;
using ZLinq;

namespace FileSharing.ApiService.Shared;

public static class FileUtil
{
    private static readonly HashSet<string> ArchiveExtensions = new (StringComparer.Ordinal)
    {
        "zip"
    };
    
    private static readonly HashSet<string> AudioExtensions = new (StringComparer.Ordinal)
    {
        "wav", "flac",
        "mp3", "m4a",
        "opus", "ogg", "aac", "aif", "aiff"
    };
    
    public static string GetFileExtension(string fileName) 
        => fileName.Split('.').AsValueEnumerable().Last().ToLower();
    
    public static FileType GetFileType(string fileName)
    {
        if (!fileName.Contains('.')) return FileType.Unsupported;
        
        var ext = GetFileExtension(fileName);
        
        if (ArchiveExtensions.Contains(ext))
        {
            return FileType.Archive; // TODO: Maybe should change to list of file ext called other
        }
        
        return AudioExtensions.Contains(ext) ? 
            FileType.Audio : FileType.Unsupported;
    }
    
    public static FileType GetFileType(this IFormFile file)
    {
        return GetFileType(file.FileName);
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

    private const long MaxSmallFileSize = 1L * 1024 * 1024;
    private const long MaxMediumFileSize = 15L * 1024 * 1024;
    
    public static int GetBufferSize(long size)
    {
        if (size is <= 0 or > Storage.MaxFileSize)
            throw new Exception("Impossible");

        if (size <= MaxSmallFileSize)
            return Storage.SmallBufferSize;

        if (size <= MaxMediumFileSize)
            return Storage.MediumBufferSize;
        
        return Storage.LargeBufferSize;
    }
}