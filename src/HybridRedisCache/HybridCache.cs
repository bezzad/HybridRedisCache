using Microsoft.Extensions.Caching.Memory;
using StackExchange.Redis;
using System.Text.Json;

namespace HybridRedisCache;

/// <summary>
/// The HybridCache class provides a hybrid caching solution that stores cached items in both
/// an in-memory cache and a Redis cache. 
/// </summary>
public class HybridCache : IHybridCache, IDisposable
{
    private readonly IMemoryCache _memoryCache;
    private readonly IDatabase _redisDb;
    private readonly ConnectionMultiplexer _redisConnection;
    private readonly string _instanceId;
    private readonly HybridCachingOptions _option;
    private readonly ISubscriber _redisSubscriber;
    private string InvalidationChannel => _option.InstanceName + ":invalidate";

    /// <summary>
    /// This method initializes the HybridCache instance and subscribes to Redis key-space events 
    /// to invalidate cache entries on all instances. 
    /// </summary>
    /// <param name="redisConnectionString">Redis connection string</param>
    /// <param name="instanceName">Application unique name for redis indexes</param>
    /// <param name="defaultExpiryTime">default caching expiry time</param>
    public HybridCache(HybridCachingOptions option)
    {
        _instanceId = Guid.NewGuid().ToString("N");
        _option = option;
        _memoryCache = new MemoryCache(new MemoryCacheOptions());
        _redisConnection = ConnectionMultiplexer.Connect(option.RedisCacheConnectString);
        _redisDb = _redisConnection.GetDatabase();
        _redisSubscriber = _redisConnection.GetSubscriber();

        if (string.IsNullOrWhiteSpace(option.InstanceName))
        {
            _option.InstanceName = _instanceId;
        }

        // Subscribe to Redis key-space events to invalidate cache entries on all instances
        _redisSubscriber.Subscribe(InvalidationChannel, OnMessage, CommandFlags.FireAndForget);
    }

    private void OnMessage(RedisChannel channel, RedisValue value)
    {
        // With this implementation, when a key is updated or removed in Redis,
        // all instances of HybridCache that are subscribed to the pub/sub channel will receive a message
        // and invalidate the corresponding key in their local MemoryCache.

        var message = Deserialize<CacheInvalidationMessage>(value.ToString());
        if (message.InstanceId != _instanceId) // filter out messages from the current instance
        {
            foreach (var key in message.CacheKeys)
            {
                _memoryCache.Remove(key);
            }
        }
    }

    /// <summary>
    /// Sets a value in the cache with the specified key.
    /// </summary>
    /// <typeparam name="T">The type of the value to cache.</typeparam>
    /// <param name="key">The cache key.</param>
    /// <param name="value">The value to cache.</param>
    /// <param name="expiration">The expiration time for the cache entry. If not specified, the default expiration time is used.</param>
    /// <param name="fireAndForget">Whether to cache the value in Redis without waiting for the operation to complete.</param>
    public void Set<T>(string key, T value, TimeSpan? expiration = null, bool fireAndForget = true)
    {
        var cacheKey = GetCacheKey(key);
        _memoryCache.Set(cacheKey, value, expiration ?? _option.DefaultExpirationTime);
        _redisDb.StringSet(cacheKey, Serialize(value), expiration ?? _option.DefaultExpirationTime,
            flags: fireAndForget ? CommandFlags.FireAndForget : CommandFlags.None);

        PublishBus(cacheKey);
    }

    /// <summary>
    /// Asynchronously sets a value in the cache with the specified key.
    /// </summary>
    /// <typeparam name="T">The type of the value to cache.</typeparam>
    /// <param name="key">The cache key.</param>
    /// <param name="value">The value to cache.</param>
    /// <param name="expiration">The expiration time for the cache entry. If not specified, the default expiration time is used.</param>
    /// <param name="fireAndForget">Whether to cache the value in Redis without waiting for the operation to complete.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, bool fireAndForget = true)
    {
        var cacheKey = GetCacheKey(key);
        _memoryCache.Set(cacheKey, value, expiration ?? _option.DefaultExpirationTime);
        await _redisDb.StringSetAsync(cacheKey, Serialize(value), expiration ?? _option.DefaultExpirationTime,
            flags: fireAndForget ? CommandFlags.FireAndForget : CommandFlags.None).ConfigureAwait(false);

        await PublishBusAsync(cacheKey).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets a cached value with the specified key.
    /// </summary>
    /// <typeparam name="T">The type of the cached value.</typeparam>
    /// <param name="key">The cache key.</param>
    /// <returns>The cached value, or null if the key is not found in the cache.</returns>
    public T Get<T>(string key)
    {
        var cacheKey = GetCacheKey(key);
        var value = _memoryCache.Get<T>(cacheKey);
        if (value != null)
        {
            return value;
        }

        var redisValue = _redisDb.StringGet(cacheKey);
        if (redisValue.HasValue)
        {
            value = Deserialize<T>(redisValue);
            _memoryCache.Set(cacheKey, value, _option.DefaultExpirationTime);
        }

        return value;
    }
        
    /// <summary>
    /// Asynchronously gets a cached value with the specified key.
    /// </summary>
    /// <typeparam name="T">The type of the cached value.</typeparam>
    /// <param name="key">The cache key.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the cached value, or null if the key is not found in the cache.</returns>
    public async Task<T> GetAsync<T>(string key)
    {
        var cacheKey = GetCacheKey(key);
        var value = _memoryCache.Get<T>(cacheKey);
        if (value != null)
        {
            return value;
        }

        var redisValue = await _redisDb.StringGetAsync(cacheKey).ConfigureAwait(false);
        if (redisValue.HasValue)
        {
            value = Deserialize<T>(redisValue);
            _memoryCache.Set(cacheKey, value, _option.DefaultExpirationTime);
        }

        return value;
    }

    /// <summary>
    /// Removes a cached value with the specified key.
    /// </summary>
    /// <param name="key">The cache key.</param>
    public void Remove(string key)
    {
        var cacheKey = GetCacheKey(key);
        _memoryCache.Remove(cacheKey);
        _redisDb.KeyDelete(cacheKey);
        PublishBus(cacheKey);
    }

    /// <summary>
    /// Asynchronously removes a cached value with the specified key.
    /// </summary>
    /// <param name="key">The cache key.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task RemoveAsync(string key)
    {
        var cacheKey = GetCacheKey(key);
        _memoryCache.Remove(cacheKey);
        await _redisDb.KeyDeleteAsync(cacheKey).ConfigureAwait(false);
        await _redisDb.PublishAsync(InvalidationChannel, cacheKey).ConfigureAwait(false);
    }

    private string GetCacheKey(string key) => $"{_option.InstanceName}:{key}";

    private async Task PublishBusAsync(params string[] cacheKeys)
    {
        // include the instance ID in the pub/sub message payload to update another instances
        var message = new CacheInvalidationMessage(_instanceId, cacheKeys);
        await _redisDb.PublishAsync(InvalidationChannel, Serialize(message)).ConfigureAwait(false);
    }
    private void PublishBus(params string[] cacheKeys)
    {
        // include the instance ID in the pub/sub message payload to update another instances
        var message = new CacheInvalidationMessage(_instanceId, cacheKeys);
        _redisDb.Publish(InvalidationChannel, Serialize(message));
    }

    private static string Serialize<T>(T value)
    {
        if (value == null)
        {
            return null;
        }

        return JsonSerializer.Serialize(value);
    }

    private static T Deserialize<T>(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return default;
        }

        return JsonSerializer.Deserialize<T>(value);
    }

    public void Dispose()
    {
        _redisSubscriber?.UnsubscribeAll();
        _redisConnection?.Dispose();
        _memoryCache?.Dispose();
    }
}