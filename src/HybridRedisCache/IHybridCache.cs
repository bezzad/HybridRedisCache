namespace HybridRedisCache;

public interface IHybridCache : IHybridCacheAsync
{
    /// <summary>
    /// Subscribe to perform some operation when a message to the preferred/active node is broadcast,
    /// without any guarantee of ordered handling.
    /// Channel: Redis KeySpace
    /// </summary>
    event RedisBusMessage OnRedisBusMessage;

    /// <summary>
    /// Describes functionality that is common to both standalone redis servers and redis clusters.
    /// </summary>
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
    /// Returns the remaining time to live of a key that has a timeout.
    /// This introspection capability allows a Redis client to check how many seconds a given key will continue to be part of the dataset.
    /// </summary>
    /// <param name="key">The key to check.</param>
    /// <returns>TTL, or <see langword="null"/> when key does not exist or does not have a timeout.</returns>
    /// <remarks><seealso href="https://redis.io/commands/ttl"/></remarks>    
    TimeSpan? GetExpiration(string key);

    void FlushLocalCaches();

    void ClearAll(Flags flags = Flags.PreferMaster);

    /// <summary>
    /// Gets the Redis version of the connected server.
    /// </summary>
    Version GetServerVersion(Flags flags = Flags.None);

    /// <summary>
    /// Gets the features available to the connected server.
    /// </summary>
    Dictionary<string, string> GetServerFeatures(Flags flags = Flags.None);

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
}
