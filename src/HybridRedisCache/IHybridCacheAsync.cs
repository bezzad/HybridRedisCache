namespace HybridRedisCache;

public interface IHybridCacheAsync
{
    /// <inheritdoc cref="IHybridCache.Subscribe"/>
    Task SubscribeAsync(string channel, RedisChannelMessage handler);

    /// <inheritdoc cref="IHybridCache.Unsubscribe"/>
    Task UnsubscribeAsync(string channel);

    /// <inheritdoc cref="IHybridCache.Publish(string, string, string, Flags)"/>
    Task<long> PublishAsync(string channel, string key, string value, Flags flags = Flags.FireAndForget);

    /// <inheritdoc cref="IHybridCache.Exists"/>
    Task<bool> ExistsAsync(string key, Flags flags = Flags.None);

    /// <inheritdoc cref="IHybridCache.Set{T}(string, T, TimeSpan?, TimeSpan?, Flags, Condition, bool, bool, bool)"/>
    Task<bool> SetAsync<T>(string key, T value, TimeSpan? localExpiry = null, TimeSpan? redisExpiry = null,
        Flags flags = Flags.PreferMaster, Condition when = Condition.Always,
        bool keepTtl = false, bool localCacheEnable = true, bool redisCacheEnable = true);

    /// <inheritdoc cref="IHybridCache.Set{T}(string, T, HybridCacheEntry)"/>
    Task<bool> SetAsync<T>(string key, T value, HybridCacheEntry cacheEntry);
    
    /// <inheritdoc cref="IHybridCache.SetAll{T}(IDictionary{string, T}, TimeSpan?, TimeSpan?, Flags, Condition, bool, bool, bool)"/>
    Task<bool> SetAllAsync<T>(IDictionary<string, T> value, TimeSpan? localExpiry = null, TimeSpan? redisExpiry = null,
        Flags flags = Flags.PreferMaster, Condition when = Condition.Always,
        bool keepTtl = false, bool localCacheEnable = true, bool redisCacheEnable = true);

    /// <inheritdoc cref="IHybridCache.SetAll{T}(IDictionary{string, T}, HybridCacheEntry)"/>
    Task<bool> SetAllAsync<T>(IDictionary<string, T> value, HybridCacheEntry cacheEntry);

    /// <inheritdoc cref="IHybridCache.Get{T}(string, bool)"/>
    Task<T> GetAsync<T>(string key, bool localCacheEnable = true);

    /// <inheritdoc cref="IHybridCache.Get{T}(string, Func{string, T}, TimeSpan?, TimeSpan?, Flags, bool)"/>
    Task<T> GetAsync<T>(string cacheKey, Func<string, Task<T>> dataRetriever, TimeSpan? localExpiry = null,
        TimeSpan? redisExpiry = null, Flags flags = Flags.PreferMaster, bool localCacheEnable = true);

    /// <inheritdoc cref="IHybridCache.Get{T}(string, Func{string, T}, HybridCacheEntry)"/>
    Task<T> GetAsync<T>(string cacheKey, Func<string, Task<T>> dataRetriever, HybridCacheEntry cacheEntry);
    
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

    /// <inheritdoc cref="IHybridCache.Remove(string[], Flags)"/>
    Task<bool> RemoveAsync(string[] keys, Flags flags = Flags.PreferMaster);

    /// <inheritdoc cref="IHybridCache.Remove(string, Flags)"/>
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

    /// <inheritdoc cref="IHybridCache.GetExpiration(string)"/>
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

    /// <inheritdoc cref="IHybridCache.FlushLocalCaches"/>
    Task FlushLocalCachesAsync();

    /// <inheritdoc cref="IHybridCache.ClearAll"/>
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

    /// <inheritdoc cref="IHybridCache.KeyExpire(string, TimeSpan, Flags, ExpireCondition)"/>
    Task KeyExpireAsync(string key, TimeSpan expiry, Flags flags = Flags.None, ExpireCondition expireWhen = ExpireCondition.Always);

    /// <summary>
    /// Gets the values of the specified hash fields and sets their expiration times.
    /// </summary>
    /// <param name="key">The key of the hash.</param>
    /// <param name="hashFields">The fields in the hash to get and set the expiration for.</param>
    /// <param name="expiry">The expiration time to set.</param>
    /// <param name="when">Which conditions under which to set the field value (defaults to always).</param>
    /// <param name="flags">The flags to use for this operation.</param>
    /// <returns>The values of the specified hash fields.</returns>
    Task HashSetAsync(string key, IDictionary<string, string> hashFields, TimeSpan? expiry = null, Condition when = Condition.Always, Flags flags = Flags.PreferMaster);

    /// <summary>
    /// Sets field in the hash stored at key to value.
    /// If key does not exist, a new key holding a hash is created.
    /// If field already exists in the hash, it is overwritten.
    /// </summary>
    /// <param name="key">The key of the hash.</param>
    /// <param name="hashField">The field to set in the hash.</param>
    /// <param name="value">The value to set.</param>
    /// <param name="when">Which conditions under which to set the field value (defaults to always).</param>
    /// <param name="flags">The flags to use for this operation.</param>
    /// <returns><see langword="true"/> if field is a new field in the hash and value was set, <see langword="false"/> if field already exists in the hash and the value was updated.</returns>
    /// <remarks>
    /// See
    /// <seealso href="https://redis.io/commands/hset"/>,
    /// <seealso href="https://redis.io/commands/hsetnx"/>.
    /// </remarks>
    Task<bool> HashSetAsync(string key, string hashField, string value, Condition when = Condition.Always, Flags flags = Flags.PreferMaster);

    /// <summary>
    /// Returns all fields and values of the hash stored at key.
    /// </summary>
    /// <param name="key">The key of the hash to get all entries from.</param>
    /// <param name="flags">The flags to use for this operation.</param>
    /// <returns>List of fields and their values stored in the hash, or an empty list when key does not exist.</returns>
    /// <remarks><seealso href="https://redis.io/commands/hgetall"/></remarks>
    Task<Dictionary<string, string>> HashGetAsync(string key, Flags flags = Flags.None);

    /// <summary>
    /// Returns the value associated with field in the hash stored at key.
    /// </summary>
    /// <param name="key">The key of the hash.</param>
    /// <param name="hashField">The field in the hash to get.</param>
    /// <param name="flags">The flags to use for this operation.</param>
    /// <returns>The value associated with field, or Null when field is not present in the hash or key does not exist.</returns>
    /// <remarks><seealso href="https://redis.io/commands/hget"/></remarks>
    Task<string> HashGetAsync(string key, string hashField, Flags flags = Flags.None);

    /// <summary>
    /// Returns if field is an existing field in the hash stored at key.
    /// </summary>
    /// <param name="key">The key of the hash.</param>
    /// <param name="hashField">The field in the hash to check.</param>
    /// <param name="flags">The flags to use for this operation.</param>
    /// <returns><see langword="true"/> if the hash contains field, <see langword="false"/> if the hash does not contain field, or key does not exist.</returns>
    /// <remarks><seealso href="https://redis.io/commands/hexists"/></remarks>
    Task<bool> HashExistsAsync(string key, string hashField, Flags flags = Flags.PreferMaster);

    /// <summary>
    /// Removes the specified fields from the hash stored at key.
    /// Non-existing fields are ignored. Non-existing keys are treated as empty hashes and this command returns 0.
    /// </summary>
    /// <param name="key">The key of the hash.</param>
    /// <param name="hashField">The field in the hash to delete.</param>
    /// <param name="flags">The flags to use for this operation.</param>
    /// <returns><see langword="true"/> if the field was removed.</returns>
    /// <remarks><seealso href="https://redis.io/commands/hdel"/></remarks>
    Task<bool> HashDeleteAsync(string key, string hashField, Flags flags = Flags.PreferMaster);

    /// <summary>
    /// Removes the specified fields from the hash stored at key.
    /// Non-existing fields are ignored. Non-existing keys are treated as empty hashes and this command returns 0.
    /// </summary>
    /// <param name="key">The key of the hash.</param>
    /// <param name="hashFields">The fields in the hash to delete.</param>
    /// <param name="flags">The flags to use for this operation.</param>
    /// <returns>The number of fields that were removed.</returns>
    /// <remarks><seealso href="https://redis.io/commands/hdel"/></remarks>
    Task<long> HashDeleteAsync(string key, string[] hashFields, Flags flags = Flags.PreferMaster);

    /// <summary>
    /// The HSCAN command is used to incrementally iterate over a hash.
    /// </summary>
    /// <param name="key">The key of the hash.</param>
    /// <param name="pattern">The pattern of keys to get entries for.</param>
    /// <param name="flags">The flags to use for this operation.</param>
    /// <returns>Yields all elements of the hash matching the pattern.</returns>
    /// <remarks><seealso href="https://redis.io/commands/hscan"/></remarks>
    IAsyncEnumerable<KeyValuePair<string, string>> HashScanAsync(string key, string pattern, Flags flags = Flags.None);

    /// <summary>
    /// The HSCAN command is used to incrementally iterate over a hash and return only field names.
    /// Note: to resume an iteration via <i>cursor</i>, cast the original enumerable or enumerator to <see cref="IScanningCursor"/>.
    /// </summary>
    /// <param name="key">The key of the hash.</param>
    /// <param name="pattern">The pattern of keys to get entries for.</param>
    /// <param name="flags">The flags to use for this operation.</param>
    /// <returns>Yields all elements of the hash matching the pattern.</returns>
    /// <remarks><seealso href="https://redis.io/commands/hscan"/></remarks>
    IAsyncEnumerable<string> HashScanNoValuesAsync(string key, string pattern, Flags flags = Flags.None);

    /// <summary>
    /// Returns all values in the hash stored at key.
    /// </summary>
    /// <param name="key">The key of the hash.</param>
    /// <param name="flags">The flags to use for this operation.</param>
    /// <returns>List of values in the hash, or an empty list when key does not exist.</returns>
    /// <remarks><seealso href="https://redis.io/commands/hvals"/></remarks>
    Task<string[]> HashValuesAsync(string key, Flags flags = Flags.None);

    /// <summary>
    /// Returns all field names in the hash stored at key.
    /// </summary>
    /// <param name="key">The key of the hash.</param>
    /// <param name="flags">The flags to use for this operation.</param>
    /// <returns>List of fields in the hash, or an empty list when key does not exist.</returns>
    /// <remarks><seealso href="https://redis.io/commands/hkeys"/></remarks>
    Task<string[]> HashKeysAsync(string key, Flags flags = Flags.None);
    
    /// <summary>
    /// Returns the value associated with field in the hash stored at key.
    /// </summary>
    /// <param name="key">The key of the hash.</param>
    /// <param name="hashField">The field in the hash to get.</param>
    /// <param name="flags">The flags to use for this operation.</param>
    /// <returns>The value associated with field, or Null when field is not present in the hash or key does not exist.</returns>
    /// <remarks><seealso href="https://redis.io/commands/hget"/></remarks>
    Task<string> HashFieldGetAndDeleteAsync(string key, string hashField, Flags flags = Flags.None);

    /// <summary>
    /// Returns the number of fields contained in the hash stored at key.
    /// </summary>
    /// <param name="key">The key of the hash.</param>
    /// <param name="flags">The flags to use for this operation.</param>
    /// <returns>The number of fields in the hash, or 0 when key does not exist.</returns>
    /// <remarks><seealso href="https://redis.io/commands/hlen"/></remarks>
    Task<long> HashLengthAsync(string key, Flags flags = Flags.None);
}
