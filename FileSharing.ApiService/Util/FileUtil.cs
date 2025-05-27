using FileSharing.ApiService.Files;

namespace FileSharing.ApiService.Util;

public static class FileUtil
{
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

        return FileType.Unknown;
    }

    public static FileType GetFileType(this FileUpload fileUpload)
    {
        return GetFileType(fileUpload.Name);        
    }
}