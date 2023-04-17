using Microsoft.Extensions.Caching.Memory;
using StackExchange.Redis;
using System.Diagnostics;
using System.Text;
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
    private readonly TimeSpan _defaultExpiration;
    private readonly string _instanceName;
    private readonly string _instanceId;
    private readonly ISubscriber _redisSubscriber;
    private string InvalidationChannel => _instanceName + ":invalidate";

    /// <summary>
    /// This method initializes the HybridCache instance and subscribes to Redis key-space events 
    /// to invalidate cache entries on all instances. 
    /// </summary>
    /// <param name="redisConnectionString">Redis connection string</param>
    /// <param name="instanceName">Application unique name for redis indexes</param>
    /// <param name="defaultExpiryTime">default caching expiry time</param>
    public HybridCache(string redisConnectionString, string instanceName = null, TimeSpan? defaultExpiryTime = null)
    {
        _memoryCache = new MemoryCache(new MemoryCacheOptions());
        _redisConnection = ConnectionMultiplexer.Connect(redisConnectionString);
        _redisDb = _redisConnection.GetDatabase();
        _redisSubscriber = _redisConnection.GetSubscriber();
        _instanceId = Guid.NewGuid().ToString("N");
        _instanceName = instanceName;
        _defaultExpiration = defaultExpiryTime ?? TimeSpan.FromDays(30);

        if (string.IsNullOrEmpty(_instanceName))
        {
            _instanceName = _instanceId;
        }

        // Subscribe to Redis key-space events to invalidate cache entries on all instances
        _redisSubscriber.Subscribe(InvalidationChannel, OnRedisValuesChanged);
    }

    private void OnRedisValuesChanged(RedisChannel channel, RedisValue message)
    {
        // With this implementation, when a key is updated or removed in Redis,
        // all instances of HybridCache that are subscribed to the pub/sub channel will receive a message
        // and invalidate the corresponding key in their local MemoryCache.

        var cacheInvalidationMessage = Deserialize<CacheInvalidationMessage>(message.ToString());
        if (cacheInvalidationMessage.InstanceId != _instanceId) // filter out messages from the current instance
        {
            var cacheKey = cacheInvalidationMessage.CacheKey;
            _memoryCache.Remove(cacheKey);
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
        _memoryCache.Set(cacheKey, value, expiration ?? _defaultExpiration);
        _redisDb.StringSet(cacheKey, Serialize(value), expiration ?? _defaultExpiration,
            flags: fireAndForget ? CommandFlags.FireAndForget : CommandFlags.None);

        // include the instance ID in the pub/sub message payload to update another instances
        var message = new CacheInvalidationMessage(cacheKey, _instanceId);
        _redisDb.Publish(InvalidationChannel, Serialize(message));
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
            _memoryCache.Set(cacheKey, value, _defaultExpiration);
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
        _redisDb.Publish(InvalidationChannel, cacheKey);
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
        _memoryCache.Set(cacheKey, value, expiration ?? _defaultExpiration);
        await _redisDb.StringSetAsync(cacheKey, Serialize(value), expiration ?? _defaultExpiration,
            flags: fireAndForget ? CommandFlags.FireAndForget : CommandFlags.None);

        // include the instance ID in the pub/sub message payload to update another instances
        var message = new CacheInvalidationMessage(cacheKey, _instanceId);
        await _redisDb.PublishAsync(InvalidationChannel, Serialize(message));
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

        var redisValue = await _redisDb.StringGetAsync(cacheKey);
        if (redisValue.HasValue)
        {
            value = Deserialize<T>(redisValue);
            _memoryCache.Set(cacheKey, value, _defaultExpiration);
        }

        return value;
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
        await _redisDb.KeyDeleteAsync(cacheKey);
        await _redisDb.PublishAsync(InvalidationChannel, cacheKey);
    }

    private string GetCacheKey(string key) => $"{_instanceName}:{key}";


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