namespace FileSharing.Constants;

public static class ProjectNames
{
    public const string ApiService = "apiservice";
    public const string Postgres = "postgres";
    public const string Redis = "redis";
    
    private const string ConnectionString = "filesdb";
    public static string GetConnectionString(bool isDevelopment)
    {
        return isDevelopment ? $"{ConnectionString}dev" : ConnectionString;
    }
}

public static class Limits
{
    public const long MaxCachedFileSize = 20 * 1024 * 1024; // 1 GB
    public const long MaxFileSize = 1 * 1024 * 1024 * 1024; // 1 GB
}

public static class Storage
{
    public const string Bucket = "files";
}