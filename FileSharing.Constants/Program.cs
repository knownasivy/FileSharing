namespace FileSharing.Constants;

// TODO: Move some of these to configs?
public static class ProjectNames
{
    public const string ApiService = "apiservice";
    public const string Postgres = "postgres";
    //public const string Redis = "redis";
    
    private const string ConnectionString = "filesdb";
    public static string GetConnectionString(bool isDevelopment)
    {
        return isDevelopment ? $"{ConnectionString}dev" : ConnectionString;
    }
}

public static class Storage
{
    public static readonly TimeSpan MaxAudioFileProcessDuration = TimeSpan.FromMinutes(45);
    
    public const string Bucket = "files";
    public const int MaxFilesPerUpload = 35;
    public const long MaxCachedFileSize = 10L * 1024 * 1024; // 10 MB
    public const long MaxMemCacheSize = 4L * 1024 * 1024 * 1024; // 3 GB
    public const long MaxFileSize = 400 * 1024 * 1024; // 400 MB
    
    public const int SmallBufferSize = 4 * 1024;
    public const int MediumBufferSize = 8 * 1024;
    public const int LargeBufferSize = 64 * 1024;
}

public static class Misc
{
    public const string DefaultIp = "127.0.0.1";
    public const int MetadataTaskCapacity = 30;
}