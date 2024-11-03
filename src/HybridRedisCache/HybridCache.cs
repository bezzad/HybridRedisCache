using HybridRedisCache.Utilities;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace HybridRedisCache;

/// <summary>
/// The HybridCache class provides a hybrid caching solution that stores cached items in both
/// an in-memory cache and a Redis cache. 
/// </summary>
public class HybridCache : IHybridCache, IDisposable
{
    private readonly IDatabase _redisDb;
    private readonly string _instanceId;
    private readonly HybridCachingOptions _options;
    private readonly ISubscriber _redisSubscriber;
    private readonly ILogger _logger;
    private readonly RedisChannel _invalidationChannel;
    private readonly int _exponentialRetryMilliseconds = 100;
    private IMemoryCache _memoryCache;
    private int _retryPublishCounter;
    private string ClearAllKey => GetCacheKey("#CMD:FLUSH_DB#");

    /// <summary>
    /// This method initializes the HybridCache instance and subscribes to Redis key-space events 
    /// to invalidate cache entries on all instances. 
    /// </summary>
    /// <param name="option">Redis connection string and order settings</param>
    /// <param name="loggerFactory">
    /// Microsoft.Extensions.Logging factory object to configure the logging system and
    /// create instances of ILogger.
    /// </param>
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
        redisConfig.SocketManager =
            option.ThreadPoolSocketManagerEnable ? SocketManager.ThreadPool : SocketManager.Shared;
        var redis = ConnectionMultiplexer.Connect(redisConfig);

        _redisDb = redis.GetDatabase();
        _redisSubscriber = redis.GetSubscriber();
        _logger = loggerFactory?.CreateLogger(nameof(HybridCache));
        _invalidationChannel =
            new RedisChannel(_options.InstancesSharedName + ":invalidate", RedisChannel.PatternMode.Literal);

        // Subscribe to Redis key-space events to invalidate cache entries on all instances
        _redisSubscriber.Subscribe(_invalidationChannel, OnMessage, CommandFlags.FireAndForget);
        redis.ConnectionRestored += OnReconnect;
    }

    private void CreateLocalCache()
    {
        _memoryCache = new MemoryCache(new MemoryCacheOptions());
    }

    private Activity PopulateActivity(OperationTypes operationType)
    {
        if (!_options.EnableTracing)
            return null;

        var activity = ActivityInstance.Source.StartActivity(nameof(HybridCache));
        activity?.SetTag(nameof(HybridRedisCache) + ".OperationType", operationType.ToString("G"));
        return activity;
    }

    private void OnMessage(RedisChannel channel, RedisValue value)
    {
        // With this implementation, when a key is updated or removed in Redis,
        // all instances of HybridCache that are subscribed to the pub/sub channel will receive a message
        // and invalidate the corresponding key in their local MemoryCache.

        var message = value.ToString().Deserialize<CacheInvalidationMessage>();

        if (message.InstanceId == _instanceId ||
            !(message.CacheKeys?.Length > 0))
            return; // filter out messages from the current instance

        if (message.CacheKeys.FirstOrDefault()?.Equals(ClearAllKey) == true)
        {
            ClearLocalMemory();
            return;
        }

        foreach (var key in message.CacheKeys)
        {
            _memoryCache.Remove(key);
            LogMessage($"remove local cache with key '{key}'");
        }
    }

    private void OnReconnect(object sender, ConnectionFailedEventArgs e)
    {
        if (_options.FlushLocalCacheOnBusReconnection)
        {
            LogMessage("Flushing local cache due to bus reconnection");
            ClearLocalMemory();
        }

        LogMessage($"Redis reconnected to {e?.EndPoint} endpoint");
    }

    public bool Exists(string key, Flags flags = Flags.PreferMaster)
    {
        using var activity = PopulateActivity(OperationTypes.KeyLookup);
        key.NotNullOrWhiteSpace(nameof(key));
        var cacheKey = GetCacheKey(key);

        // Circuit Breaker may be better
        try
        {
            if (_redisDb.KeyExists(cacheKey, (CommandFlags)flags))
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

    public async Task<bool> ExistsAsync(string key, Flags flags = Flags.PreferMaster)
    {
        using var activity = PopulateActivity(OperationTypes.KeyLookupAsync);
        key.NotNullOrWhiteSpace(nameof(key));
        var cacheKey = GetCacheKey(key);

        // Circuit Breaker may be better
        try
        {
            if (await _redisDb.KeyExistsAsync(cacheKey, (CommandFlags)flags).ConfigureAwait(false))
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

    public bool Set<T>(string key, T value, TimeSpan? localExpiry, TimeSpan? redisExpiry, bool fireAndForget)
    {
        return Set(key, value, localExpiry, redisExpiry, fireAndForget ? Flags.FireAndForget : Flags.PreferMaster);
    }

    public bool Set<T>(string key, T value, HybridCacheEntry cacheEntry)
    {
        return Set(key, value, cacheEntry.LocalExpiry, cacheEntry.RedisExpiry,
            cacheEntry.Flags, cacheEntry.When, cacheEntry.KeepTtl,
            cacheEntry.LocalCacheEnable, cacheEntry.RedisCacheEnable);
    }

    public bool Set<T>(string key, T value, TimeSpan? localExpiry = null, TimeSpan? redisExpiry = null,
        Flags flags = Flags.PreferMaster, Condition when = Condition.Always,
        bool keepTtl = false, bool localCacheEnable = true, bool redisCacheEnable = true)
    {
        using var activity = PopulateActivity(OperationTypes.SetCache);
        key.NotNullOrWhiteSpace(nameof(key));
        SetExpiryTimes(ref localExpiry, ref redisExpiry);
        var cacheKey = GetCacheKey(key);

        // Write Redis just when the local caching is successful
        if (localCacheEnable && !SetLocalMemory(cacheKey, value, localExpiry, when))
            return false;

        try
        {
            if (redisCacheEnable)
                if (_redisDb.StringSet(cacheKey, value.Serialize(),
                        keepTtl ? null : redisExpiry,
                        keepTtl, when: (When)when, flags: (CommandFlags)flags) == false)
                    return false;
        }
        catch (Exception ex)
        {
            LogMessage($"set cache key [{key}] error", ex);

            if (_options.ThrowIfDistributedCacheError)
            {
                throw;
            }

            return false;
        }

        // When create/update cache, send message to bus so that other clients can remove it.
        PublishBus(cacheKey);
        return true;
    }

    public Task<bool> SetAsync<T>(string key, T value, TimeSpan? localExpiry, TimeSpan? redisExpiry, bool fireAndForget)
    {
        return SetAsync(key, value, localExpiry, redisExpiry,
            flags: fireAndForget ? Flags.FireAndForget : Flags.PreferMaster);
    }

    public Task<bool> SetAsync<T>(string key, T value, HybridCacheEntry cacheEntry)
    {
        return SetAsync(key, value, cacheEntry.LocalExpiry, cacheEntry.RedisExpiry,
            cacheEntry.Flags, cacheEntry.When, cacheEntry.KeepTtl,
            cacheEntry.LocalCacheEnable, cacheEntry.RedisCacheEnable);
    }

    public async Task<bool> SetAsync<T>(string key, T value, TimeSpan? localExpiry = null, TimeSpan? redisExpiry = null,
        Flags flags = Flags.PreferMaster, Condition when = Condition.Always,
        bool keepTtl = false, bool localCacheEnable = true, bool redisCacheEnable = true)
    {
        using var activity = PopulateActivity(OperationTypes.SetCache);
        key.NotNullOrWhiteSpace(nameof(key));
        SetExpiryTimes(ref localExpiry, ref redisExpiry);
        var cacheKey = GetCacheKey(key);

        // Write Redis just when the local caching is successful
        if (localCacheEnable && !SetLocalMemory(cacheKey, value, localExpiry, when))
            return false;

        try
        {
            if (redisCacheEnable)
            {
                var result = await _redisDb.StringSetAsync(cacheKey, value.Serialize(),
                    keepTtl ? null : redisExpiry,
                    keepTtl, when: (When)when, flags: (CommandFlags)flags).ConfigureAwait(false);

                if (result == false)
                    return false;
            }
        }
        catch (Exception ex)
        {
            LogMessage($"set cache key [{key}] error", ex);

            if (_options.ThrowIfDistributedCacheError)
            {
                throw;
            }

            return false;
        }

        // When create/update cache, send message to bus so that other clients can remove it.
        await PublishBusAsync(cacheKey).ConfigureAwait(false);
        return true;
    }

    public bool SetAll<T>(IDictionary<string, T> value, TimeSpan? localExpiry, TimeSpan? redisExpiry,
        bool fireAndForget)
    {
        return SetAll(value, localExpiry, redisExpiry, flags: fireAndForget ? Flags.FireAndForget : Flags.PreferMaster);
    }

    public bool SetAll<T>(IDictionary<string, T> value, HybridCacheEntry cacheEntry)
    {
        return SetAll(value, cacheEntry.LocalExpiry, cacheEntry.RedisExpiry,
            cacheEntry.Flags, cacheEntry.When, cacheEntry.KeepTtl,
            cacheEntry.LocalCacheEnable, cacheEntry.RedisCacheEnable);
    }

    public bool SetAll<T>(IDictionary<string, T> value, TimeSpan? localExpiry = null, TimeSpan? redisExpiry = null,
        Flags flags = Flags.PreferMaster, Condition when = Condition.Always,
        bool keepTtl = false, bool localCacheEnable = true, bool redisCacheEnable = true)
    {
        using var activity = PopulateActivity(OperationTypes.SetBatchCache);
        value.NotNullAndCountGTZero(nameof(value));
        SetExpiryTimes(ref localExpiry, ref redisExpiry);
        var result = true;

        foreach (var kvp in value)
        {
            var cacheKey = GetCacheKey(kvp.Key);

            // Write Redis just when the local caching is successful
            if (localCacheEnable && !SetLocalMemory(cacheKey, kvp.Value, localExpiry, when))
            {
                result = false;
                continue;
            }

            try
            {
                if (redisCacheEnable)
                    result &= _redisDb.StringSet(cacheKey, kvp.Value.Serialize(),
                        keepTtl ? null : redisExpiry,
                        keepTtl, when: (When)when, flags: (CommandFlags)flags);
            }
            catch (Exception ex)
            {
                LogMessage($"set cache key [{kvp.Key}] error", ex);

                if (_options.ThrowIfDistributedCacheError)
                {
                    throw;
                }

                return false;
            }
        }

        // send message to bus 
        PublishBus(value.Keys.ToArray());
        return result;
    }

    public Task<bool> SetAllAsync<T>(IDictionary<string, T> value, TimeSpan? localExpiry, TimeSpan? redisExpiry,
        bool fireAndForget)
    {
        return SetAllAsync(value, localExpiry, redisExpiry,
            flags: fireAndForget ? Flags.FireAndForget : Flags.PreferMaster);
    }

    public Task<bool> SetAllAsync<T>(IDictionary<string, T> value, HybridCacheEntry cacheEntry)
    {
        return SetAllAsync(value, cacheEntry.LocalExpiry, cacheEntry.RedisExpiry,
            cacheEntry.Flags, cacheEntry.When, cacheEntry.KeepTtl,
            cacheEntry.LocalCacheEnable, cacheEntry.RedisCacheEnable);
    }

    public async Task<bool> SetAllAsync<T>(IDictionary<string, T> value,
        TimeSpan? localExpiry = null, TimeSpan? redisExpiry = null,
        Flags flags = Flags.PreferMaster, Condition when = Condition.Always,
        bool keepTtl = false, bool localCacheEnable = true, bool redisCacheEnable = true)
    {
        using var activity = PopulateActivity(OperationTypes.SetBatchCache);
        value.NotNullAndCountGTZero(nameof(value));
        SetExpiryTimes(ref localExpiry, ref redisExpiry);
        var result = true;

        foreach (var kvp in value)
        {
            var cacheKey = GetCacheKey(kvp.Key);

            // Write Redis just when the local caching is successful
            if (localCacheEnable && !SetLocalMemory(cacheKey, kvp.Value, localExpiry, when))
            {
                result = false;
                continue;
            }

            try
            {
                if (redisCacheEnable)
                    result &= await _redisDb.StringSetAsync(cacheKey, kvp.Value.Serialize(),
                        keepTtl ? null : redisExpiry,
                        keepTtl, when: (When)when, flags: (CommandFlags)flags).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                LogMessage($"set cache key [{kvp.Key}] error", ex);

                if (_options.ThrowIfDistributedCacheError)
                {
                    throw;
                }

                result = false;
            }
        }

        // send message to bus 
        await PublishBusAsync(value.Keys.ToArray()).ConfigureAwait(false);
        return result;
    }

    public T Get<T>(string key)
    {
        using var activity = PopulateActivity(OperationTypes.GetCache);
        key.NotNullOrWhiteSpace(nameof(key));
        var cacheKey = GetCacheKey(key);
        if (_memoryCache.TryGetValue(cacheKey, out T value))
        {
            activity?.SetRetrievalStrategyActivity(RetrievalStrategy.MemoryCache);
            activity?.SetCacheHitActivity(CacheResultType.Hit, cacheKey);
            return value;
        }

        try
        {
            var redisValue = _redisDb.StringGetWithExpiry(cacheKey);
            if (TryUpdateLocalCache(cacheKey, redisValue, null, out value))
            {
                activity?.SetRetrievalStrategyActivity(RetrievalStrategy.RedisCache);
                activity?.SetCacheHitActivity(CacheResultType.Hit, cacheKey);
                return value;
            }
        }
        catch (Exception ex)
        {
            LogMessage($"Redis cache get error, [{key}]", ex);
            if (_options.ThrowIfDistributedCacheError)
                throw;
        }

        LogMessage($"distributed cache can not get the value of `{key}` key");
        return value;
    }

    public T Get<T>(string key, Func<string, T> dataRetriever, HybridCacheEntry cacheEntry)
    {
        return Get<T>(key, dataRetriever, cacheEntry.LocalExpiry, cacheEntry.RedisExpiry, cacheEntry.Flags);
    }

    public T Get<T>(string key, Func<string, T> dataRetriever, TimeSpan? localExpiry = null,
        TimeSpan? redisExpiry = null, Flags flags = Flags.PreferMaster)
    {
        using var activity = PopulateActivity(OperationTypes.GetCache);
        key.NotNullOrWhiteSpace(nameof(key));
        SetExpiryTimes(ref localExpiry, ref redisExpiry);
        var cacheKey = GetCacheKey(key);

        if (_memoryCache.TryGetValue(cacheKey, out T value))
        {
            activity?.SetRetrievalStrategyActivity(RetrievalStrategy.MemoryCache);
            activity?.SetCacheHitActivity(CacheResultType.Hit, cacheKey);
            return value;
        }

        try
        {
            var redisValue = _redisDb.StringGetWithExpiry(cacheKey, (CommandFlags)flags);
            if (TryUpdateLocalCache(cacheKey, redisValue, localExpiry, out value))
            {
                activity?.SetRetrievalStrategyActivity(RetrievalStrategy.RedisCache);
                activity?.SetCacheHitActivity(CacheResultType.Hit, cacheKey);
                return value;
            }
        }
        catch (Exception ex)
        {
            LogMessage($"Redis cache get error, [{key}]", ex);
            if (_options.ThrowIfDistributedCacheError)
                throw;
        }

        try
        {
            value = dataRetriever(key);
            if (value is not null)
            {
                Set(key, value, localExpiry, redisExpiry, flags);
                activity?.SetRetrievalStrategyActivity(RetrievalStrategy.DataRetrieverExecution);
                activity?.SetCacheHitActivity(CacheResultType.Miss, cacheKey);
                return value;
            }
        }
        catch (Exception ex)
        {
            LogMessage($"get with data retriever error [{key}]", ex);
            if (_options.ThrowIfDistributedCacheError)
                throw;
        }

        LogMessage($"distributed cache can not get the value of `{key}` key. Data retriever also had problem.");
        return value;
    }

    public async Task<T> GetAsync<T>(string key)
    {
        using var activity = PopulateActivity(OperationTypes.GetCache);
        key.NotNullOrWhiteSpace(nameof(key));
        var cacheKey = GetCacheKey(key);
        if (_memoryCache.TryGetValue(cacheKey, out T value))
        {
            activity?.SetRetrievalStrategyActivity(RetrievalStrategy.MemoryCache);
            activity?.SetCacheHitActivity(CacheResultType.Hit, cacheKey);
            return value;
        }

        try
        {
            var redisValue = await _redisDb.StringGetWithExpiryAsync(cacheKey).ConfigureAwait(false);
            if (TryUpdateLocalCache(cacheKey, redisValue, null, out value))
            {
                activity?.SetRetrievalStrategyActivity(RetrievalStrategy.RedisCache);
                activity?.SetCacheHitActivity(CacheResultType.Hit, cacheKey);
                return value;
            }
        }
        catch (Exception ex)
        {
            LogMessage($"Redis cache get error, [{key}]", ex);
            if (_options.ThrowIfDistributedCacheError)
                throw;
        }

        LogMessage($"distributed cache can not get the value of `{key}` key");
        activity?.SetCacheHitActivity(CacheResultType.Miss, cacheKey);
        return value;
    }

    public Task<T> GetAsync<T>(string key, Func<string, Task<T>> dataRetriever, HybridCacheEntry cacheEntry)
    {
        return GetAsync<T>(key, dataRetriever, cacheEntry.LocalExpiry, cacheEntry.RedisExpiry,
            cacheEntry.FireAndForget);
    }

    public Task<T> GetAsync<T>(string key, Func<string, Task<T>> dataRetriever,
        TimeSpan? localExpiry, TimeSpan? redisExpiry, bool fireAndForget)
    {
        return GetAsync(key, dataRetriever, localExpiry, redisExpiry,
            fireAndForget ? Flags.FireAndForget : Flags.PreferMaster);
    }

    public async Task<T> GetAsync<T>(string key, Func<string, Task<T>> dataRetriever,
        TimeSpan? localExpiry = null, TimeSpan? redisExpiry = null, Flags flags = Flags.PreferMaster)
    {
        using var activity = PopulateActivity(OperationTypes.GetCache);
        key.NotNullOrWhiteSpace(nameof(key));
        SetExpiryTimes(ref localExpiry, ref redisExpiry);
        var cacheKey = GetCacheKey(key);

        if (_memoryCache.TryGetValue(cacheKey, out T value))
        {
            activity?.SetRetrievalStrategyActivity(RetrievalStrategy.MemoryCache);
            activity?.SetCacheHitActivity(CacheResultType.Hit, cacheKey);
            return value;
        }

        try
        {
            var redisValue = await _redisDb.StringGetWithExpiryAsync(cacheKey, (CommandFlags)flags)
                .ConfigureAwait(false);
            if (TryUpdateLocalCache(cacheKey, redisValue, localExpiry, out value))
            {
                activity?.SetRetrievalStrategyActivity(RetrievalStrategy.RedisCache);
                activity?.SetCacheHitActivity(CacheResultType.Hit, cacheKey);
                return value;
            }
        }
        catch (Exception ex)
        {
            LogMessage($"Redis cache get error, [{key}]", ex);
            if (_options.ThrowIfDistributedCacheError)
                throw;
        }

        try
        {
            value = await dataRetriever(key).ConfigureAwait(false);
            if (value is not null)
            {
                activity?.SetRetrievalStrategyActivity(RetrievalStrategy.DataRetrieverExecution);
                activity?.SetCacheHitActivity(CacheResultType.Miss, cacheKey);
                await SetAsync(key, value, localExpiry, redisExpiry, flags).ConfigureAwait(false);
                return value;
            }
        }
        catch (Exception ex)
        {
            LogMessage($"get with data retriever error [{key}]", ex);
            if (_options.ThrowIfDistributedCacheError)
                throw;
        }

        LogMessage($"distributed cache can not get the value of `{key}` key. Data retriever also had a problem.");
        return value;
    }

    public bool TryGetValue<T>(string key, out T value)
    {
        using var activity = PopulateActivity(OperationTypes.GetCache);

        key.NotNullOrWhiteSpace(nameof(key));
        var cacheKey = GetCacheKey(key);
        if (_memoryCache.TryGetValue(cacheKey, out value))
        {
            activity?.SetRetrievalStrategyActivity(RetrievalStrategy.MemoryCache);
            activity?.SetCacheHitActivity(CacheResultType.Hit, cacheKey);
            return true;
        }

        try
        {
            var redisValue = _redisDb.StringGetWithExpiry(cacheKey);
            if (TryUpdateLocalCache(cacheKey, redisValue, null, out value))
            {
                activity?.SetRetrievalStrategyActivity(RetrievalStrategy.RedisCache);
                activity?.SetCacheHitActivity(CacheResultType.Hit, cacheKey);
                return true;
            }
        }
        catch (Exception ex)
        {
            LogMessage($"Redis cache get error, [{key}]", ex);
            if (_options.ThrowIfDistributedCacheError)
                throw;
        }

        LogMessage($"distributed cache can not get the value of `{key}` key");
        return false;
    }

    public bool Remove(string key, bool fireAndForget)
    {
        return Remove(key, fireAndForget ? Flags.FireAndForget : Flags.PreferMaster);
    }

    public bool Remove(string key, Flags flags = Flags.PreferMaster)
    {
        using var activity = PopulateActivity(OperationTypes.DeleteCache);
        return Remove(new[] { key }, flags);
    }

    public bool Remove(string[] keys, bool fireAndForget)
    {
        return Remove(keys, fireAndForget ? Flags.FireAndForget : Flags.PreferMaster);
    }

    public bool Remove(string[] keys, Flags flags = Flags.PreferMaster)
    {
        using var activity = PopulateActivity(OperationTypes.BatchDeleteCache);
        keys.NotNullAndCountGTZero(nameof(keys));
        var cacheKeys = Array.ConvertAll(keys, GetCacheKey);
        try
        {
            // distributed cache at first
            if (_redisDb.KeyDelete(cacheKeys.Select(k => (RedisKey)k).ToArray(), flags: (CommandFlags)flags) == 0)
                return false;
        }
        catch (Exception ex)
        {
            LogMessage($"remove cache key [{string.Join(" | ", keys)}] error", ex);

            if (_options.ThrowIfDistributedCacheError)
            {
                throw;
            }

            return false;
        }

        Array.ForEach(cacheKeys, _memoryCache.Remove);

        // send message to bus 
        PublishBus(cacheKeys);
        return true;
    }

    public Task<bool> RemoveAsync(string key, bool fireAndForget)
    {
        return RemoveAsync(key, fireAndForget ? Flags.FireAndForget : Flags.PreferMaster);
    }

    public Task<bool> RemoveAsync(string key, Flags flags = Flags.PreferMaster)
    {
        using var activity = PopulateActivity(OperationTypes.DeleteCache);
        return RemoveAsync(new[] { key }, flags);
    }

    public Task<bool> RemoveAsync(string[] keys, bool fireAndForget)
    {
        return RemoveAsync(keys, fireAndForget ? Flags.FireAndForget : Flags.PreferMaster);
    }

    public async Task<bool> RemoveAsync(string[] keys, Flags flags = Flags.PreferMaster)
    {
        using var activity = PopulateActivity(OperationTypes.BatchDeleteCache);
        keys.NotNullAndCountGTZero(nameof(keys));
        var cacheKeys = Array.ConvertAll(keys, GetCacheKey);
        try
        {
            var result = await _redisDb
                .KeyDeleteAsync(cacheKeys.Select(k => (RedisKey)k).ToArray(), flags: (CommandFlags)flags)
                .ConfigureAwait(false);

            if (result == 0)
                return false;
        }
        catch (Exception ex)
        {
            LogMessage($"remove cache key [{string.Join(" | ", keys)}] error", ex);

            if (_options.ThrowIfDistributedCacheError)
            {
                throw;
            }

            return false;
        }

        Array.ForEach(cacheKeys, _memoryCache.Remove);

        // send message to bus 
        await PublishBusAsync(cacheKeys).ConfigureAwait(false);
        return true;
    }

    public ValueTask<long> RemoveWithPatternAsync(string pattern, bool fireAndForget, CancellationToken token)
    {
        return RemoveWithPatternAsync(pattern, flags: fireAndForget ? Flags.FireAndForget : Flags.PreferMaster,
            token: token);
    }

    public async ValueTask<long> RemoveWithPatternAsync(
        string pattern, Flags flags = Flags.PreferMaster,
        int batchRemovePackSize = 1024, CancellationToken token = default)
    {
        using var activity = PopulateActivity(OperationTypes.RemoveWithPattern);
        pattern.NotNullAndCountGTZero(nameof(pattern));
        var batch = new List<string>(batchRemovePackSize);
        var removedCount = 0L;
        var keyPattern = GetCacheKey(pattern);

        try
        {
            await foreach (var key in KeysAsync(pattern, Flags.PreferReplica, token).ConfigureAwait(false))
            {
                // have match; flush if we've hit the batch size
                batch.Add(key);
                if (batch.Count == batchRemovePackSize)
                    await FlushBatch().ConfigureAwait(false);
            }

            // make sure we flush per-server, so we don't cross shards
            await FlushBatch().ConfigureAwait(false);

            LogMessage($"{batch.Count} matching keys found and removed with `{keyPattern}` pattern");
        }
        catch (Exception ex)
        {
            LogMessage($"remove cache keys with pattern `{keyPattern}` error", ex);

            if (_options.ThrowIfDistributedCacheError)
            {
                throw;
            }
        }

        return removedCount;

        async ValueTask FlushBatch()
        {
            if (batch.Count == 0)
                return;

            var keys = batch.ToArray();
            var redisKeys = batch.Select(key => (RedisKey)key).ToArray();
            removedCount += batch.Count;
            batch.Clear();
            await _redisDb.KeyDeleteAsync(redisKeys, (CommandFlags)flags);
            Array.ForEach(keys, _memoryCache.Remove);

            // send message to bus 
            await PublishBusAsync(keys).ConfigureAwait(false);
        }
    }

    public void ClearAll(Flags flags = Flags.PreferMaster)
    {
        using var activity = PopulateActivity(OperationTypes.Flush);
        var servers = GetServers(flags);
        foreach (var server in servers)
        {
            FlushServer(server, flags);
        }

        FlushLocalCaches();
    }

    public Task ClearAllAsync(bool fireAndForget)
    {
        return ClearAllAsync(fireAndForget ? Flags.FireAndForget : Flags.PreferMaster);
    }

    public async Task ClearAllAsync(Flags flags = Flags.PreferMaster)
    {
        using var activity = PopulateActivity(OperationTypes.Flush);
        var servers = GetServers(flags);
        foreach (var server in servers)
        {
            await FlushServerAsync(server, flags).ConfigureAwait(false);
        }

        await FlushLocalCachesAsync().ConfigureAwait(false);
    }

    public async Task<TimeSpan> PingAsync()
    {
        using var activity = PopulateActivity(OperationTypes.Ping);
        var stopWatch = Stopwatch.StartNew();
        var servers = _redisDb.Multiplexer.GetServers(); // get all servers (connected|disconnected)
        foreach (var server in servers)
        {
            if (server.ServerType == ServerType.Cluster)
            {
                var clusterInfo = await server.ExecuteAsync("CLUSTER", "INFO").ConfigureAwait(false);
                if (clusterInfo is object && !clusterInfo.IsNull)
                {
                    if (!clusterInfo.ToString().Contains("cluster_state:ok"))
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
                await _redisDb.PingAsync().ConfigureAwait(false);
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
        await PublishBusAsync(ClearAllKey).ConfigureAwait(false);
    }

    private void ClearLocalMemory()
    {
        using var activity = PopulateActivity(OperationTypes.ClearLocalCache);
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
            await _redisDb.PublishAsync(_invalidationChannel, message.Serialize(), CommandFlags.FireAndForget)
                .ConfigureAwait(false);
        }
        catch
        {
            // Retry to publish message
            if (_retryPublishCounter++ < _options.ConnectRetry)
            {
                await Task.Delay(_exponentialRetryMilliseconds * _retryPublishCounter).ConfigureAwait(false);
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
            if (_retryPublishCounter++ < _options.ConnectRetry)
            {
                Thread.Sleep(_exponentialRetryMilliseconds * _retryPublishCounter);
                PublishBus(cacheKeys);
            }
        }
    }

    public TimeSpan? GetExpiration(string cacheKey)
    {
        using var activity = PopulateActivity(OperationTypes.GetExpiration);
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
        using var activity = PopulateActivity(OperationTypes.GetExpiration);
        cacheKey.NotNullOrWhiteSpace(nameof(cacheKey));

        try
        {
            var time = await _redisDb.KeyExpireTimeAsync(GetCacheKey(cacheKey)).ConfigureAwait(false);
            return time.ToTimeSpan();
        }
        catch
        {
            return null;
        }
    }

    public async IAsyncEnumerable<string> KeysAsync(string pattern, Flags flags = Flags.PreferReplica,
        [EnumeratorCancellation] CancellationToken token = default)
    {
        using var activity = PopulateActivity(OperationTypes.KeyLookupAsync);
        pattern.NotNullOrWhiteSpace(nameof(pattern));
        var keyPattern = GetCacheKey(pattern);

        // it would be *better* to try and find a single replica per
        // primary and run the SCAN on the replica
        var servers = GetServers(flags);

        foreach (var server in servers)
        {
            await foreach (var key in server.KeysAsync(pattern: keyPattern, flags: (CommandFlags)flags)
                               .WithCancellation(token).ConfigureAwait(false))
            {
                yield return key;
            }
        }
    }

    private bool SetLocalMemory<T>(string cacheKey, T value, TimeSpan? localExpiry, Condition when)
    {
        if (when != Condition.Always)
        {
            var valueIsExist = _memoryCache.TryGetValue(cacheKey, out T _);
            if ((when == Condition.Exists && !valueIsExist) ||
                (when == Condition.NotExists && valueIsExist))
                return false;
        }

        _memoryCache.Set(cacheKey, value, localExpiry ?? _options.DefaultLocalExpirationTime);
        return true;
    }

    private void SetExpiryTimes(ref TimeSpan? localExpiry, ref TimeSpan? redisExpiry)
    {
        localExpiry ??= _options.DefaultLocalExpirationTime;
        redisExpiry ??= _options.DefaultDistributedExpirationTime;
    }

    private bool TryUpdateLocalCache<T>(string cacheKey, RedisValueWithExpiry redisValue, TimeSpan? localExpiry,
        out T value)
    {
        value = default;
        if (redisValue.Expiry.HasValue)
        {
            value = redisValue.Value.ToString().Deserialize<T>();
            if (value is not null)
            {
                localExpiry = localExpiry ?? _options.DefaultLocalExpirationTime;
                if (localExpiry > redisValue.Expiry.Value)
                    localExpiry = redisValue.Expiry.Value;

                _memoryCache.Set(cacheKey, value, localExpiry.Value);
                return true;
            }
        }

        return false;
    }

    private IServer[] GetServers(Flags flags)
    {
        // there may be multiple endpoints behind a multiplexer
        var servers = _redisDb.Multiplexer.GetServers(); //.GetEndPoints(configuredOnly: true);

        if (flags.HasFlag(Flags.PreferReplica) && servers.Any(s => s.IsConnected && s.IsReplica))
            return servers.Where(s => s.IsReplica).ToArray();

        if (flags.HasFlag(Flags.PreferMaster))
            return servers.Where(s =>  s.IsConnected && !s.IsReplica).ToArray();

        return servers.Where(s =>  s.IsConnected).ToArray();
    }

    private async Task FlushServerAsync(IServer server, Flags flags = Flags.PreferMaster)
    {
        if (server.IsConnected)
        {
            // completely wipe ALL keys from database 0
            await server.FlushDatabaseAsync(flags: (CommandFlags)flags)
                .ConfigureAwait(false);
        }
    }

    private void FlushServer(IServer server, Flags flags = Flags.PreferMaster)
    {
        if (server.IsConnected)
        {
            // completely wipe ALL keys from database 0
            server.FlushDatabase(flags: (CommandFlags)flags);
        }
    }

    public async Task<string[]> MemoryStatsAsync(Flags flags = Flags.PreferMaster)
    {
        var result = new List<string>();
        var servers = GetServers(flags);
        var memTasks = servers.Select(server => server.MemoryStatsAsync((CommandFlags)flags));
        var results = await Task.WhenAll(memTasks);
        return results.Select(r => r.ToString()).ToArray();
    }

    public async Task<string> SentinelGetMasterAddressByNameAsync(string serviceName, Flags flags = Flags.None)
    {
        var servers = GetServers(flags);
        var endpoint = await servers.First().SentinelGetMasterAddressByNameAsync(serviceName, (CommandFlags)flags);
        return endpoint?.ToString();
    }

    public async Task<string[]> SentinelGetSentinelAddressesAsync(string serviceName, Flags flags = Flags.None)
    {
        var servers = GetServers(flags);
        var endpoints = await servers.First().SentinelGetSentinelAddressesAsync(serviceName, (CommandFlags)flags);
        return endpoints.Select(ep => ep.ToString()).ToArray();
    }

    public async Task<string[]> SentinelGetReplicaAddressesAsync(string serviceName, Flags flags = Flags.None)
    {
        var servers = GetServers(flags);
        var endpoints = await servers.First().SentinelGetReplicaAddressesAsync(serviceName, (CommandFlags)flags);
        return endpoints.Select(ep => ep.ToString()).ToArray();
    }

    public async Task<long> DatabaseSizeAsync(int database = -1, Flags flags = Flags.None)
    {
        var servers = GetServers(flags);
        return await servers.First().DatabaseSizeAsync(flags: (CommandFlags)flags);
    }

    public async Task<string[]> EchoAsync(string message, Flags flags = Flags.None)
    {
        var servers = GetServers(flags);
        var echoTasks = servers.Select(server => server.EchoAsync(message, (CommandFlags)flags));
        var results = await Task.WhenAll(echoTasks);
        return results.Select(r => r.ToString()).ToArray();
    }

    public async Task<DateTime> TimeAsync(Flags flags = Flags.None)
    {
        var servers = GetServers(flags);
        return await servers.First().TimeAsync(flags: (CommandFlags)flags);
    }

    public Task<bool> LockKeyAsync<T>(string key, T value, TimeSpan? expiry, Flags flags = Flags.None)
    {
        return _redisDb.LockTakeAsync(key, value.Serialize(),
            expiry ?? _options.DefaultDistributedExpirationTime, (CommandFlags)flags);
    }

    public Task<bool>  LockExtendAsync<T>(string key, T value, TimeSpan? expiry, Flags flags = Flags.None)
    {
        return _redisDb.LockExtendAsync(key, value.Serialize(),
            expiry ?? _options.DefaultDistributedExpirationTime, (CommandFlags)flags);
    }

    public Task<bool>  LockReleaseAsync<T>(string key, T value, Flags flags = Flags.None)
    {
        return _redisDb.LockReleaseAsync(key, value.Serialize(), (CommandFlags)flags);
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