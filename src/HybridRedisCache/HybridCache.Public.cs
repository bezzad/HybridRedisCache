﻿namespace HybridRedisCache;

/// <summary>
/// The HybridCache class provides a hybrid caching solution that stores cached items in both
/// an in-memory cache and a Redis cache. 
/// </summary>
public partial class HybridCache
{
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
        var inserted = true;

        try
        {
            if (redisCacheEnable)
            {
                inserted = _redisDb.StringSet(cacheKey, value.Serialize(),
                    keepTtl ? null : redisExpiry,
                    keepTtl, when: (When)when, flags: (CommandFlags)flags);
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

        // Wait to Redis set operation to be completed
        // KeySpace sent a signal and removed local cache
        // So, now we can set the local cache
        if (inserted && localCacheEnable)
            inserted = SetLocalMemory(cacheKey, value, localExpiry, when);

        if (inserted && _options.SupportOldInvalidateBus)
            PublishBus(MessageType.SetCache, cacheKey);

        return inserted;
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
        var inserted = true;

        try
        {
            if (redisCacheEnable)
            {
                inserted = await _redisDb.StringSetAsync(cacheKey, value.Serialize(),
                    keepTtl ? null : redisExpiry,
                    keepTtl, when: (When)when, flags: (CommandFlags)flags).ConfigureAwait(false);
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

        // Wait to Redis set operation to be completed
        // KeySpace sent a signal and removed local cache
        // So, now we can set the local cache
        if (inserted && localCacheEnable)
            inserted = SetLocalMemory(cacheKey, value, localExpiry, redisCacheEnable ? Condition.Always : when);

        if (inserted && _options.SupportOldInvalidateBus)
            await PublishBusAsync(MessageType.SetCache, cacheKey);

        return inserted;
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
        value.NotNullAndCountGtZero(nameof(value));
        SetExpiryTimes(ref localExpiry, ref redisExpiry);
        var result = true;

        foreach (var kvp in value)
        {
            var inserted = true;
            var cacheKey = GetCacheKey(kvp.Key);

            try
            {
                if (redisCacheEnable)
                {
                    inserted = _redisDb.StringSet(cacheKey, kvp.Value.Serialize(),
                        keepTtl ? null : redisExpiry,
                        keepTtl, when: (When)when, flags: (CommandFlags)flags);
                }
            }
            catch (Exception ex)
            {
                LogMessage($"set cache key [{kvp.Key}] error", ex);

                if (_options.ThrowIfDistributedCacheError)
                {
                    throw;
                }

                inserted = false;
            }

            // Wait to Redis set operation to be completed
            // KeySpace sent a signal and removed local cache
            // So, now we can set the local cache
            if (inserted && localCacheEnable)
                inserted = SetLocalMemory(cacheKey, value, localExpiry, redisCacheEnable ? Condition.Always : when);

            if (inserted && _options.SupportOldInvalidateBus)
                PublishBus(MessageType.SetCache, cacheKey);

            result &= inserted;
        }

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
        value.NotNullAndCountGtZero(nameof(value));
        SetExpiryTimes(ref localExpiry, ref redisExpiry);
        var result = true;

        foreach (var kvp in value)
        {
            var inserted = true;
            var cacheKey = GetCacheKey(kvp.Key);

            try
            {
                if (redisCacheEnable)
                {
                    inserted = await _redisDb.StringSetAsync(cacheKey, kvp.Value.Serialize(),
                        keepTtl ? null : redisExpiry,
                        keepTtl, when: (When)when, flags: (CommandFlags)flags).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                LogMessage($"set cache key [{kvp.Key}] error", ex);

                if (_options.ThrowIfDistributedCacheError)
                {
                    throw;
                }

                inserted = false;
            }

            // Wait to Redis set operation to be completed
            // KeySpace sent a signal and removed local cache
            // So, now we can set the local cache
            if (inserted && localCacheEnable)
                inserted = SetLocalMemory(cacheKey, value, localExpiry, redisCacheEnable ? Condition.Always : when);

            if (inserted && _options.SupportOldInvalidateBus)
                await PublishBusAsync(MessageType.SetCache, cacheKey);

            result &= inserted;
        }

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

        LogMessage($"distributed cache can not get the value of key[{key}]");
        activity?.SetCacheHitActivity(CacheResultType.Miss, cacheKey);
        return value;
    }

    public T Get<T>(string key, Func<string, T> dataRetriever, HybridCacheEntry cacheEntry)
    {
        return Get(key, dataRetriever, cacheEntry.LocalExpiry, cacheEntry.RedisExpiry, cacheEntry.Flags);
    }

    public T Get<T>(string key, Func<string, T> dataRetriever, TimeSpan? localExpiry = null,
        TimeSpan? redisExpiry = null, Flags flags = Flags.PreferMaster)
    {
        if (TryGetValue(key, out T value)) return value;

        using var activity = PopulateActivity(OperationTypes.SetCacheWithDataRetriever);
        key.NotNullOrWhiteSpace(nameof(key));
        SetExpiryTimes(ref localExpiry, ref redisExpiry);
        var cacheKey = GetCacheKey(key);

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
            LogMessage($"get with data retriever for key [{key}] error", ex);
            if (_options.ThrowIfDistributedCacheError)
                throw;
        }

        LogMessage($"distributed cache can not get the value of key[{key}]. Data retriever also had problem.");
        activity?.SetCacheHitActivity(CacheResultType.Miss, cacheKey);
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

        LogMessage($"distributed cache can not get the value of key[{key}].");
        activity?.SetCacheHitActivity(CacheResultType.Miss, cacheKey);
        return value;
    }

    public Task<T> GetAsync<T>(string key, Func<string, Task<T>> dataRetriever, HybridCacheEntry cacheEntry)
    {
        return GetAsync(key, dataRetriever, cacheEntry.LocalExpiry, cacheEntry.RedisExpiry,
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
        var resp = await TryGetValueAsync<T>(key);
        if (resp.success) return resp.value;

        using var activity = PopulateActivity(OperationTypes.SetCacheWithDataRetriever);
        key.NotNullOrWhiteSpace(nameof(key));
        SetExpiryTimes(ref localExpiry, ref redisExpiry);
        var cacheKey = GetCacheKey(key);

        try
        {
            var value = await dataRetriever(key).ConfigureAwait(false);
            if (value is not null)
            {
                await SetAsync(key, value, localExpiry, redisExpiry, flags).ConfigureAwait(false);
                activity?.SetRetrievalStrategyActivity(RetrievalStrategy.DataRetrieverExecution);
                activity?.SetCacheHitActivity(CacheResultType.Miss, cacheKey);
                return value;
            }
        }
        catch (Exception ex)
        {
            LogMessage($"get with data retriever for key [{key}] error", ex);
            if (_options.ThrowIfDistributedCacheError)
                throw;
        }

        LogMessage($"distributed cache can not get the value of key[{key}]. Data retriever also had a problem.");
        activity?.SetCacheHitActivity(CacheResultType.Miss, cacheKey);
        return default;
    }

    public bool TryGetValue<T>(string key, out T value)
    {
        using var activity = PopulateActivity(OperationTypes.GetCache);
        key.NotNullOrWhiteSpace(nameof(key));
        var cacheKey = GetCacheKey(key);
        
        // Try to get the value from the memory cache
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

        LogMessage($"distributed cache can not get the value of key[{key}].");
        activity?.SetCacheHitActivity(CacheResultType.Miss, cacheKey);
        return false;
    }

    public  async ValueTask<(bool success, T value)> TryGetValueAsync<T>(string key)
    {
        using var activity = PopulateActivity(OperationTypes.GetCache);
        key.NotNullOrWhiteSpace(nameof(key));
        var cacheKey = GetCacheKey(key);
        
        // Try to get the value from the memory cache
        if (_memoryCache.TryGetValue(cacheKey, out T value))
        {
            activity?.SetRetrievalStrategyActivity(RetrievalStrategy.MemoryCache);
            activity?.SetCacheHitActivity(CacheResultType.Hit, cacheKey);
            return (true, value);
        }

        try
        {
            var redisValue = await _redisDb.StringGetWithExpiryAsync(cacheKey).ConfigureAwait(false);
            if (TryUpdateLocalCache(cacheKey, redisValue, null, out value))
            {
                activity?.SetRetrievalStrategyActivity(RetrievalStrategy.RedisCache);
                activity?.SetCacheHitActivity(CacheResultType.Hit, cacheKey);
                return (true, value);
            }
        }
        catch (Exception ex)
        {
            LogMessage($"Redis cache get error, [{key}]", ex);
            if (_options.ThrowIfDistributedCacheError)
                throw;
        }

        LogMessage($"distributed cache can not get the value of key[{key}].");
        activity?.SetCacheHitActivity(CacheResultType.Miss, cacheKey);
        return (false, default);
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
        keys.NotNullAndCountGtZero(nameof(keys));
        var cacheKeys = Array.ConvertAll(keys, key => GetCacheKey(key));
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
        keys.NotNullAndCountGtZero(nameof(keys));
        var cacheKeys = Array.ConvertAll(keys, key => GetCacheKey(key));
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
        pattern.NotNullAndCountGtZero(nameof(pattern));
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
            Parallel.ForEach(keys, _memoryCache.Remove);
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
                if (!clusterInfo.IsNull)
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
                // await _redisDb.PingAsync().ConfigureAwait(false);
                await server.PingAsync().ConfigureAwait(false);
            }
        }

        stopWatch.Stop();
        return stopWatch.Elapsed;
    }

    public void FlushLocalCaches()
    {
        ClearLocalMemory();
        PublishBus(MessageType.ClearLocalCache, _instanceId);
    }

    public async Task FlushLocalCachesAsync()
    {
        ClearLocalMemory();
        await PublishBusAsync(MessageType.ClearLocalCache, _instanceId).ConfigureAwait(false);
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

    public async Task<string> SentinelGetMasterAddressByNameAsync(string serviceName, Flags flags = Flags.None)
    {
        using var activity = PopulateActivity(OperationTypes.GetSentinelInfo);
        var servers = GetServers(flags);
        var endpoint = await servers.First().SentinelGetMasterAddressByNameAsync(serviceName, (CommandFlags)flags);
        return endpoint?.ToString();
    }

    public async Task<string[]> SentinelGetSentinelAddressesAsync(string serviceName, Flags flags = Flags.None)
    {
        using var activity = PopulateActivity(OperationTypes.GetSentinelInfo);
        var servers = GetServers(flags);
        var endpoints = await servers.First().SentinelGetSentinelAddressesAsync(serviceName, (CommandFlags)flags);
        return endpoints.Select(ep => ep.ToString()).ToArray();
    }

    public async Task<string[]> SentinelGetReplicaAddressesAsync(string serviceName, Flags flags = Flags.None)
    {
        using var activity = PopulateActivity(OperationTypes.GetSentinelInfo);
        var servers = GetServers(flags);
        var endpoints = await servers.First().SentinelGetReplicaAddressesAsync(serviceName, (CommandFlags)flags);
        return endpoints.Select(ep => ep.ToString()).ToArray();
    }

    public async Task<long> DatabaseSizeAsync(int database = -1, Flags flags = Flags.None)
    {
        using var activity = PopulateActivity(OperationTypes.DatabaseSize);
        var servers = GetServers(flags);
        return await servers.First().DatabaseSizeAsync(flags: (CommandFlags)flags);
    }

    public async Task<string[]> EchoAsync(string message, Flags flags = Flags.None)
    {
        using var activity = PopulateActivity(OperationTypes.Echo);
        var servers = GetServers(flags);
        var echoTasks = servers.Select(server => server.EchoAsync(message, (CommandFlags)flags));
        var results = await Task.WhenAll(echoTasks);
        return results.Select(r => r.ToString()).ToArray();
    }

    public async Task<DateTime> TimeAsync(Flags flags = Flags.None)
    {
        using var activity = PopulateActivity(OperationTypes.GetServerTime);
        var servers = GetServers(flags);
        return await servers.First().TimeAsync(flags: (CommandFlags)flags);
    }

    public Task<bool> TryLockKeyAsync(string key, string token, TimeSpan? expiry = null, Flags flags = Flags.None)
    {
        using var activity = PopulateActivity(OperationTypes.LockKey);
        expiry ??= TimeSpan.MaxValue;
        var cacheKey = GetCacheKey(key, true);
        return _redisDb.LockTakeAsync(cacheKey, token.Serialize(), expiry.Value, (CommandFlags)flags);
    }

    public async Task<RedisLockObject> LockKeyAsync(string key, Flags flags = Flags.None)
    {
        using var activity = PopulateActivity(OperationTypes.LockKeyObject);
        var token = Guid.NewGuid().ToString("N");
        var lockObject = new RedisLockObject(this, key, token);
        var cacheKey = GetCacheKey(key, true);

        while (true)
        {
            // First add TaskCompletionSource to bag and catch incoming lock release signals
            var bag = _lockTasks.GetOrAdd(cacheKey, _ => []);
            var tcs = new TaskCompletionSource();
            bag.Add(tcs);

            if (await _redisDb.LockTakeAsync(cacheKey, token.Serialize(), TimeSpan.MaxValue, (CommandFlags)flags))
            {
                return lockObject;
            }

            // wait until a signal income and release this lock
            using var cts = new CancellationTokenSource(10_000);
            // Register the cancellation to trigger the TCS completion if timeout occurs
            await using (cts.Token.Register(() => tcs.TrySetResult()))
            {
                // Wait for either the signal or timeout
                await tcs.Task;
            }
        }
    }

    public Task<bool> TryExtendLockAsync(string key, string token, TimeSpan? expiry, Flags flags = Flags.None)
    {
        using var activity = PopulateActivity(OperationTypes.ExtendLockKey);
        var cacheKey = GetCacheKey(key, true);
        return _redisDb.LockExtendAsync(cacheKey, token.Serialize(),
            expiry ?? _options.DefaultDistributedExpirationTime, (CommandFlags)flags);
    }

    public async Task<bool> TryReleaseLockAsync(string key, string token, Flags flags = Flags.None)
    {
        using var activity = PopulateActivity(OperationTypes.ReleaseLock);
        var cacheKey = GetCacheKey(key, true);
        if (await _redisDb.LockReleaseAsync(cacheKey, token.Serialize(), (CommandFlags)flags))
        {
            return true;
        }

        return false;
    }

    public bool TryReleaseLock(string key, string token, Flags flags = Flags.None)
    {
        using var activity = PopulateActivity(OperationTypes.ReleaseLock);
        var cacheKey = GetCacheKey(key, true);
        if (_redisDb.LockRelease(cacheKey, token.Serialize(), (CommandFlags)flags))
        {
            return true;
        }

        return false;
    }

    public async Task<long> ValueIncrementAsync(string key, long value = 1, Flags flags = Flags.None)
    {
        using var activity = PopulateActivity(OperationTypes.SetCache);
        var result = await _redisDb.StringIncrementAsync(key, value, (CommandFlags)flags);
        _memoryCache.Set(key, result);
        return result;
    }

    public async Task<double> ValueIncrementAsync(string key, double value, Flags flags = Flags.None)
    {
        using var activity = PopulateActivity(OperationTypes.SetCache);
        var result = await _redisDb.StringIncrementAsync(key, value, (CommandFlags)flags);
        _memoryCache.Set(key, result);
        return result;
    }

    public async Task<long> ValueDecrementAsync(string key, long value = 1, Flags flags = Flags.None)
    {
        using var activity = PopulateActivity(OperationTypes.SetCache);
        var result = await _redisDb.StringDecrementAsync(key, value, (CommandFlags)flags);
        _memoryCache.Set(key, result);
        return result;
    }

    public async Task<double> ValueDecrementAsync(string key, double value, Flags flags = Flags.None)
    {
        using var activity = PopulateActivity(OperationTypes.SetCache);
        var result = await _redisDb.StringDecrementAsync(key, value, (CommandFlags)flags);
        _memoryCache.Set(key, result);
        return result;
    }

    public Version GetServerVersion(Flags flags = Flags.None)
    {
        using var activity = PopulateActivity(OperationTypes.GetServerVersion);
        var servers = GetServers(flags);
        return servers.First().Version;
    }

    public Dictionary<string, string> GetServerFeatures(Flags flags = Flags.None)
    {
        var servers = GetServers(Flags.PreferMaster);
        var features = servers.First().Features;
        var featureList = typeof(RedisFeatures)
            .GetProperties()
            .ToDictionary(x => x.Name, x => x.GetValue(features)?.ToString());
        return featureList;
    }

    public async ValueTask RemoveWithPatternOnRedisAsync(string pattern, Flags flags = Flags.None)
    {
        using var activity = PopulateActivity(OperationTypes.BatchDeleteCache);
        pattern.NotNullOrWhiteSpace(nameof(pattern));
        var cacheKeyPattern = GetCacheKey(pattern);
        LogMessage($"Remove keys by pattern `{pattern}` on Redis server.");

        const string luaScript = @"
        local pattern = ARGV[1]
        local cursor = '0'
        repeat
            -- Perform SCAN with the given pattern and cursor
            local result = redis.call('SCAN', cursor, 'MATCH', pattern, 'COUNT', 1000)
            cursor = result[1]
            local keys = result[2]
            if #keys > 0 then
                redis.call('UNLINK', unpack(keys))
            end        
        until cursor == '0'";

        // Execute the Lua script and get the deleted keys as a RedisResult array
        await _redisDb.ScriptEvaluateAsync(luaScript, values: [cacheKeyPattern],
            flags: (CommandFlags)flags).ConfigureAwait(false);
    }
}