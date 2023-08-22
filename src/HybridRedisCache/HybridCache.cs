using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace HybridRedisCache;

/// <summary>
/// The HybridCache class provides a hybrid caching solution that stores cached items in both
/// an in-memory cache and a Redis cache. 
/// </summary>
public class HybridCache : IHybridCache, IDisposable
{
    private const string FlushDb = "FLUSHDB";
    private readonly IDatabase _redisDb;
    private readonly string _instanceId;
    private readonly HybridCachingOptions _options;
    private readonly ISubscriber _redisSubscriber;
    private readonly ILogger _logger;

    private IMemoryCache _memoryCache;
    private string InvalidationChannel => _options.InstancesSharedName + ":invalidate";
    private int retryPublishCounter = 0;
    private int exponentialRetryMilliseconds = 100;
    private string ClearAllKey => GetCacheKey($"*{FlushDb}*");

    /// <summary>
    /// This method initializes the HybridCache instance and subscribes to Redis key-space events 
    /// to invalidate cache entries on all instances. 
    /// </summary>
    /// <param name="redisConnectionString">Redis connection string</param>
    /// <param name="instanceName">Application unique name for redis indexes</param>
    /// <param name="defaultExpiryTime">default caching expiry time</param>
    public HybridCache(HybridCachingOptions option, ILoggerFactory loggerFactory = null)
    {
        option.NotNull(nameof(option));

        _instanceId = Guid.NewGuid().ToString("N");
        _options = option;
        CreateLocalCache();
        var redisConfig = ConfigurationOptions.Parse(option.RedisConnectString, true);
        redisConfig.AbortOnConnectFail = option.AbortOnConnectFail;
        redisConfig.ConnectRetry = option.ConnectRetry;
        redisConfig.ClientName = option.InstancesSharedName + ":" + _instanceId;
        var redis = ConnectionMultiplexer.Connect(redisConfig);

        _redisDb = redis.GetDatabase();
        _redisSubscriber = redis.GetSubscriber();
        _logger = loggerFactory?.CreateLogger(nameof(HybridCache));

        // Subscribe to Redis key-space events to invalidate cache entries on all instances
        _redisSubscriber.Subscribe(InvalidationChannel, OnMessage, CommandFlags.FireAndForget);
        redis.ConnectionRestored += OnReconnect;
    }

    private void CreateLocalCache()
    {
        _memoryCache = new MemoryCache(new MemoryCacheOptions());
    }

    private void OnMessage(RedisChannel channel, RedisValue value)
    {
        // With this implementation, when a key is updated or removed in Redis,
        // all instances of HybridCache that are subscribed to the pub/sub channel will receive a message
        // and invalidate the corresponding key in their local MemoryCache.

        var message = value.ToString().Deserialize<CacheInvalidationMessage>();
        if (message.InstanceId != _instanceId) // filter out messages from the current instance
        {
            if (message.CacheKeys.FirstOrDefault().Equals(ClearAllKey))
            {
                ClearLocalMemory();
                return;
            }

            foreach (var key in message.CacheKeys)
            {
                _memoryCache.Remove(key);
                LogMessage($"remove local cache that cache key is {key}");
            }
        }
    }

    /// <summary>
    /// On reconnect (flushes local memory as it could be stale).
    /// </summary>
    private void OnReconnect(object sender, ConnectionFailedEventArgs e)
    {
        if (_options.FlushLocalCacheOnBusReconnection)
        {
            LogMessage("Flushing local cache due to bus reconnection");
            ClearLocalMemory();
        }
    }

    /// <summary>
    /// Exists the specified Key in cache
    /// </summary>
    /// <returns>The exists</returns>
    /// <param name="cacheKey">Cache key</param>
    public bool Exists(string key)
    {
        key.NotNullOrWhiteSpace(nameof(key));
        var cacheKey = GetCacheKey(key);

        // Circuit Breaker may be more better
        try
        {
            if (_redisDb.KeyExists(cacheKey))
                return true;
        }
        catch (Exception ex)
        {
            LogMessage($"Check cache key exists error [{key}] ", ex);
            if (_options.ThrowIfDistributedCacheError)
            {
                throw;
            }
        }

        return _memoryCache.TryGetValue(cacheKey, out var _);
    }

    /// <summary>
    /// Exists the specified cacheKey async.
    /// </summary>
    /// <returns>The async.</returns>
    /// <param name="cacheKey">Cache key.</param>
    /// <param name="cancellationToken">CancellationToken</param>
    public async Task<bool> ExistsAsync(string key)
    {
        key.NotNullOrWhiteSpace(nameof(key));
        var cacheKey = GetCacheKey(key);

        // Circuit Breaker may be more better
        try
        {
            if (await _redisDb.KeyExistsAsync(cacheKey))
                return true;
        }
        catch (Exception ex)
        {
            LogMessage($"Check cache key [{key}] exists error", ex);
            if (_options.ThrowIfDistributedCacheError)
            {
                throw;
            }
        }

        return _memoryCache.TryGetValue(cacheKey, out var _);
    }

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
    public void Set<T>(string key, T value, TimeSpan? localExpiry = null, TimeSpan? redisExpiry = null, bool fireAndForget = true)
    {
        key.NotNullOrWhiteSpace(nameof(key));
        SetExpiryTimes(ref localExpiry, ref redisExpiry);
        var cacheKey = GetCacheKey(key);
        _memoryCache.Set(cacheKey, value, localExpiry.Value);

        try
        {
            _redisDb.StringSet(cacheKey, value.Serialize(), redisExpiry.Value,
                    flags: fireAndForget ? CommandFlags.FireAndForget : CommandFlags.None);
        }
        catch (Exception ex)
        {
            LogMessage($"set cache key [{key}] error", ex);

            if (_options.ThrowIfDistributedCacheError)
            {
                throw;
            }
        }

        // When create/update cache, send message to bus so that other clients can remove it.
        PublishBus(cacheKey);
    }

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
    public void Set<T>(string key, T value, HybridCacheEntry cacheEntry)
    {
        key.NotNullOrWhiteSpace(nameof(key));
        var cacheKey = GetCacheKey(key);

        try
        {
            //SetExpiryTimes(ref cacheEntry.LocalExpiry, ref cacheEntry.RedisExpiry);
            if (cacheEntry.LocalCacheEnable)
                _memoryCache.Set(cacheKey, value, cacheEntry.LocalExpiry.Value);

            if (cacheEntry.RedisCacheEnable)
                _redisDb.StringSet(cacheKey, value.Serialize(), cacheEntry.RedisExpiry.Value,
                    flags: cacheEntry.FireAndForget ? CommandFlags.FireAndForget : CommandFlags.None);
        }
        catch (Exception ex)
        {
            LogMessage($"set cache key [{key}] error", ex);

            if (_options.ThrowIfDistributedCacheError)
            {
                throw;
            }
        }

        // When create/update cache, send message to bus so that other clients can remove it.
        PublishBus(cacheKey);
    }

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
    public async Task SetAsync<T>(string key, T value, TimeSpan? localExpiry = null, TimeSpan? redisExpiry = null, bool fireAndForget = true)
    {
        key.NotNullOrWhiteSpace(nameof(key));
        SetExpiryTimes(ref localExpiry, ref redisExpiry);
        var cacheKey = GetCacheKey(key);
        _memoryCache.Set(cacheKey, value, localExpiry.Value);

        try
        {
            await _redisDb.StringSetAsync(cacheKey, value.Serialize(), redisExpiry.Value,
                    flags: fireAndForget ? CommandFlags.FireAndForget : CommandFlags.None).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            LogMessage($"set cache key [{key}] error", ex);

            if (_options.ThrowIfDistributedCacheError)
            {
                throw;
            }
        }

        // When create/update cache, send message to bus so that other clients can remove it.
        await PublishBusAsync(cacheKey).ConfigureAwait(false);
    }

    /// <summary>
    /// Sets all.
    /// </summary>
    /// <returns>The all async.</returns>
    /// <param name="value">Value.</param>
    /// <param name="localExpiry">The expiration time for the local cache entry. If not specified, the default local expiration time is used.</param>
    /// <param name="redisExpiry">The expiration time for the redis cache entry. If not specified, the default distributed expiration time is used.</param>
    /// <param name="fireAndForget">Whether to cache the value in Redis without waiting for the operation to complete.</param>
    /// <typeparam name="T">The 1st type parameter.</typeparam>
    public void SetAll<T>(IDictionary<string, T> value, TimeSpan? localExpiry = null, TimeSpan? redisExpiry = null, bool fireAndForget = true)
    {
        value.NotNullAndCountGTZero(nameof(value));
        SetExpiryTimes(ref localExpiry, ref redisExpiry);

        foreach (var kvp in value)
        {
            var cacheKey = GetCacheKey(kvp.Key);
            _memoryCache.Set(cacheKey, kvp.Value, localExpiry.Value);

            try
            {
                _redisDb.StringSet(cacheKey, kvp.Value.Serialize(), redisExpiry.Value,
                     flags: fireAndForget ? CommandFlags.FireAndForget : CommandFlags.None);
            }
            catch (Exception ex)
            {
                LogMessage($"set cache key [{kvp.Key}] error", ex);

                if (_options.ThrowIfDistributedCacheError)
                {
                    throw;
                }
            }
        }

        // send message to bus 
        PublishBus(value.Keys.ToArray());
    }

    /// <summary>
    /// Sets all async.
    /// </summary>
    /// <returns>The all async.</returns>
    /// <param name="value">Value.</param>
    /// <param name="localExpiry">The expiration time for the local cache entry. If not specified, the default local expiration time is used.</param>
    /// <param name="redisExpiry">The expiration time for the redis cache entry. If not specified, the default distributed expiration time is used.</param>
    /// <typeparam name="T">The 1st type parameter.</typeparam>
    public async Task SetAllAsync<T>(IDictionary<string, T> value, TimeSpan? localExpiry = null, TimeSpan? redisExpiry = null, bool fireAndForget = true)
    {
        value.NotNullAndCountGTZero(nameof(value));
        SetExpiryTimes(ref localExpiry, ref redisExpiry);

        foreach (var kvp in value)
        {
            var cacheKey = GetCacheKey(kvp.Key);
            _memoryCache.Set(cacheKey, kvp.Value, localExpiry.Value);

            try
            {
                await _redisDb.StringSetAsync(cacheKey, kvp.Value.Serialize(), redisExpiry.Value,
                     flags: fireAndForget ? CommandFlags.FireAndForget : CommandFlags.None).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                LogMessage($"set cache key [{kvp.Key}] error", ex);

                if (_options.ThrowIfDistributedCacheError)
                {
                    throw;
                }
            }
        }

        // send message to bus 
        await PublishBusAsync(value.Keys.ToArray());
    }

    /// <summary>
    /// Gets a cached value with the specified key.
    /// </summary>
    /// <typeparam name="T">The type of the cached value.</typeparam>
    /// <param name="key">The cache key.</param>
    /// <returns>The cached value, or null if the key is not found in the cache.</returns>
    public T Get<T>(string key)
    {
        key.NotNullOrWhiteSpace(nameof(key));
        var cacheKey = GetCacheKey(key);
        if (_memoryCache.TryGetValue(cacheKey, out T value))
        {
            return value;
        }

        try
        {
            var redisValue = _redisDb.StringGet(cacheKey);
            if (redisValue.HasValue)
            {
                value = redisValue.ToString().Deserialize<T>();
            }
        }
        catch (Exception ex)
        {
            LogMessage($"Redis cache get error, [{key}]", ex);
            if (_options.ThrowIfDistributedCacheError)
                throw;
        }

        if (value != null)
        {
            var expiry = GetExpiration(key);
            _memoryCache.Set(cacheKey, value, expiry);
            return value;
        }

        LogMessage($"distributed cache can not get the value of `{key}` key");
        return value;
    }

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
    public T Get<T>(string key, Func<string, T> dataRetriever, TimeSpan? localExpiry = null, TimeSpan? redisExpiry = null, bool fireAndForget = true)
    {
        key.NotNullOrWhiteSpace(nameof(key));
        SetExpiryTimes(ref localExpiry, ref redisExpiry);
        var cacheKey = GetCacheKey(key);

        if (_memoryCache.TryGetValue(cacheKey, out T value))
        {
            return value;
        }

        try
        {
            var redisValue = _redisDb.StringGet(cacheKey);
            if (redisValue.HasValue)
            {
                value = redisValue.ToString().Deserialize<T>();
            }
        }
        catch (Exception ex)
        {
            LogMessage($"Redis cache get error, [{key}]", ex);
            if (_options.ThrowIfDistributedCacheError)
                throw;
        }

        if (value is not null)
        {
            _memoryCache.Set(cacheKey, value, localExpiry.Value);
            return value;
        }

        try
        {
            value = dataRetriever(key);
        }
        catch (Exception ex)
        {
            LogMessage($"get with data retriever error [{key}]", ex);
            if (_options.ThrowIfDistributedCacheError)
                throw;
        }

        if (value is not null)
        {
            Set(key, value, localExpiry, redisExpiry, fireAndForget);
            return value;
        }

        LogMessage($"distributed cache can not get the value of `{key}` key. Data retriver also had problem.");
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
        key.NotNullOrWhiteSpace(nameof(key));
        var cacheKey = GetCacheKey(key);
        if(_memoryCache.TryGetValue(cacheKey, out T value))
        {
            return value;
        }

        try
        {
            var redisValue = await _redisDb.StringGetAsync(cacheKey).ConfigureAwait(false);
            if (redisValue.HasValue)
            {
                value = redisValue.ToString().Deserialize<T>();
            }
        }
        catch (Exception ex)
        {
            LogMessage($"Redis cache get error, [{key}]", ex);
            if (_options.ThrowIfDistributedCacheError)
                throw;
        }

        if (value != null)
        {
            var expiry = await GetExpirationAsync(key);
            _memoryCache.Set(cacheKey, value, expiry);
            return value;
        }

        LogMessage($"distributed cache can not get the value of `{key}` key");
        return value;
    }

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
    public async Task<T> GetAsync<T>(string key, Func<string, Task<T>> dataRetriever,
        TimeSpan? localExpiry = null, TimeSpan? redisExpiry = null, bool fireAndForget = true)
    {
        key.NotNullOrWhiteSpace(nameof(key));
        SetExpiryTimes(ref localExpiry, ref redisExpiry);
        var cacheKey = GetCacheKey(key);

        if (_memoryCache.TryGetValue(cacheKey, out T value))
        {
            return value;
        }

        try
        {
            var redisValue = await _redisDb.StringGetAsync(cacheKey).ConfigureAwait(false);
            if (redisValue.HasValue)
            {
                value = redisValue.ToString().Deserialize<T>();
            }
        }
        catch (Exception ex)
        {
            LogMessage($"Redis cache get error, [{key}]", ex);
            if (_options.ThrowIfDistributedCacheError)
                throw;
        }

        if (value is not null)
        {
            _memoryCache.Set(cacheKey, value, localExpiry.Value);
            return value;
        }

        try
        {
            value = await dataRetriever(key);
        }
        catch (Exception ex)
        {
            LogMessage($"get with data retriever error [{key}]", ex);
            if (_options.ThrowIfDistributedCacheError)
                throw;
        }

        if (value is not null)
        {
            Set(key, value, localExpiry, redisExpiry, fireAndForget);
            return value;
        }

        LogMessage($"distributed cache can not get the value of `{key}` key. Data retriver also had a problem.");
        return value;
    }

    /// <summary>
    /// Try gets a cached value with the specified key.
    /// </summary>
    /// <typeparam name="T">The type of the cached value.</typeparam>
    /// <param name="key">The cache key.</param>
    /// <returns>The cached value, or null if the key is not found in the cache.</returns>
    public bool TryGetValue<T>(string key, out T value)
    {
        key.NotNullOrWhiteSpace(nameof(key));
        var cacheKey = GetCacheKey(key);
        if (_memoryCache.TryGetValue(cacheKey, out value))
        {
            return true;
        }

        try
        {
            var redisValue = _redisDb.StringGet(cacheKey);
            if (redisValue.HasValue)
            {
                value = redisValue.ToString().Deserialize<T>();
            }
        }
        catch (Exception ex)
        {
            LogMessage($"Redis cache get error, [{key}]", ex);
            if (_options.ThrowIfDistributedCacheError)
                throw;
        }

        if (value != null)
        {
            var expiry = GetExpiration(key);
            _memoryCache.Set(cacheKey, value, expiry);
            return true;
        }

        LogMessage($"distributed cache can not get the value of `{key}` key");
        return false;
    }

    /// <summary>
    /// Removes a cached value with the specified key.
    /// </summary>
    /// <param name="key">The cache key.</param>
    public void Remove(params string[] keys)
    {
        keys.NotNullAndCountGTZero(nameof(keys));
        var cacheKeys = Array.ConvertAll(keys, GetCacheKey);
        try
        {
            // distributed cache at first
            _redisDb.KeyDelete(Array.ConvertAll(cacheKeys, x => (RedisKey)x));
        }
        catch (Exception ex)
        {
            LogMessage($"remove cache key [{string.Join(" | ", keys)}] error", ex);

            if (_options.ThrowIfDistributedCacheError)
            {
                throw;
            }
        }

        Array.ForEach(cacheKeys, _memoryCache.Remove);

        // send message to bus 
        PublishBus(cacheKeys);
    }

    /// <summary>
    /// Asynchronously removes a cached value with the specified key.
    /// </summary>
    /// <param name="keys">The cache key.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task RemoveAsync(params string[] keys)
    {
        keys.NotNullAndCountGTZero(nameof(keys));
        var cacheKeys = Array.ConvertAll(keys, GetCacheKey);
        try
        {
            // distributed cache at first
            await _redisDb.KeyDeleteAsync(Array.ConvertAll(cacheKeys, x => (RedisKey)x)).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            LogMessage($"remove cache key [{string.Join(" | ", keys)}] error", ex);

            if (_options.ThrowIfDistributedCacheError)
            {
                throw;
            }
        }

        Array.ForEach(cacheKeys, _memoryCache.Remove);

        // send message to bus 
        await PublishBusAsync(cacheKeys).ConfigureAwait(false);
    }

    public void ClearAll()
    {
        _redisDb.Execute(FlushDb);
        FlushLocalCaches();
    }

    public async Task ClearAllAsync()
    {
        await _redisDb.ExecuteAsync(FlushDb);
        await FlushLocalCachesAsync();
    }

    public void FlushLocalCaches()
    {
        ClearLocalMemory();
        PublishBus(ClearAllKey);
    }

    public async Task FlushLocalCachesAsync()
    {
        ClearLocalMemory();
        await PublishBusAsync(ClearAllKey);
    }

    private void ClearLocalMemory()
    {
        lock (_memoryCache)
        {
            _memoryCache.Dispose();
            CreateLocalCache();
            LogMessage($"clear all local cache");
        }
    }

    private string GetCacheKey(string key) => $"{_options.InstancesSharedName}:{key}";

    private async Task PublishBusAsync(params string[] cacheKeys)
    {
        cacheKeys.NotNullAndCountGTZero(nameof(cacheKeys));

        try
        {
            // include the instance ID in the pub/sub message payload to update another instances
            var message = new CacheInvalidationMessage(_instanceId, cacheKeys);
            await _redisDb.PublishAsync(InvalidationChannel, message.Serialize(), CommandFlags.FireAndForget).ConfigureAwait(false);
        }
        catch
        {
            // Retry to publish message
            if (retryPublishCounter++ < _options.ConnectRetry)
            {
                await Task.Delay(exponentialRetryMilliseconds * retryPublishCounter).ConfigureAwait(false);
                await PublishBusAsync(cacheKeys).ConfigureAwait(false);
            }
        }
    }
    private void PublishBus(params string[] cacheKeys)
    {
        cacheKeys.NotNullAndCountGTZero(nameof(cacheKeys));

        try
        {
            // include the instance ID in the pub/sub message payload to update another instances
            var message = new CacheInvalidationMessage(_instanceId, cacheKeys);
            _redisDb.Publish(InvalidationChannel, message.Serialize(), CommandFlags.FireAndForget);
        }
        catch
        {
            // Retry to publish message
            if (retryPublishCounter++ < _options.ConnectRetry)
            {
                Thread.Sleep(exponentialRetryMilliseconds * retryPublishCounter);
                PublishBus(cacheKeys);
            }
        }
    }

    public TimeSpan GetExpiration(string cacheKey)
    {
        cacheKey.NotNullOrWhiteSpace(nameof(cacheKey));

        try
        {
            var time = _redisDb.KeyExpireTime(GetCacheKey(cacheKey));
            return time.ToTimeSpan();
        }
        catch
        {
            return _options.DefaultDistributedExpirationTime;
        }
    }

    public async Task<TimeSpan> GetExpirationAsync(string cacheKey)
    {
        cacheKey.NotNullOrWhiteSpace(nameof(cacheKey));

        try
        {
            var time = await _redisDb.KeyExpireTimeAsync(GetCacheKey(cacheKey));
            return time.ToTimeSpan();
        }
        catch
        {
            return TimeSpan.Zero;
        }
    }

    private void SetExpiryTimes(ref TimeSpan? localExpiry, ref TimeSpan? redisExpiry)
    {
        localExpiry ??= _options.DefaultLocalExpirationTime;
        redisExpiry ??= _options.DefaultDistributedExpirationTime;
    }

    /// <summary>
    /// Logs the message.
    /// </summary>
    /// <param name="message">Message.</param>
    /// <param name="ex">Ex.</param>
    private void LogMessage(string message, Exception ex = null)
    {
        if (_options.EnableLogging && _logger is not null)
        {
            if (ex is null)
            {
                _logger.LogInformation(message);
            }
            else
            {
                _logger.LogError(ex, message);
            }
        }
    }

    public void Dispose()
    {
        _redisSubscriber?.UnsubscribeAll();
        _redisDb?.Multiplexer?.Dispose();
        _memoryCache?.Dispose();
    }
}