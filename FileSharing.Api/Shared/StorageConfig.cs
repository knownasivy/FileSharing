namespace FileSharing.Api.Shared;

public static class StorageConfig
{ 
    public const long MaxFileSize = 400L * BytesSize.MiB;
    public const long MaxCachedFileSize = 25L * BytesSize.MiB;
    public const long MaxMemCacheSize = 6L * BytesSize.GiB;
    
    public const int SmallBufferSize = 16 * BytesSize.KiB;
    public const int MediumBufferSize = 32 * BytesSize.KiB;
    public const int LargeBufferSize = 64 * BytesSize.KiB;
}