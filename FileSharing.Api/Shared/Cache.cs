using Microsoft.Extensions.Caching.Hybrid;

namespace FileSharing.Api.Shared;

public static class Cache
{
    public static async Task<T?> WithHybridCacheAsync<T>(
        this HybridCache cache,
        string key,
        Func<CancellationToken, Task<T?>> factory,
        TimeSpan expire,
        CancellationToken cancellationToken = default) where T : class
    {
        return await cache.GetOrCreateAsync<T?>(
            key,
            async ct => await factory(ct),
            new HybridCacheEntryOptions
            {
                LocalCacheExpiration = expire,
                Expiration = expire
            },
            tags: null,
            cancellationToken
        );
    }
}