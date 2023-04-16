using Microsoft.Extensions.Caching.Memory;
using StackExchange.Redis;
using System.Text;
using System.Text.Json;

public class HybridCache : IHybridCache, IDisposable
{
    private readonly IMemoryCache _memoryCache;
    private readonly IDatabase _redisDb;
    private readonly ConnectionMultiplexer _redisConnection;
    private readonly TimeSpan _defaultExpiration;
    private readonly string _instanceName;
    private readonly ISubscriber _redisSubscriber;

    public HybridCache(string redisConnectionString, string instanceName, TimeSpan? defaultExpiration = null)
    {
        _memoryCache = new MemoryCache(new MemoryCacheOptions());
        _redisConnection = ConnectionMultiplexer.Connect(redisConnectionString);
        _redisDb = _redisConnection.GetDatabase();
        _redisSubscriber = _redisConnection.GetSubscriber();
        _instanceName = instanceName;
        _defaultExpiration = defaultExpiration ?? TimeSpan.FromMinutes(30);

        // Subscribe to Redis key-space events to invalidate cache entries on all instances
        _redisSubscriber.Subscribe(GetInvalidationChannel(), (channel, message) =>
        {
            var cacheKey = message.ToString();
            _memoryCache.Remove(cacheKey);
        });
    }

    public void Set<T>(string key, T value, TimeSpan? expiration = null)
    {
        var cacheKey = GetCacheKey(key);
        _memoryCache.Set(cacheKey, value, expiration ?? _defaultExpiration);
        _redisDb.StringSet(cacheKey, Serialize(value), expiration ?? _defaultExpiration);
    }

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

    public void Remove(string key)
    {
        var cacheKey = GetCacheKey(key);
        _memoryCache.Remove(cacheKey);
        _redisDb.KeyDelete(cacheKey);
        _redisDb.Publish(GetInvalidationChannel(), cacheKey);
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expiration = null)
    {
        var cacheKey = GetCacheKey(key);
        _memoryCache.Set(cacheKey, value, expiration ?? _defaultExpiration);
        await _redisDb.StringSetAsync(cacheKey, Serialize(value), expiration ?? _defaultExpiration);
    }

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

    public async Task RemoveAsync(string key)
    {
        var cacheKey = GetCacheKey(key);
        _memoryCache.Remove(cacheKey);
        await _redisDb.KeyDeleteAsync(cacheKey);
        await _redisDb.PublishAsync(GetInvalidationChannel(), cacheKey);
    }

    private string GetCacheKey(string key)
    {
        return $"{_instanceName}:{key}";
    }

    private string GetInvalidationChannel()
    {
        return $"{_instanceName}:invalidate";
    }

    private byte[] Serialize<T>(T value)
    {
        if (value == null)
        {
            return null;
        }

        var json = JsonSerializer.Serialize(value);
        return Encoding.UTF8.GetBytes(json);
    }

    private T Deserialize<T>(byte[] bytes)
    {
        if (bytes == null)
        {
            return default;
        }

        var json = Encoding.UTF8.GetString(bytes);
        return JsonSerializer.Deserialize<T>(json);
    }

    public void Dispose()
    {
        _redisConnection.Dispose();
        _memoryCache.Dispose();
    }
}