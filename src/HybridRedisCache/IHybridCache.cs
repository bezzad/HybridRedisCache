namespace HybridRedisCache;

public interface IHybridCache
{
    /// <summary>
    /// Exists the specified Key in cache
    /// </summary>
    /// <returns>The exists</returns>
    /// <param name="key">Cache key</param>
    bool Exists(string key);

    /// <summary>
    /// Exists the specified cacheKey async.
    /// </summary>
    /// <returns>The async.</returns>
    /// <param name="key">Cache key.</param>
    Task<bool> ExistsAsync(string key);

    /// <summary>
    /// Sets a value in the cache with the specified key.
    /// </summary>
    /// <typeparam name="T">The type of the value to cache.</typeparam>
    /// <param name="key">The cache key.</param>
    /// <param name="value">The value to cache.</param>
    /// <param name="localExpiry">The expiration time for the local cache entry. If not specified, the default local expiration time is used.</param>
    /// <param name="redisExpiry">The expiration time for the redis cache entry. If not specified, the default distributed expiration time is used.</param>
    /// <param name="fireAndForget">Whether to cache the value in Redis without waiting for the operation to complete.</param>
    /// <typeparam name="T">The 1st type parameter.</typeparam>
    void Set<T>(string key, T value, TimeSpan? localExpiry = null, TimeSpan? redisExpiry = null, bool fireAndForget = true);

    /// <summary>
    /// Sets a value in the cache with the specified key.
    /// </summary>
    /// <typeparam name="T">The type of the value to cache.</typeparam>
    /// <param name="key">The cache key</param>
    /// <param name="value">The value to cache</param>
    /// <param name="cacheEntry">Parameters of caching an entry like expiration</param>
    /// <typeparam name="T">The 1st type parameter.</typeparam>
    void Set<T>(string key, T value, HybridCacheEntry cacheEntry);

    /// <summary>
    /// Asynchronously sets a value in the cache with the specified key.
    /// </summary>
    /// <typeparam name="T">The type of the value to cache.</typeparam>
    /// <param name="key">The cache key.</param>
    /// <param name="value">The value to cache.</param>
    /// <param name="localExpiry">The expiration time for the local cache entry. If not specified, the default local expiration time is used.</param>
    /// <param name="redisExpiry">The expiration time for the redis cache entry. If not specified, the default distributed expiration time is used.</param>
    /// <param name="fireAndForget">Whether to cach1e the value in Redis without waiting for the operation to complete.</param>
    /// <typeparam name="T">The 1st type parameter.</typeparam>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task SetAsync<T>(string key, T value, TimeSpan? localExpiry = null, TimeSpan? redisExpiry = null, bool fireAndForget = true);

    /// <summary>
    /// Asynchronously sets a value in the cache with the specified key.
    /// </summary>
    /// <typeparam name="T">The type of the value to cache.</typeparam>
    /// <param name="key">The cache key.</param>
    /// <param name="value">The value to cache.</param>
    /// <param name="cacheEntry">Parameters of caching an entry like expiration</param>
    /// <typeparam name="T">The 1st type parameter.</typeparam>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task SetAsync<T>(string key, T value, HybridCacheEntry cacheEntry);

    /// <summary>
    /// Sets all.
    /// </summary>
    /// <returns>The all async.</returns>
    /// <param name="value">Value.</param>
    /// <param name="localExpiry">The expiration time for the local cache entry. If not specified, the default local expiration time is used.</param>
    /// <param name="redisExpiry">The expiration time for the redis cache entry. If not specified, the default distributed expiration time is used.</param>
    /// <param name="fireAndForget">Whether to cache the value in Redis without waiting for the operation to complete.</param>
    /// <typeparam name="T">The 1st type parameter.</typeparam>
    void SetAll<T>(IDictionary<string, T> value, HybridCacheEntry cacheEntry);

    /// <summary>
    /// Sets all.
    /// </summary>
    /// <returns>The all async.</returns>
    /// <param name="value">Value.</param>
    /// <param name="cacheEntry">Parameters of caching an entry like expiration</param>
    /// <typeparam name="T">The 1st type parameter.</typeparam>
    void SetAll<T>(IDictionary<string, T> value, TimeSpan? localExpiry = null, TimeSpan? redisExpiry = null, bool fireAndForget = true);

    /// <summary>
    /// Sets all async.
    /// </summary>
    /// <returns>The all async.</returns>
    /// <param name="value">Value.</param>
    /// <param name="localExpiry">The expiration time for the local cache entry. If not specified, the default local expiration time is used.</param>
    /// <param name="redisExpiry">The expiration time for the redis cache entry. If not specified, the default distributed expiration time is used.</param>
    /// <typeparam name="T">The 1st type parameter.</typeparam>
    Task SetAllAsync<T>(IDictionary<string, T> value, TimeSpan? localExpiry = null, TimeSpan? redisExpiry = null, bool fireAndForget = true);

    /// <summary>
    /// Sets all.
    /// </summary>
    /// <returns>The all async.</returns>
    /// <param name="value">Value.</param>
    /// <param name="cacheEntry">Parameters of caching an entry like expiration</param>
    /// <typeparam name="T">The 1st type parameter.</typeparam>
    Task SetAllAsync<T>(IDictionary<string, T> value, HybridCacheEntry cacheEntry);

    /// <summary>
    /// Gets a cached value with the specified key.
    /// </summary>
    /// <typeparam name="T">The type of the cached value.</typeparam>
    /// <param name="key">The cache key.</param>
    /// <returns>The cached value, or null if the key is not found in the cache.</returns>
    T Get<T>(string key);

    /// <summary>
    /// Get the specified cacheKey, dataRetriever and expiration.
    /// </summary>
    /// <returns>The get.</returns>
    /// <param name="key">Cache key.</param>
    /// <param name="dataRetriever">Data retriever.</param>
    /// <param name="localExpiry">The expiration time for the local cache entry. If not specified, the default local expiration time is used.</param>
    /// <param name="redisExpiry">The expiration time for the redis cache entry. If not specified, the default distributed expiration time is used.</param>
    /// <param name="fireAndForget">Whether to cache the value in Redis without waiting for the operation to complete.</param>
    /// <typeparam name="T">The 1st type parameter.</typeparam>
    T Get<T>(string cacheKey, Func<string,T> dataRetriever, TimeSpan? localExpiry = null, TimeSpan? redisExpiry = null, bool fireAndForget = true);

    /// <summary>
    /// Asynchronously gets a cached value with the specified key.
    /// </summary>
    /// <typeparam name="T">The type of the cached value.</typeparam>
    /// <param name="key">The cache key.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the cached value, or null if the key is not found in the cache.</returns>
    Task<T> GetAsync<T>(string key);

    /// <summary>
    /// Asynchronously get the specified cacheKey, dataRetriever and expiration.
    /// </summary>
    /// <returns>The get.</returns>
    /// <param name="key">Cache key.</param>
    /// <param name="dataRetriever">Data retriever.</param>
    /// <param name="localExpiry">The expiration time for the local cache entry. If not specified, the default local expiration time is used.</param>
    /// <param name="redisExpiry">The expiration time for the redis cache entry. If not specified, the default distributed expiration time is used.</param>
    /// <param name="fireAndForget">Whether to cache the value in Redis without waiting for the operation to complete.</param>
    /// <typeparam name="T">The 1st type parameter.</typeparam>
    Task<T> GetAsync<T>(string cacheKey, Func<string,Task<T>> dataRetriever, TimeSpan? localExpiry = null, TimeSpan? redisExpiry = null, bool fireAndForget = true);

    /// <summary>
    /// Try gets a cached value with the specified key.
    /// </summary>
    /// <typeparam name="T">The type of the cached value.</typeparam>
    /// <param name="key">The cache key.</param>
    /// <returns>The cached value, or null if the key is not found in the cache.</returns>
    bool TryGetValue<T>(string key, out T value);

    /// <summary>
    /// Removes a cached value with the specified key.
    /// </summary>
    /// <param name="key">The cache key to remove.</param>
    /// <param name="fireAndForget">Whether to cache the value in Redis without waiting for the operation to complete.</param>
    void Remove(string[] keys, bool fireAndForget = false);

    /// <summary>
    /// Removes a cached value with the specified key.
    /// </summary>
    /// <param name="keys">Cache keys to remove.</param>
    /// <param name="fireAndForget">Whether to cache the value in Redis without waiting for the operation to complete.</param>
    void Remove(string key, bool fireAndForget = false);

    /// <summary>
    /// Asynchronously removes a cached value with the specified key.
    /// </summary>
    /// <param name="keys">cache keys</param>
    /// <param name="fireAndForget">Whether to cache the value in Redis without waiting for the operation to complete.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task RemoveAsync(string[] keys, bool fireAndForget = false);

    /// <summary>
    /// Asynchronously removes a cached value with the specified key.
    /// </summary>
    /// <param name="key">The cache key</param>
    /// <param name="fireAndForget">Whether to cache the value in Redis without waiting for the operation to complete.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task RemoveAsync(string key, bool fireAndForget = false);

    /// <summary>
    /// Asynchronously removes a cached value with a key pattern.
    /// </summary>
    /// <param name="keys">The cache key pattern. <example>"test_keys_*"</example></param>
    /// /// <param name="fireAndForget">Whether to cache the value in Redis without waiting for the operation to complete.</param>
    /// <returns>Get all removed keys</returns>
    Task<string[]> RemoveWithPatternAsync(string pattern, bool fireAndForget = false, CancellationToken token = default);

    TimeSpan? GetExpiration(string cacheKey);

    Task<TimeSpan?> GetExpirationAsync(string cacheKey);

    /// <summary>
    /// Search all servers to find all keys which match with the pattern
    /// </summary>
    /// <param name="pattern">pattern to search keys</param>
    /// <param name="token">cancellation token</param>
    /// <returns>Enumerable of Redis keys</returns>
    IAsyncEnumerable<string> KeysAsync(string pattern, CancellationToken token = default);

    void FlushLocalCaches();

    Task FlushLocalCachesAsync();

    void ClearAll();

    Task ClearAllAsync(bool fireAndForget = false);

    /// <summary>
    /// Ping all servers and clusters to health checking of Redis server
    /// </summary>
    /// <returns>Sum of pings durations</returns>
    Task<TimeSpan> PingAsync();
}