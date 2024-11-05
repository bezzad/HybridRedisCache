using StackExchange.Redis;

namespace HybridRedisCache;

public interface IHybridCache
{
    /// <summary>
    /// Exists the specified Key in cache
    /// </summary>
    /// <param name="key">Cache key</param>
    /// <param name="flags">The flags to use for this operation.</param>
    /// <returns>The bool for key is exist or not.</returns>
    bool Exists(string key, Flags flags = Flags.PreferMaster);

    /// <summary>
    /// Exists the specified cacheKey async.
    /// </summary>
    /// <param name="key">Cache key.</param>
    /// <param name="flags">The flags to use for this operation.</param>
    /// <returns>The async bool for key is exist or not.</returns>
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
    /// <param name="keepTtl"> Whether to maintain the existing key's TTL (keepTtl flag)</param>
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
    /// <param name="keepTtl"> Whether to maintain the existing key's TTL (keepTtl flag)</param>
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
    /// <param name="keepTtl"> Whether to maintain the existing key's TTL (keepTtl flag)</param>
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
    Task<bool> SetAllAsync<T>(IDictionary<string, T> value, TimeSpan? localExpiry, TimeSpan? redisExpiry,
        bool fireAndForget);

    /// <summary>
    /// Sets many key/value in the cache as Async.
    /// </summary>
    /// <param name="value">key/values to cache.</param>
    /// <param name="localExpiry">The expiration time for the local cache entry. If not specified, the default local expiration time is used.</param>
    /// <param name="redisExpiry">The expiration time for the redis cache entry. If not specified, the default distributed expiration time is used.</param>
    /// <param name="flags">The flags to use for this operation.</param>
    /// <param name="when">Which condition to set the value under (defaults to Always)</param>
    /// <param name="keepTtl"> Whether to maintain the existing key's TTL (keepTtl flag)</param>
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
    T Get<T>(string cacheKey, Func<string, T> dataRetriever, TimeSpan? localExpiry = null, TimeSpan? redisExpiry = null,
        Flags flags = Flags.PreferMaster);

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
    /// <param name="fireAndForget">The fire and forget flags to use for this operation.</param>
    /// <typeparam name="T">The 1st type parameter.</typeparam>
    [Obsolete("Please use 'Flags.FireAndForget' instead of 'fireAndForget'")]
    Task<T> GetAsync<T>(string cacheKey, Func<string, Task<T>> dataRetriever, TimeSpan? localExpiry,
        TimeSpan? redisExpiry, bool fireAndForget);


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
    Task<T> GetAsync<T>(string cacheKey, Func<string, Task<T>> dataRetriever, TimeSpan? localExpiry = null,
        TimeSpan? redisExpiry = null, Flags flags = Flags.PreferMaster);

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
    ValueTask<long> RemoveWithPatternAsync(string pattern, bool fireAndForget, CancellationToken token);

    /// <summary>
    /// Asynchronously removes a cached value with a key pattern.
    /// </summary>
    /// <param name="pattern">pattern to search keys. must have * in the key. like:  key_*_test_*</param>
    /// <param name="flags">The flags to use for this operation.</param>
    /// <param name="batchRemovePackSize">call redis remove per batch remove package size</param>
    /// <param name="token">cancellation token</param>
    /// <returns>Get all removed keys</returns>
    /// <example>
    /// Supported glob-style patterns:
    ///    h?llo matches hello, hallo and hxllo
    ///    h*llo matches hllo and heeeello
    ///    h[ae] llo matches hello and hallo, but not hillo
    ///    h[^e] llo matches hallo, hbllo, ... but not hello
    ///    h[a-b]llo matches hallo and hbllo
    /// </example>
    ValueTask<long> RemoveWithPatternAsync(string pattern, Flags flags = Flags.PreferMaster,
        int batchRemovePackSize = 1024, CancellationToken token = default);

    TimeSpan? GetExpiration(string cacheKey);

    Task<TimeSpan?> GetExpirationAsync(string cacheKey);

    /// <summary>
    /// Returns all keys matching <paramref name="pattern"/>.
    /// The <c>KEYS</c> or <c>SCAN</c> commands will be used based on the servers capabilities.
    /// Note: to resume an iteration via <i>cursor</i>, cast the original enumerable or enumerator to <see cref="IScanningCursor"/>.
    /// </summary>
    /// <param name="pattern">pattern to search keys</param>
    /// <param name="token">cancellation token</param>
    /// <param name="flags">The flags to use for this operation.</param>
    /// <returns>Enumerable of Redis keys</returns>
    IAsyncEnumerable<string> KeysAsync(string pattern, Flags flags = Flags.PreferReplica,
        CancellationToken token = default);

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

    /// <summary>
    /// Returns the IP and port number of the primary with that name.
    /// If a failover is in progress or terminated successfully for this primary it returns the address and port of the promoted replica.
    /// </summary>
    /// <param name="serviceName">The sentinel service name.</param>
    /// <param name="flags">The command flags to use.</param>
    /// <returns>The primary IP and port.</returns>
    /// <remarks><seealso href="https://redis.io/topics/sentinel"/></remarks>
    Task<string> SentinelGetMasterAddressByNameAsync(string serviceName, Flags flags = Flags.None);

    /// <summary>
    /// Returns the IP and port numbers of all known Sentinels for the given service name.
    /// </summary>
    /// <param name="serviceName">The sentinel service name.</param>
    /// <param name="flags">The command flags to use.</param>
    /// <returns>A list of the sentinel IPs and ports.</returns>
    /// <remarks><seealso href="https://redis.io/topics/sentinel"/></remarks>
    Task<string[]> SentinelGetSentinelAddressesAsync(string serviceName, Flags flags = Flags.None);

    /// <summary>
    /// Returns the IP and port numbers of all known Sentinel replicas for the given service name.
    /// </summary>
    /// <param name="serviceName">The sentinel service name.</param>
    /// <param name="flags">The command flags to use.</param>
    /// <returns>A list of the replica IPs and ports.</returns>
    /// <remarks><seealso href="https://redis.io/topics/sentinel"/></remarks>
    Task<string[]> SentinelGetReplicaAddressesAsync(string serviceName, Flags flags = Flags.None);

    /// <summary>
    /// Return the number of keys in the database.
    /// </summary>
    /// <param name="database">The database ID.</param>
    /// <param name="flags">The command flags to use.</param>
    /// <remarks><seealso href="https://redis.io/commands/dbsize"/></remarks>
    Task<long> DatabaseSizeAsync(int database = -1, Flags flags = Flags.None);

    /// <summary>
    /// Return the same message passed in.
    /// </summary>
    /// <param name="message">The message to echo.</param>
    /// <param name="flags">The command flags to use.</param>
    /// <remarks><seealso href="https://redis.io/commands/echo"/></remarks>
    Task<string[]> EchoAsync(string message, Flags flags = Flags.None);

    /// <summary>
    /// The <c>TIME</c> command returns the current server time in UTC format.
    /// Use the <see cref="DateTime.ToLocalTime"/> method to get local time.
    /// </summary>
    /// <param name="flags">The command flags to use.</param>
    /// <returns>The server's current time.</returns>
    /// <remarks><seealso href="https://redis.io/commands/time"/></remarks>
    Task<DateTime> TimeAsync(Flags flags = Flags.None);

    /// <summary>
    /// Takes a lock (specifying a token value) if it is not already taken.
    /// Note: Lock an exist key and token is not possible. But, Set a locked key with difference or same value is possible.
    /// </summary>
    /// <param name="key">The key of the lock.</param>
    /// <param name="token">The value to set at the key.</param>
    /// <param name="expiry">The expiration of the lock key.</param>
    /// <param name="flags">The flags to use for this operation.</param>
    /// <returns><see langword="true"/> if the lock was successfully taken, <see langword="false"/> otherwise.</returns>
    Task<bool> TryLockKeyAsync(string key, string token, TimeSpan? expiry, Flags flags = Flags.None);

    /// <summary>
    /// Extends a lock, if the token value is correct.
    /// </summary>
    /// <param name="key">The key of the lock.</param>
    /// <param name="token">The value to set at the key.</param>
    /// <param name="expiry">The expiration of the lock key.</param>
    /// <param name="flags">The flags to use for this operation.</param>
    /// <returns><see langword="true"/> if the lock was successfully extended.</returns>
    Task<bool> TryExtendLockAsync(string key, string token, TimeSpan? expiry, Flags flags = Flags.None);

    /// <summary>
    /// Releases a lock, if the token value is correct.
    /// </summary>
    /// <param name="key">The key of the lock.</param>
    /// <param name="token">The value at the key that must match.</param>
    /// <param name="flags">The flags to use for this operation.</param>
    /// <returns><see langword="true"/> if the lock was successfully released, <see langword="false"/> otherwise.</returns>
    Task<bool> TryReleaseLockAsync(string key, string token, Flags flags = Flags.None);

    /// <summary>
    /// Releases a lock, if the token value is correct.
    /// </summary>
    /// <param name="key">The key of the lock.</param>
    /// <param name="token">The value at the key that must match.</param>
    /// <param name="flags">The flags to use for this operation.</param>
    /// <returns><see langword="true"/> if the lock was successfully released, <see langword="false"/> otherwise.</returns>
    bool TryReleaseLock(string key, string token, Flags flags = Flags.None);

    /// <summary>
    /// Takes a lock (specifying a token value) if it is not already taken.
    /// This locking way has no expiration, and you must release that with disposing returned object.
    /// Note: Lock an exist key and token is not possible. But, Set a locked key with difference or same value is possible.
    /// </summary>
    /// <param name="key">The key of the lock.</param>
    /// <param name="flags">The flags to use for this operation.</param>
    /// <returns>Return the <see cref="RedisLockObject"/> if the lock was successfully acquired; otherwise, wait until the key is released.</returns>
    Task<RedisLockObject> LockKeyAsync(string key, Flags flags = Flags.None);
    
    /// <summary>
    /// Increments the number stored at key by increment.
    /// If the key does not exist, it is set to 0 before performing the operation.
    /// An error is returned if the key contains a value of the wrong type or contains a string that is not representable as integer.
    /// This operation is limited to 64 bit signed integers.
    /// </summary>
    /// <param name="key">The key of the string.</param>
    /// <param name="value">The amount to increment by (defaults to 1).</param>
    /// <param name="flags">The flags to use for this operation.</param>
    /// <returns>The value of key after the increment.</returns>
    /// <remarks>
    /// See
    /// <seealso href="https://redis.io/commands/incrby"/>,
    /// <seealso href="https://redis.io/commands/incr"/>.
    /// </remarks>
    Task<long> ValueIncrementAsync(string key, long value = 1, Flags flags = Flags.None);

    /// <summary>
    /// Increments the string representing a floating point number stored at key by the specified increment.
    /// If the key does not exist, it is set to 0 before performing the operation.
    /// The precision of the output is fixed at 17 digits after the decimal point regardless of the actual internal precision of the computation.
    /// </summary>
    /// <param name="key">The key of the string.</param>
    /// <param name="value">The amount to increment by (defaults to 1).</param>
    /// <param name="flags">The flags to use for this operation.</param>
    /// <returns>The value of key after the increment.</returns>
    /// <remarks><seealso href="https://redis.io/commands/incrbyfloat"/></remarks>
    Task<double> ValueIncrementAsync(string key, double value, Flags flags = Flags.None);
    
    /// <summary>
    /// Decrements the number stored at key by decrement.
    /// If the key does not exist, it is set to 0 before performing the operation.
    /// An error is returned if the key contains a value of the wrong type or contains a string that is not representable as integer.
    /// This operation is limited to 64 bit signed integers.
    /// </summary>
    /// <param name="key">The key of the string.</param>
    /// <param name="value">The amount to decrement by (defaults to 1).</param>
    /// <param name="flags">The flags to use for this operation.</param>
    /// <returns>The value of key after the decrement.</returns>
    /// <remarks>
    /// See
    /// <seealso href="https://redis.io/commands/decrby"/>,
    /// <seealso href="https://redis.io/commands/decr"/>.
    /// </remarks>
    Task<long> ValueDecrementAsync(string key, long value = 1, Flags flags = Flags.None);

    /// <summary>
    /// Decrements the string representing a floating point number stored at key by the specified decrement.
    /// If the key does not exist, it is set to 0 before performing the operation.
    /// The precision of the output is fixed at 17 digits after the decimal point regardless of the actual internal precision of the computation.
    /// </summary>
    /// <param name="key">The key of the string.</param>
    /// <param name="value">The amount to decrement by (defaults to 1).</param>
    /// <param name="flags">The flags to use for this operation.</param>
    /// <returns>The value of key after the decrement.</returns>
    /// <remarks><seealso href="https://redis.io/commands/incrbyfloat"/></remarks>
    Task<double> ValueDecrementAsync(string key, double value, Flags flags = Flags.None);
}