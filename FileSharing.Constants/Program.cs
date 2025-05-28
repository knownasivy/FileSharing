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
    public static TimeSpan MaxFileDuration = TimeSpan.FromMinutes(45);
    public const string Bucket = "files";
    public const int BufferSize = 80 * 1024; // 80 KB per second
    public const long MaxCachedFileSize = 15L * 1024 * 1024; // 20 MB
    public const long MaxMemCacheSize = 2L * 1024 * 1024 * 1024; // 2 GB
    public const long MaxFileSize = 1L * 1024 * 1024 * 1024; // 1 GB

}

public static class Misc
{
    public const string DefaultIp = "127.0.0.1";
}