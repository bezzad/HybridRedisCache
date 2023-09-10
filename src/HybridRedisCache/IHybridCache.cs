namespace HybridRedisCache;

public interface IHybridCache
{
    bool Exists(string cacheKey);
    Task<bool> ExistsAsync(string cacheKey);
    void Set<T>(string key, T value, TimeSpan? localExpiry = null, TimeSpan? redisExpiry = null, bool fireAndForget = true);
    void Set<T>(string key, T value, HybridCacheEntry cacheEntry);
    Task SetAsync<T>(string key, T value, TimeSpan? localExpiry = null, TimeSpan? redisExpiry = null, bool fireAndForget = true);
    Task SetAsync<T>(string key, T value, HybridCacheEntry cacheEntry);
    void SetAll<T>(IDictionary<string, T> value, HybridCacheEntry cacheEntry);
    void SetAll<T>(IDictionary<string, T> value, TimeSpan? localExpiry = null, TimeSpan? redisExpiry = null, bool fireAndForget = true);
    Task SetAllAsync<T>(IDictionary<string, T> value, HybridCacheEntry cacheEntry);
    Task SetAllAsync<T>(IDictionary<string, T> value, TimeSpan? localExpiry = null, TimeSpan? redisExpiry = null, bool fireAndForget = true);
    T Get<T>(string key);
    T Get<T>(string cacheKey, Func<string, T> dataRetriever, TimeSpan? localExpiry = null, TimeSpan? redisExpiry = null, bool fireAndForget = true);
    Task<T> GetAsync<T>(string key);
    Task<T> GetAsync<T>(string cacheKey, Func<string, Task<T>> dataRetriever, TimeSpan? localExpiry = null, TimeSpan? redisExpiry = null, bool fireAndForget = true);
    bool TryGetValue<T>(string key, out T value);
    void Remove(string[] keys, bool fireAndForget = false);
    void Remove(string key, bool fireAndForget = false);
    Task RemoveAsync(string[] keys, bool fireAndForget = false);

    /// <summary>
    /// Asynchronously removes a cached value with the specified key.
    /// </summary>
    /// <param name="key">The cache key</param>
    /// <param name="fireAndForget">Whether to cache the value in Redis without waiting for the operation to complete.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task RemoveAsync(string key, bool fireAndForget = false);
    Task<string[]> RemoveWithPatternAsync(string pattern, bool fireAndForget = false, CancellationToken token = default);
    TimeSpan GetExpiration(string cacheKey);
    Task<TimeSpan> GetExpirationAsync(string cacheKey);
    IAsyncEnumerable<string> KeysAsync(string pattern, CancellationToken token = default);
    void FlushLocalCaches();
    Task FlushLocalCachesAsync();
    void ClearAll();
    Task ClearAllAsync();
}