using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using System.Diagnostics;
using System.Net;
using System.Runtime.CompilerServices;

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
    private readonly RedisChannel _invalidationChannel;

    private IMemoryCache _memoryCache;
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
        redisConfig.AsyncTimeout = option.AsyncTimeout;
        redisConfig.SyncTimeout = option.SyncTimeout;
        redisConfig.ConnectTimeout = option.ConnectionTimeout;
        redisConfig.KeepAlive = option.KeepAlive;
        redisConfig.AllowAdmin = option.AllowAdmin;
        new ConfigurationOptions();
        var redis = ConnectionMultiplexer.Connect(redisConfig);

        _redisDb = redis.GetDatabase();
        _redisSubscriber = redis.GetSubscriber();
        _logger = loggerFactory?.CreateLogger(nameof(HybridCache));
        _invalidationChannel = new RedisChannel(_options.InstancesSharedName + ":invalidate", RedisChannel.PatternMode.Literal);

        // Subscribe to Redis key-space events to invalidate cache entries on all instances
        _redisSubscriber.Subscribe(_invalidationChannel, OnMessage, CommandFlags.FireAndForget);
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

    public void Set<T>(string key, T value, TimeSpan? localExpiry = null, TimeSpan? redisExpiry = null, bool fireAndForget = true)
    {
        Set(key, value, localExpiry, redisExpiry, fireAndForget, true, true);
    }

    public void Set<T>(string key, T value, HybridCacheEntry cacheEntry)
    {
        Set(key, value, cacheEntry.LocalExpiry, cacheEntry.RedisExpiry, cacheEntry.FireAndForget, cacheEntry.LocalCacheEnable, cacheEntry.RedisCacheEnable);
    }

    private void Set<T>(string key, T value, TimeSpan? localExpiry, TimeSpan? redisExpiry, bool fireAndForget, bool localCacheEnable, bool redisCacheEnable)
    {
        key.NotNullOrWhiteSpace(nameof(key));
        SetExpiryTimes(ref localExpiry, ref redisExpiry);
        var cacheKey = GetCacheKey(key);
        if (localCacheEnable)
            _memoryCache.Set(cacheKey, value, localExpiry.Value);

        try
        {
            if (redisCacheEnable)
                _redisDb.StringSet(cacheKey, value.Serialize(), redisExpiry.Value, flags: GetCommandFlags(fireAndForget));
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

    public Task SetAsync<T>(string key, T value, TimeSpan? localExpiry = null, TimeSpan? redisExpiry = null, bool fireAndForget = true)
    {
        return SetAsync(key, value, localExpiry, redisExpiry, fireAndForget, true, true);
    }

    public Task SetAsync<T>(string key, T value, HybridCacheEntry cacheEntry)
    {
        return SetAsync(key, value, cacheEntry.LocalExpiry, cacheEntry.RedisExpiry, cacheEntry.FireAndForget, cacheEntry.LocalCacheEnable, cacheEntry.RedisCacheEnable);
    }

    private async Task SetAsync<T>(string key, T value, TimeSpan? localExpiry, TimeSpan? redisExpiry, bool fireAndForget, bool localCacheEnable, bool redisCacheEnable)
    {
        key.NotNullOrWhiteSpace(nameof(key));
        SetExpiryTimes(ref localExpiry, ref redisExpiry);
        var cacheKey = GetCacheKey(key);
        if (localCacheEnable)
            _memoryCache.Set(cacheKey, value, localExpiry.Value);

        try
        {
            if (redisCacheEnable)
                await _redisDb.StringSetAsync(cacheKey, value.Serialize(), redisExpiry.Value,
                        flags: GetCommandFlags(fireAndForget)).ConfigureAwait(false);
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

    public void SetAll<T>(IDictionary<string, T> value, TimeSpan? localExpiry = null, TimeSpan? redisExpiry = null, bool fireAndForget = true)
    {
        SetAll(value, localExpiry, redisExpiry, fireAndForget, true, true);
    }

    public void SetAll<T>(IDictionary<string, T> value, HybridCacheEntry cacheEntry)
    {
        SetAll(value, cacheEntry.LocalExpiry, cacheEntry.RedisExpiry, cacheEntry.FireAndForget, cacheEntry.LocalCacheEnable, cacheEntry.RedisCacheEnable);
    }

    private void SetAll<T>(IDictionary<string, T> value, TimeSpan? localExpiry, TimeSpan? redisExpiry, bool fireAndForget, bool localCacheEnable, bool redisCacheEnable)
    {
        value.NotNullAndCountGTZero(nameof(value));
        SetExpiryTimes(ref localExpiry, ref redisExpiry);

        foreach (var kvp in value)
        {
            var cacheKey = GetCacheKey(kvp.Key);
            if (localCacheEnable)
                _memoryCache.Set(cacheKey, kvp.Value, localExpiry.Value);

            try
            {
                if (redisCacheEnable)
                    _redisDb.StringSet(cacheKey, kvp.Value.Serialize(), redisExpiry.Value,
                         flags: GetCommandFlags(fireAndForget));
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

    public Task SetAllAsync<T>(IDictionary<string, T> value, TimeSpan? localExpiry = null, TimeSpan? redisExpiry = null, bool fireAndForget = true)
    {
        return SetAllAsync(value, localExpiry, redisExpiry, fireAndForget, true, true);
    }

    public Task SetAllAsync<T>(IDictionary<string, T> value, HybridCacheEntry cacheEntry)
    {
        return SetAllAsync(value, cacheEntry.LocalExpiry, cacheEntry.RedisExpiry, cacheEntry.FireAndForget, cacheEntry.LocalCacheEnable, cacheEntry.RedisCacheEnable);
    }

    private async Task SetAllAsync<T>(IDictionary<string, T> value, TimeSpan? localExpiry, TimeSpan? redisExpiry, bool fireAndForget, bool localCacheEnable, bool redisCacheEnable)
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
                     flags: GetCommandFlags(fireAndForget)).ConfigureAwait(false);
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

            if (expiry.HasValue)
                _memoryCache.Set(cacheKey, value, expiry.Value);
            return value;
        }

        LogMessage($"distributed cache can not get the value of `{key}` key");
        return value;
    }

    public T Get<T>(string key, Func<T> dataRetriever, TimeSpan? localExpiry = null, TimeSpan? redisExpiry = null, bool fireAndForget = true)
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
            value = dataRetriever();
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

    public async Task<T> GetAsync<T>(string key)
    {
        key.NotNullOrWhiteSpace(nameof(key));
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

        if (value != null)
        {
            var expiry = await GetExpirationAsync(key);


            if (expiry.HasValue)
                _memoryCache.Set(cacheKey, value, expiry.Value);

            return value;
        }

        LogMessage($"distributed cache can not get the value of `{key}` key");
        return value;
    }

    public async Task<T> GetAsync<T>(string key, Func<Task<T>> dataRetriever,
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
            value = await dataRetriever();
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

            if (expiry.HasValue)
                _memoryCache.Set(cacheKey, value, expiry.Value);
            return true;
        }

        LogMessage($"distributed cache can not get the value of `{key}` key");
        return false;
    }

    public void Remove(string key, bool fireAndForget = false)
    {
        Remove(new[] { key }, fireAndForget);
    }

    public void Remove(string[] keys, bool fireAndForget = false)
    {
        keys.NotNullAndCountGTZero(nameof(keys));
        var cacheKeys = Array.ConvertAll(keys, GetCacheKey);
        try
        {
            // distributed cache at first
            foreach (var cacheKey in cacheKeys)
            {
                _redisDb.KeyDelete(cacheKey,
                    flags: GetCommandFlags(fireAndForget));
            }
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

    public Task RemoveAsync(string key, bool fireAndForget = false)
    {
        return RemoveAsync(new[] { key }, fireAndForget);
    }

    public async Task RemoveAsync(string[] keys, bool fireAndForget = false)
    {
        keys.NotNullAndCountGTZero(nameof(keys));
        var cacheKeys = Array.ConvertAll(keys, GetCacheKey);
        try
        {
            foreach (var cacheKey in cacheKeys)
            {
                await _redisDb.KeyDeleteAsync(cacheKey,
                    flags: GetCommandFlags(fireAndForget)).ConfigureAwait(false);
            }

            // distributed cache at first

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

    public async Task<string[]> RemoveWithPatternAsync(string pattern, bool fireAndForget = false, CancellationToken token = default)
    {
        pattern.NotNullAndCountGTZero(nameof(pattern));
        var removedKeys = new List<string>();
        var keyPattern = "*" + GetCacheKey(pattern);
        if (keyPattern.EndsWith("*") == false)
            keyPattern += "*";

        try
        {
            await foreach (var key in GetKeysAsync(keyPattern, token).ConfigureAwait(false))
            {
                if (token.IsCancellationRequested)
                    break;

                if (await _redisDb.KeyDeleteAsync(key).ConfigureAwait(false))
                {
                    removedKeys.Add(key);
                }
            }
            LogMessage($"{removedKeys.Count} matching keys found and removed with `{keyPattern}` pattern");
        }
        catch (Exception ex)
        {
            LogMessage($"remove cache key [{string.Join(" | ", removedKeys)}] error", ex);

            if (_options.ThrowIfDistributedCacheError)
            {
                throw;
            }
        }

        var keys = removedKeys.ToArray();
        Array.ForEach(keys, _memoryCache.Remove);

        // send message to bus 
        await PublishBusAsync(keys).ConfigureAwait(false);
        return keys;
    }

    public void ClearAll()
    {
        var servers = GetServers();

        foreach (var server in servers)
        {
            if (server.IsConnected)
                server.Execute(FlushDb);
        }

        FlushLocalCaches();
    }

    public async Task ClearAllAsync(bool fireAndForget = false)
    {
        var servers = GetServers();
        foreach (var server in servers)
        {
            await FlushServer(server, fireAndForget);
        }

        await FlushLocalCachesAsync();
    }

    public async Task<TimeSpan> PingAsync()
    {
        var stopWatch = Stopwatch.StartNew();
        var servers = GetServers();
        foreach (var server in servers)
        {
            if (server.ServerType == ServerType.Cluster)
            {
                var clusterInfo = await server.ExecuteAsync("CLUSTER", "INFO").ConfigureAwait(false);
                if (clusterInfo is object && !clusterInfo.IsNull)
                {
                    if (!clusterInfo.ToString()!.Contains("cluster_state:ok"))
                    {
                        // cluster info is not ok!
                        throw new RedisException($"INFO CLUSTER is not on OK state for endpoint {server.EndPoint}");
                    }
                }
                else
                {
                    // cluster info cannot be read for this cluster node
                    throw new RedisException($"INFO CLUSTER is null or can't be read for endpoint {server.EndPoint}");
                }
            }
            else
            {
                await _redisDb.Multiplexer.GetDatabase().PingAsync().ConfigureAwait(false);
                await server.PingAsync().ConfigureAwait(false);
            }
        }
        stopWatch.Stop();
        return stopWatch.Elapsed;
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
            await _redisDb.PublishAsync(_invalidationChannel, message.Serialize(), CommandFlags.FireAndForget).ConfigureAwait(false);
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
            _redisDb.Publish(_invalidationChannel, message.Serialize(), CommandFlags.FireAndForget);
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

    public TimeSpan? GetExpiration(string cacheKey)
    {
        cacheKey.NotNullOrWhiteSpace(nameof(cacheKey));

        try
        {
            var time = _redisDb.KeyExpireTime(GetCacheKey(cacheKey));
            return time.ToTimeSpan();
        }
        catch
        {
            return null;
        }
    }

    public async Task<TimeSpan?> GetExpirationAsync(string cacheKey)
    {
        cacheKey.NotNullOrWhiteSpace(nameof(cacheKey));

        try
        {
            var time = await _redisDb.KeyExpireTimeAsync(GetCacheKey(cacheKey));
            return time.ToTimeSpan();
        }
        catch
        {
            return null;
        }
    }

    public async IAsyncEnumerable<string> KeysAsync(string pattern, [EnumeratorCancellation] CancellationToken token = default)
    {
        await foreach (var key in GetKeysAsync(pattern, token).ConfigureAwait(false))
        {
            yield return key;
        }
    }

    private async IAsyncEnumerable<RedisKey> GetKeysAsync(string pattern, [EnumeratorCancellation] CancellationToken token = default)
    {
        var servers = GetServers();
        foreach (var server in servers)
        {
            // it would be *better* to try and find a single replica per
            // primary and run the SCAN on the replica, but... let's
            // keep it relatively simple
            if (server.IsConnected && !server.IsReplica)
            {
                if (token.IsCancellationRequested)
                    break;

                await foreach (var key in server.KeysAsync(pattern: pattern).ConfigureAwait(false))
                {
                    if (token.IsCancellationRequested)
                        break;

                    yield return key;
                }
            }
        }
    }

    private void SetExpiryTimes(ref TimeSpan? localExpiry, ref TimeSpan? redisExpiry)
    {
        localExpiry ??= _options.DefaultLocalExpirationTime;
        redisExpiry ??= _options.DefaultDistributedExpirationTime;
    }

    private IServer[] GetServers()
    {
        // there may be multiple endpoints behind a multiplexer
        var endpoints = _redisDb.Multiplexer.GetEndPoints(configuredOnly: true);

        // SCAN is on the server API per endpoint
        return endpoints.Select(ep => _redisDb.Multiplexer.GetServer(ep)).ToArray();
    }

    private async Task FlushServer(IServer server, bool fireAndForget = true)
    {
        if (server.IsConnected)
        {
            await server.ExecuteAsync(FlushDb, args: default, flags: GetCommandFlags(fireAndForget)).ConfigureAwait(false);
        }
    }

    private CommandFlags GetCommandFlags(bool fireAndForget)
    {
        return fireAndForget ? CommandFlags.FireAndForget : CommandFlags.None;
    }

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