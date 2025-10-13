namespace HybridRedisCache;

public interface IHybridCache
{
    /// <summary>
    /// Subscribe to perform some operation when a message to the preferred/active node is broadcast,
    /// without any guarantee of ordered handling.
    /// Channel: Redis KeySpace
    /// </summary>
    event RedisBusMessage OnRedisBusMessage;

    IDatabase RedisDb { get; }

    /// <summary>
    /// Subscribe to a channel in Redis to receive messages published to that channel.
    /// </summary>
    /// <param name="channel"></param>
    /// <param name="handler"></param>
    void Subscribe(string channel, RedisChannelMessage handler);

    /// <summary>
    /// Unsubscribe from a specified message channel.
    /// The subscription is canceled regardless of the subscribers.
    /// </summary>
    /// <param name="channel">The channel that was subscribed to.</param>
    /// <remarks>
    /// See
    /// <seealso href="https://redis.io/commands/unsubscribe" />,
    /// <seealso href="https://redis.io/commands/punsubscribe" />.
    /// </remarks>
    void Unsubscribe(string channel);

    /// <inheritdoc cref="IHybridCache.Publish(string, string, string, Flags)"/>
    Task<long> PublishAsync(string channel, string key, string value, Flags flags = Flags.FireAndForget);

    /// <summary>Posts a message to the given channel.</summary>
    /// <param name="channel">The channel to publish to.</param>
    /// <param name="key">The channel postfix name with `:` to publish as specific key</param>
    /// <param name="value">The message to publish.</param>
    /// <param name="flags">The command flags to use.</param>
    /// <returns>
    /// The number of clients that received the message *on the destination server*,
    /// note that this doesn't mean much in a cluster as clients can get the message through other nodes.
    /// </returns>
    /// <remarks><seealso href="https://redis.io/commands/publish" /></remarks>
    long Publish(string channel, string key, string value, Flags flags = Flags.FireAndForget);

    /// <summary>
    /// Exists the specified Key in cache
    /// </summary>
    /// <param name="key">Cache key</param>
    /// <param name="flags">The flags to use for this operation.</param>
    /// <returns>The bool for key is exist or not.</returns>
    bool Exists(string key, Flags flags = Flags.None);

    /// <summary>
    /// Exists the specified cacheKey async.
    /// </summary>
    /// <param name="key">Cache key.</param>
    /// <param name="flags">The flags to use for this operation.</param>
    /// <returns>The async bool for key is exist or not.</returns>
    Task<bool> ExistsAsync(string key, Flags flags = Flags.None);

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
    /// <param name="localCacheEnable">Set local memory or not?</param>
    /// <returns>The cached value, or null if the key is not found in the cache.</returns>
    T Get<T>(string key, bool localCacheEnable = true);

    /// <summary>
    /// Get the specified cacheKey, dataRetriever, and expiration.
    /// </summary>
    /// <returns>The get.</returns>
    /// <param name="cacheKey">Cache key.</param>
    /// <param name="dataRetriever">Data retriever.</param>
    /// <param name="localExpiry">The expiration time for the local cache entry. If not specified, the default local expiration time is used.</param>
    /// <param name="redisExpiry">The expiration time for the redis cache entry. If not specified, the default distributed expiration time is used.</param>
    /// <param name="flags">The flags to use for this operation.</param>
    /// <param name="localCacheEnable">Set local memory or not?</param>
    T Get<T>(string cacheKey, Func<string, T> dataRetriever, TimeSpan? localExpiry = null, TimeSpan? redisExpiry = null,
        Flags flags = Flags.PreferMaster, bool localCacheEnable = true);

    /// <summary>
    /// Get the specified cacheKey, dataRetriever, and expiration.
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
    /// <param name="localCacheEnable">set local memory or not?</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the cached value, or null if the key is not found in the cache.</returns>
    Task<T> GetAsync<T>(string key, bool localCacheEnable = true);

    /// <summary>
    /// Asynchronously get the specified cacheKey, dataRetriever, and expiration.
    /// </summary>
    /// <returns>The get.</returns>
    /// <param name="cacheKey">Cache key.</param>
    /// <param name="dataRetriever">Data retriever.</param>
    /// <param name="localExpiry">The expiration time for the local cache entry. If not specified, the default local expiration time is used.</param>
    /// <param name="redisExpiry">The expiration time for the redis cache entry. If not specified, the default distributed expiration time is used.</param>
    /// <param name="flags">The flags to use for this operation.</param>
    /// <param name="localCacheEnable">set local memory or not?</param>
    /// <typeparam name="T">The 1st type parameter.</typeparam>
    Task<T> GetAsync<T>(string cacheKey, Func<string, Task<T>> dataRetriever, TimeSpan? localExpiry = null,
        TimeSpan? redisExpiry = null, Flags flags = Flags.PreferMaster, bool localCacheEnable = true);

    /// <summary>
    /// Asynchronously get the specified cacheKey, dataRetriever, and expiration.
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
    /// Try gets a cached value with the specified key.
    /// </summary>
    /// <typeparam name="T">The type of the cached value.</typeparam>
    /// <param name="key">The cache key.</param>
    /// <param name="value">The value of caching</param>
    /// <param name="localCacheEnable">Set local memory or not?</param>
    /// <returns>The cached value, or null if the key is not found in the cache.</returns>
    bool TryGetValue<T>(string key, bool localCacheEnable, out T value);

    /// <summary>
    /// Try gets a cached value with the specified key.
    /// </summary>
    /// <typeparam name="T">The type of the cached value.</typeparam>
    /// <param name="key">The cache key.</param>
    /// <param name="localCacheEnable">Set local memory or not?</param>
    /// <returns>
    /// A tuple containing the cached value, or null if the key is not found in the cache,
    /// and a boolean indicating whether the value was found in the cache.
    /// </returns>
    ValueTask<(bool success, T value)> TryGetValueAsync<T>(string key, bool localCacheEnable = true);

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
    /// <param name="flags">The flags to use for this operation.</param>
    bool Remove(string key, Flags flags = Flags.PreferMaster);

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
    /// <param name="flags">The flags to use for this operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task<bool> RemoveAsync(string key, Flags flags = Flags.PreferMaster);

    /// <summary>
    /// Deletes all keys in Redis that match the specified pattern, operating entirely on the Redis server side.
    /// This operation uses Lua scripting and the SCAN command to efficiently locate and delete keys, avoiding 
    /// the need to fetch them to the client side for deletion.
    /// </summary>
    /// <param name="pattern">The pattern to match Redis keys against (e.g., "my-prefix:*").</param>
    /// <param name="flags">Optional command flags to control the behavior of this Redis command.</param>
    /// <returns>A task that represents the asynchronous deleted keys as string array</returns>
    /// <remarks>
    /// <para>
    /// Since this operation may delete unknown keys from Redis, it also invalidates all local caches
    /// as they may now be out-of-date. Therefore, after this operation completes, all local caches
    /// should be cleared to ensure consistency.
    /// </para>
    /// <para>
    /// This operation requires Redis version 2.8.0 or higher, as it relies on the SCAN command and Lua scripting support.
    /// </para>
    /// </remarks>
    ValueTask RemoveWithPatternOnRedisAsync(string pattern, Flags flags = Flags.None);

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

    Task ClearAllAsync(Flags flags = Flags.PreferMaster);

    /// <summary>
    /// Ping all servers and clusters to health checking of Redis server
    /// </summary>
    /// <returns>Sum of pings durations</returns>
    Task<TimeSpan> PingAsync();

    /// <summary>
    /// Returns the IP and port number of the primary with that name.
    /// If failover is in progress or terminated successfully for this primary it returns the address and port of the promoted replica.
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
    /// This locking way has no expiration, and you must release that with disposing returned object.
    /// Note: Lock an existed key and token is not possible. But Set a locked key with difference, or same value is possible.
    /// </summary>
    /// <param name="key">The key of the lock.</param>
    /// <param name="expiry">The expiration of the lock key.</param>
    /// <param name="flags">The flags to use for this operation.</param>
    /// <returns>Return the <see cref="RedisLockObject"/> if the lock was successfully acquired; otherwise, wait until the key is released.</returns>
    Task<RedisLockObject> LockKeyAsync(string key, TimeSpan? expiry = null, Flags flags = Flags.None);

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
    /// Releases a lock if the token value is correct.
    /// </summary>
    /// <param name="key">The key of the lock.</param>
    /// <param name="token">The value at the key that must match.</param>
    /// <param name="flags">The flags to use for this operation.</param>
    /// <returns><see langword="true"/> if the lock was successfully released, <see langword="false"/> otherwise.</returns>
    bool TryReleaseLock(string key, string token, Flags flags = Flags.None);

    /// <summary>
    /// Increments the number stored at a key by increment.
    /// If the key does not exist, it is set to 0 before performing the operation.
    /// An error is returned if the key contains a value of the wrong type or contains a string that is not representable as integer.
    /// This operation is limited to 64-bit signed integers.
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

    /// <summary>
    /// Gets the Redis version of the connected server.
    /// </summary>
    Version GetServerVersion(Flags flags = Flags.None);

    /// <summary>
    /// Gets the features available to the connected server.
    /// </summary>
    Dictionary<string, string> GetServerFeatures(Flags flags = Flags.None);

    /// <inheritdoc cref="IHybridCache.KeyExpire(string, TimeSpan, Flags, ExpireCondition)"/>
    Task KeyExpireAsync(string key, TimeSpan expiry, Flags flags = Flags.None, ExpireCondition expireWhen = ExpireCondition.Always);

    /// <summary>
    /// Set a timeout on <paramref name="key"/>.
    /// After the timeout has expired, the key will automatically be deleted.
    /// A key with an associated timeout is said to be volatile in Redis terminology.
    /// </summary>
    /// <param name="key">The key to set the expiration for.</param>
    /// <param name="expiry">The timeout to set.</param>
    /// <param name="expireWhen">In Redis 7+, we can choose under which condition the expiration will be set using <see cref="ExpireWhen"/>.</param>
    /// <param name="flags">The flags to use for this operation.</param>
    /// <returns><see langword="true"/> if the timeout was set. <see langword="false"/> if key does not exist or the timeout could not be set.</returns>
    /// <remarks>
    /// See
    /// <seealso href="https://redis.io/commands/expire"/>,
    /// <seealso href="https://redis.io/commands/pexpire"/>.
    /// </remarks>
    void KeyExpire(string key, TimeSpan expiry, Flags flags = Flags.None, ExpireCondition expireWhen = ExpireCondition.Always);

    Task SetHashAsync(string key, IDictionary<string, string> keyValuePairs, TimeSpan? redisExpiry = null, Flags flags = Flags.PreferMaster);

    Task SetHashAsync(string key, string hashField, string value, Condition when = Condition.Always, Flags flags = Flags.PreferMaster);

    Task<Dictionary<string, string>> GetHashAsync(string key);
}
