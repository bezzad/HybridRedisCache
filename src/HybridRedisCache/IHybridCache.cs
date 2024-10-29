namespace HybridRedisCache;

public interface IHybridCache
{
    /// <summary>
    /// Exists the specified Key in cache
    /// </summary>
    /// <returns>The exists</returns>
    /// <param name="key">Cache key</param>
    bool Exists(string key, Flags flags = Flags.PreferMaster);

    /// <summary>
    /// Exists the specified cacheKey async.
    /// </summary>
    /// <returns>The async.</returns>
    /// <param name="key">Cache key.</param>
    Task<bool> ExistsAsync(string key, Flags flags = Flags.PreferMaster);

    /// <summary>
    /// Sets a value in the cache with the specified key.
    /// </summary>
    /// <param name="key">The cache key.</param>
    /// <param name="value">The value to cache.</param>
    /// <param name="localExpiry">The expiration time for the local cache entry. If not specified, the default local expiration time is used.</param>
    /// <param name="redisExpiry">The expiration time for the redis cache entry. If not specified, the default distributed expiration time is used.</param>
    /// <param name="fireAndForget">Whether to cache the value in Redis without waiting for the operation to complete.</param>
    [Obsolete("Please use 'Flags.FireAndForget' instead of 'fireAndForget'")]
    bool Set<T>(string key, T value, TimeSpan? localExpiry, TimeSpan? redisExpiry, bool fireAndForget);

    /// <summary>
    /// Sets a value in the cache with the specified key.
    /// </summary>
    /// <param name="key">The cache key.</param>
    /// <param name="value">The value to cache.</param>
    /// <param name="localExpiry">The expiration time for the local cache entry. If not specified, the default local expiration time is used.</param>
    /// <param name="redisExpiry">The expiration time for the redis cache entry. If not specified, the default distributed expiration time is used.</param>
    /// <param name="flags">The flags to use for this operation.</param>
    /// <param name="when">Which condition to set the value under (defaults to Always)</param>
    /// <param name="keepTtl"> Whether to maintain the existing key's TTL (KEEPTTL flag)</param>
    /// <param name="localCacheEnable">Cache value in the local memory or not, default value is True.</param>
    /// <param name="redisCacheEnable">Cache value in the Redis cache or not, default value is True.</param>
    bool Set<T>(string key, T value, TimeSpan? localExpiry = null, TimeSpan? redisExpiry = null,
        Flags flags = Flags.PreferMaster, Condition when = Condition.Always,
        bool keepTtl = false, bool localCacheEnable = true, bool redisCacheEnable = true);

    /// <summary>
    /// Sets a value in the cache with the specified key.
    /// </summary>
    /// <param name="key">The cache key</param>
    /// <param name="value">The value to cache</param>
    /// <param name="cacheEntry">Parameters of caching an entry like expiration</param>
    /// <typeparam name="T">The 1st type parameter.</typeparam>
    bool Set<T>(string key, T value, HybridCacheEntry cacheEntry);

    /// <summary>
    /// Asynchronously sets a value in the cache with the specified key.
    /// </summary>
    /// <param name="key">The cache key.</param>
    /// <param name="value">The value to cache.</param>
    /// <param name="localExpiry">The expiration time for the local cache entry. If not specified, the default local expiration time is used.</param>
    /// <param name="redisExpiry">The expiration time for the redis cache entry. If not specified, the default distributed expiration time is used.</param>
    /// <param name="fireAndForget">Whether to cache the value in Redis without waiting for the operation to complete.</param>
    /// <typeparam name="T">The 1st type parameter.</typeparam>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Obsolete("Please use 'Flags.FireAndForget' instead of 'fireAndForget'")]
    Task<bool> SetAsync<T>(string key, T value, TimeSpan? localExpiry, TimeSpan? redisExpiry, bool fireAndForget);

    /// <summary>
    /// Asynchronously sets a value in the cache with the specified key.
    /// </summary>
    /// <param name="key">The cache key.</param>
    /// <param name="value">The value to cache.</param>
    /// <param name="localExpiry">The expiration time for the local cache entry. If not specified, the default local expiration time is used.</param>
    /// <param name="redisExpiry">The expiration time for the redis cache entry. If not specified, the default distributed expiration time is used.</param>
    /// <param name="flags">The flags to use for this operation.</param>
    /// <param name="when">Which condition to set the value under (defaults to Always)</param>
    /// <param name="keepTtl"> Whether to maintain the existing key's TTL (KEEPTTL flag)</param>
    /// <param name="localCacheEnable">Cache value in the local memory or not, default value is True.</param>
    /// <param name="redisCacheEnable">Cache value in the Redis cache or not, default value is True.</param>
    Task<bool> SetAsync<T>(string key, T value, TimeSpan? localExpiry = null, TimeSpan? redisExpiry = null,
        Flags flags = Flags.PreferMaster, Condition when = Condition.Always,
        bool keepTtl = false, bool localCacheEnable = true, bool redisCacheEnable = true);

    /// <summary>
    /// Asynchronously sets a value in the cache with the specified key.
    /// </summary>
    /// <param name="key">The cache key.</param>
    /// <param name="value">The value to cache.</param>
    /// <param name="cacheEntry">Parameters of caching an entry like expiration</param>
    /// <typeparam name="T">The 1st type parameter.</typeparam>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task<bool> SetAsync<T>(string key, T value, HybridCacheEntry cacheEntry);

    /// <summary>
    /// Sets all.
    /// </summary>
    /// <returns>The all async.</returns>
    /// <param name="value">Value of caching</param>
    /// <param name="cacheEntry">cache options entry</param>
    /// <typeparam name="T">The 1st type parameter.</typeparam>
    bool SetAll<T>(IDictionary<string, T> value, HybridCacheEntry cacheEntry);

    /// <summary>
    /// Sets all.
    /// </summary>
    /// <returns>The all async.</returns>
    /// <param name="value">Value.</param>
    /// <param name="localExpiry">The expiration time for the local cache entry. If not specified, the default local expiration time is used.</param>
    /// <param name="redisExpiry">The expiration time for the redis cache entry. If not specified, the default distributed expiration time is used.</param>
    /// <param name="fireAndForget">Whether to cache the value in Redis without waiting for the operation to complete.</param>
    /// <typeparam name="T">The 1st type parameter.</typeparam>
    [Obsolete("Please use 'Flags.FireAndForget' instead of 'fireAndForget'")]
    bool SetAll<T>(IDictionary<string, T> value, TimeSpan? localExpiry, TimeSpan? redisExpiry, bool fireAndForget);

    /// <summary>
    /// Sets many key/value in the cache.
    /// </summary>
    /// <param name="value">key/values to cache.</param>
    /// <param name="localExpiry">The expiration time for the local cache entry. If not specified, the default local expiration time is used.</param>
    /// <param name="redisExpiry">The expiration time for the redis cache entry. If not specified, the default distributed expiration time is used.</param>
    /// <param name="flags">The flags to use for this operation.</param>
    /// <param name="when">Which condition to set the value under (defaults to Always)</param>
    /// <param name="keepTtl"> Whether to maintain the existing key's TTL (KEEPTTL flag)</param>
    /// <param name="localCacheEnable">Cache value in the local memory or not, default value is True.</param>
    /// <param name="redisCacheEnable">Cache value in the Redis cache or not, default value is True.</param>
    bool SetAll<T>(IDictionary<string, T> value, TimeSpan? localExpiry = null, TimeSpan? redisExpiry = null,
        Flags flags = Flags.PreferMaster, Condition when = Condition.Always,
        bool keepTtl = false, bool localCacheEnable = true, bool redisCacheEnable = true);

    /// <summary>
    /// Sets all async.
    /// </summary>
    /// <returns>The all async.</returns>
    /// <param name="value">Value.</param>
    /// <param name="localExpiry">The expiration time for the local cache entry. If not specified, the default local expiration time is used.</param>
    /// <param name="redisExpiry">The expiration time for the redis cache entry. If not specified, the default distributed expiration time is used.</param>
    /// <param name="fireAndForget">Whether to cache the value in Redis without waiting for the operation to complete.</param>
    /// <typeparam name="T">The 1st type parameter.</typeparam>
    [Obsolete("Please use 'Flags.FireAndForget' instead of 'fireAndForget'")]
    Task<bool> SetAllAsync<T>(IDictionary<string, T> value, TimeSpan? localExpiry, TimeSpan? redisExpiry, bool fireAndForget);

    /// <summary>
    /// Sets many key/value in the cache as Async.
    /// </summary>
    /// <param name="value">key/values to cache.</param>
    /// <param name="localExpiry">The expiration time for the local cache entry. If not specified, the default local expiration time is used.</param>
    /// <param name="redisExpiry">The expiration time for the redis cache entry. If not specified, the default distributed expiration time is used.</param>
    /// <param name="flags">The flags to use for this operation.</param>
    /// <param name="when">Which condition to set the value under (defaults to Always)</param>
    /// <param name="keepTtl"> Whether to maintain the existing key's TTL (KEEPTTL flag)</param>
    /// <param name="localCacheEnable">Cache value in the local memory or not, default value is True.</param>
    /// <param name="redisCacheEnable">Cache value in the Redis cache or not, default value is True.</param>
    Task<bool> SetAllAsync<T>(IDictionary<string, T> value, TimeSpan? localExpiry = null, TimeSpan? redisExpiry = null,
        Flags flags = Flags.PreferMaster, Condition when = Condition.Always,
        bool keepTtl = false, bool localCacheEnable = true, bool redisCacheEnable = true);

    /// <summary>
    /// Sets all.
    /// </summary>
    /// <returns>The all async.</returns>
    /// <param name="value">Value.</param>
    /// <param name="cacheEntry">Parameters of caching an entry like expiration</param>
    /// <typeparam name="T">The 1st type parameter.</typeparam>
    Task<bool> SetAllAsync<T>(IDictionary<string, T> value, HybridCacheEntry cacheEntry);

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
    /// <param name="cacheKey">Cache key.</param>
    /// <param name="dataRetriever">Data retriever.</param>
    /// <param name="localExpiry">The expiration time for the local cache entry. If not specified, the default local expiration time is used.</param>
    /// <param name="redisExpiry">The expiration time for the redis cache entry. If not specified, the default distributed expiration time is used.</param>
    /// <param name="flags">The flags to use for this operation.</param>
    /// <typeparam name="T">The 1st type parameter.</typeparam>
    T Get<T>(string cacheKey, Func<string, T> dataRetriever, TimeSpan? localExpiry = null, TimeSpan? redisExpiry = null, Flags flags = Flags.PreferMaster);

    /// <summary>
    /// Get the specified cacheKey, dataRetriever and expiration.
    /// </summary>
    /// <returns>The get.</returns>
    /// <param name="cacheKey">Cache key.</param>
    /// <param name="dataRetriever">Data retriever.</param>
    /// <param name="cacheEntry">Parameters of caching an entry like expiration</param>
    /// <typeparam name="T">The 1st type parameter.</typeparam>
    T Get<T>(string cacheKey, Func<string, T> dataRetriever, HybridCacheEntry cacheEntry);

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
    /// <param name="cacheKey">Cache key.</param>
    /// <param name="dataRetriever">Data retriever.</param>
    /// <param name="localExpiry">The expiration time for the local cache entry. If not specified, the default local expiration time is used.</param>
    /// <param name="redisExpiry">The expiration time for the redis cache entry. If not specified, the default distributed expiration time is used.</param>
    /// <param name="flags">The flags to use for this operation.</param>
    /// <typeparam name="T">The 1st type parameter.</typeparam>
    [Obsolete("Please use 'Flags.FireAndForget' instead of 'fireAndForget'")]
    Task<T> GetAsync<T>(string cacheKey, Func<string, Task<T>> dataRetriever, TimeSpan? localExpiry, TimeSpan? redisExpiry, bool fireAndForget);


    /// <summary>
    /// Asynchronously get the specified cacheKey, dataRetriever and expiration.
    /// </summary>
    /// <returns>The get.</returns>
    /// <param name="cacheKey">Cache key.</param>
    /// <param name="dataRetriever">Data retriever.</param>
    /// <param name="localExpiry">The expiration time for the local cache entry. If not specified, the default local expiration time is used.</param>
    /// <param name="redisExpiry">The expiration time for the redis cache entry. If not specified, the default distributed expiration time is used.</param>
    /// <param name="flags">The flags to use for this operation.</param>
    /// <typeparam name="T">The 1st type parameter.</typeparam>
    Task<T> GetAsync<T>(string cacheKey, Func<string, Task<T>> dataRetriever, TimeSpan? localExpiry = null, TimeSpan? redisExpiry = null, Flags flags = Flags.PreferMaster);

    /// <summary>
    /// Asynchronously get the specified cacheKey, dataRetriever and expiration.
    /// </summary>
    /// <returns>The get.</returns>
    /// <param name="cacheKey">Cache key.</param>
    /// <param name="dataRetriever">Data retriever.</param>
    /// <param name="cacheEntry">Parameters of caching an entry like expiration</param>
    /// <typeparam name="T">The 1st type parameter.</typeparam>
    Task<T> GetAsync<T>(string cacheKey, Func<string, Task<T>> dataRetriever, HybridCacheEntry cacheEntry);

    /// <summary>
    /// Try gets a cached value with the specified key.
    /// </summary>
    /// <typeparam name="T">The type of the cached value.</typeparam>
    /// <param name="key">The cache key.</param>
    /// <param name="value">The value of caching</param>
    /// <returns>The cached value, or null if the key is not found in the cache.</returns>
    bool TryGetValue<T>(string key, out T value);

    /// <summary>
    /// Removes a cached value with the specified key.
    /// </summary>
    /// <param name="keys">Cache keys to remove.</param>
    /// <param name="fireAndForget">Whether to cache the value in Redis without waiting for the operation to complete.</param>
    [Obsolete("Please use 'Flags.FireAndForget' instead of 'fireAndForget'")]
    bool Remove(string[] keys, bool fireAndForget);

    /// <summary>
    /// Removes a cached value with the specified key.
    /// </summary>
    /// <param name="keys">Cache keys to remove.</param>
    /// <param name="flags">The flags to use for this operation.</param>
    bool Remove(string[] keys, Flags flags = Flags.PreferMaster);

    /// <summary>
    /// Removes a cached value with the specified key.
    /// </summary>
    /// <param name="key">Cache key to remove.</param>
    /// <param name="fireAndForget">Whether to cache the value in Redis without waiting for the operation to complete.</param>
    [Obsolete("Please use 'Flags.FireAndForget' instead of 'fireAndForget'")]
    bool Remove(string key, bool fireAndForget);

    /// <summary>
    /// Removes a cached value with the specified key.
    /// </summary>
    /// <param name="key">Cache key to remove.</param>
    /// <param name="flags">The flags to use for this operation.</param>
    bool Remove(string key, Flags flags = Flags.PreferMaster);

    /// <summary>
    /// Asynchronously removes a cached value with the specified key.
    /// </summary>
    /// <param name="keys">cache keys</param>
    /// <param name="fireAndForget">Whether to cache the value in Redis without waiting for the operation to complete.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Obsolete("Please use 'Flags.FireAndForget' instead of 'fireAndForget")]
    Task<bool> RemoveAsync(string[] keys, bool fireAndForget);

    /// <summary>
    /// Asynchronously removes a cached value with the specified key.
    /// </summary>
    /// <param name="keys">cache keys</param>
    /// <param name="flags">The flags to use for this operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task<bool> RemoveAsync(string[] keys, Flags flags = Flags.PreferMaster);

    /// <summary>
    /// Asynchronously removes a cached value with the specified key.
    /// </summary>
    /// <param name="key">The cache key</param>
    /// <param name="fireAndForget">Whether to cache the value in Redis without waiting for the operation to complete.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Obsolete("Please use 'Flags.FireAndForget' instead of 'fireAndForget")]
    Task<bool> RemoveAsync(string key, bool fireAndForget);

    /// <summary>
    /// Asynchronously removes a cached value with the specified key.
    /// </summary>
    /// <param name="key">The cache key</param>
    /// <param name="flags">The flags to use for this operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task<bool> RemoveAsync(string key, Flags flags = Flags.PreferMaster);

    /// <summary>
    /// Asynchronously removes a cached value with a key pattern.
    /// </summary>
    /// <param name="pattern">pattern to search keys. must have * in the key. like:  key_*_test_*</param>
    /// <param name="token">cancellation token</param>
    /// <param name="fireAndForget">Whether to cache the value in Redis without waiting for the operation to complete.</param>
    /// <returns>Get all removed keys</returns>
    /// <example>
    /// Supported glob-style patterns:
    ///    h?llo matches hello, hallo and hxllo
    ///    h*llo matches hllo and heeeello
    ///    h[ae] llo matches hello and hallo, but not hillo
    ///    h[^e] llo matches hallo, hbllo, ... but not hello
    ///    h[a-b]llo matches hallo and hbllo
    /// </example>
    [Obsolete("Please use 'Flags.FireAndForget' instead of 'fireAndForget")]
    Task<string[]> RemoveWithPatternAsync(string pattern, bool fireAndForget, CancellationToken token);

    /// <summary>
    /// Asynchronously removes a cached value with a key pattern.
    /// </summary>
    /// <param name="pattern">pattern to search keys. must have * in the key. like:  key_*_test_*</param>
    /// <param name="token">cancellation token</param>
    /// <param name="flags">The flags to use for this operation.</param>
    /// <returns>Get all removed keys</returns>
    /// <example>
    /// Supported glob-style patterns:
    ///    h?llo matches hello, hallo and hxllo
    ///    h*llo matches hllo and heeeello
    ///    h[ae] llo matches hello and hallo, but not hillo
    ///    h[^e] llo matches hallo, hbllo, ... but not hello
    ///    h[a-b]llo matches hallo and hbllo
    /// </example>
    Task<string[]> RemoveWithPatternAsync(string pattern, Flags flags = Flags.PreferMaster, CancellationToken token = default);

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

    void ClearAll(Flags flags = Flags.PreferMaster);

    [Obsolete("Please use 'Flags.FireAndForget' instead of 'fireAndForget")]
    Task ClearAllAsync(bool fireAndForget);
    Task ClearAllAsync(Flags flags = Flags.PreferMaster);

    /// <summary>
    /// Ping all servers and clusters to health checking of Redis server
    /// </summary>
    /// <returns>Sum of pings durations</returns>
    Task<TimeSpan> PingAsync();
}