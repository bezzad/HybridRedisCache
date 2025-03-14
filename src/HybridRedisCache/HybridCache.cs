﻿namespace HybridRedisCache;

/// <summary>
/// The HybridCache class provides a hybrid caching solution that stores cached items in both
/// an in-memory cache and a Redis cache. 
/// </summary>
public partial class HybridCache : IHybridCache, IDisposable
{
    private readonly ConcurrentDictionary<string, ConcurrentBag<TaskCompletionSource>> _lockTasks = new();
    private readonly ActivitySource _activity;
    private readonly IDatabase _redisDb;
    private readonly string _instanceId;
    private readonly HybridCachingOptions _options;
    private readonly ISubscriber _redisSubscriber;
    private readonly ILogger _logger;
    private readonly RedisChannel _keySpaceChannel;
    private readonly RedisChannel _invalidateChannel;
    private IMemoryCache _memoryCache;
    private IMemoryCache _recentlySetKeys;
    private readonly TimeSpan _timeWindow = TimeSpan.FromSeconds(5); // Expiration time window
    internal const string LockKeyPrefix = "lock/";

    internal record CacheInvalidationMessage(string InstanceId, params string[] CacheKeys);

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
        _activity = new TracingActivity(option.TracingActivitySourceName).Source;
        CreateLocalCache();
        var redisConfig = ConfigurationOptions.Parse(option.RedisConnectionString, true);
        redisConfig.AbortOnConnectFail = option.AbortOnConnectFail;
        redisConfig.ConnectRetry = option.ConnectRetry;
        redisConfig.ReconnectRetryPolicy = new ExponentialRetry(5000); // defaults maxDeltaBackoff to 10000 ms
        redisConfig.ClientName = option.InstancesSharedName + ":" + _instanceId;
        redisConfig.AsyncTimeout = option.AsyncTimeout;
        redisConfig.SyncTimeout = option.SyncTimeout;
        redisConfig.ConnectTimeout = option.ConnectionTimeout;
        redisConfig.KeepAlive = option.KeepAlive;
        redisConfig.AllowAdmin = option.AllowAdmin;
        redisConfig.SocketManager =
            option.ThreadPoolSocketManagerEnable ? SocketManager.ThreadPool : SocketManager.Shared;
        // redisConfig.LoggerFactory = loggerFactory;
        var redis = ConnectionMultiplexer.Connect(redisConfig);
        redis.ConnectionRestored += OnReconnect;
        _redisDb = redis.GetDatabase();
        _redisSubscriber = redis.GetSubscriber();
        _logger = loggerFactory?.CreateLogger(nameof(HybridCache));

        _invalidateChannel = option.SupportOldInvalidateBus
            ? new RedisChannel(_options.InstancesSharedName + ":invalidate", RedisChannel.PatternMode.Literal)
            : new RedisChannel("__redis__:invalidate", RedisChannel.PatternMode.Literal);

        // Subscribe to Redis key-space events to invalidate cache entries on all instances
        _keySpaceChannel = new RedisChannel($"__keyspace@{_redisDb.Database}__:{option.InstancesSharedName}:*",
            RedisChannel.PatternMode.Pattern);

        _redisSubscriber.Subscribe(_keySpaceChannel, OnMessage, CommandFlags.FireAndForget);
        _redisSubscriber.Subscribe(_invalidateChannel,
            (channel, message) => { LogMessage($"OnInvalidateMessage: {channel}: {message}"); },
            CommandFlags.FireAndForget);

        SetRedisServersConfigs();
    }

    private void SetRedisServersConfigs()
    {
        // _redisDb.Execute("CLIENT", "SETNAME", _options.InstancesSharedName + ":" + _instanceId);
        var clientId = (long)_redisDb.Execute("CLIENT", "ID");

        // Set the notify-keyspace-events configuration
        // Explanation of notify-keyspace-events Flags
        //
        //    K     Keyspace events, published with __keyspace@<db>__ prefix.
        //    E     Keyevent events, published with __keyevent@<db>__ prefix.
        //    g     Generic commands (non-type specific) like DEL, EXPIRE, RENAME, ...
        //    $     String commands
        //    l     List commands
        //    s     Set commands
        //    h     Hash commands
        //    z     Sorted set commands
        //    t     Stream commands
        //    d     Module key type events
        //    x     Expired events (events generated every time a key expires)
        //    e     Evicted events (events generated when a key is evicted for maxmemory)
        //    m     Key miss events (events generated when a key that doesn't exist is accessed)
        //    n     New key events (Note: not included in the 'A' class)
        //    A     Alias for "g$lshztxed", so that the "AKE" string means all the events except "m" and "n".
        // 
        // https://redis.io/docs/latest/develop/use/keyspace-notifications/
        _redisDb.Execute("CONFIG", "SET", "notify-keyspace-events", "KA");

        // Enable tracking with broadcast mode
        // _redisDb.Execute("CLIENT", "TRACKING", "ON", "BCAST");

        // Enable tracking with specific key prefixes to reduce overhead
        _redisDb.Execute($"CLIENT", "TRACKING", "ON", "REDIRECT", clientId, "BCAST", "PREFIX",
            $"{_options.InstancesSharedName}:*", "NOLOOP");

        // Enable CLIENT TRACKING in OPTIN mode
        // _redisDb.Execute("CLIENT", "TRACKING", "ON", "REDIRECT", clientId, "OPTIN");

        // Now you can use CLIENT CACHING YES
        // _redisDb.Execute("CLIENT", "CACHING", "YES");
    }

    private void CreateLocalCache()
    {
        _memoryCache = new MemoryCache(new MemoryCacheOptions());
        _recentlySetKeys = new MemoryCache(new MemoryCacheOptions());
    }

    private Activity PopulateActivity(OperationTypes operationType)
    {
        if (!_options.EnableTracing)
            return null;

        var activity = _activity.StartActivity(nameof(HybridCache));
        activity?.SetTag(nameof(HybridRedisCache) + ".OperationType", operationType.ToString("G"));
        return activity;
    }

    private void OnMessage(RedisChannel channel, RedisValue type)
    {
        // With this implementation, when a key is updated or removed in Redis,
        // all instances of HybridCache that are subscribed to the pub/sub channel will receive a message
        // and invalidate the corresponding key in their local MemoryCache.

        var key = GetChannelKey(channel);

        if (type.Is(MessageType.ExpireKey))
        {
            // ignore the set TTL events
            return;
        }

        if (type.Is(MessageType.SetCache))
        {
            // Check if the key exists in the cache (i.e., was set by this instance)
            if (_recentlySetKeys.TryGetValue(key, out _))
            {
                // The key was set by this instance; ignore the notification
                LogMessage($"{nameof(OnMessage)}: Notification ignored for key: {key}");
                _recentlySetKeys.Remove(key);
                return;
            }

            _memoryCache.Remove(key);
            return;
        }

        if (type.Is(MessageType.RemoveKey) ||
            type.Is(MessageType.ExpiredKey))
        {
            _memoryCache.Remove(key);
            _recentlySetKeys.Remove(key);
            if (key.StartsWith(GetCacheKey(LockKeyPrefix)) &&
                _lockTasks.TryGetValue(key, out var bag))
            {
                while (bag.TryTake(out var tcs))
                {
                    LogMessage($"{nameof(OnMessage)}: Continue to lock the `{key}` key.");
                    tcs.SetResult();
                }

                return;
            }

            if (_options.SupportOldInvalidateBus)
            {
                PublishBus(MessageType.RemoveKey, key);
            }
        }

        if (type.Is(MessageType.ClearLocalCache) &&
            key != GetCacheKey(_instanceId)) // ignore self instance from duplicate clearing
        {
            LogMessage($"{nameof(OnMessage)}: Clearing local cache");
            ClearLocalMemory();
            return;
        }

        return;

        string GetChannelKey(string strChannel)
        {
            var index = strChannel.IndexOf(':');
            return index >= 0 && index < strChannel.Length - 1
                ? strChannel[(index + 1)..]
                : strChannel;
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

    private void ClearLocalMemory()
    {
        using var activity = PopulateActivity(OperationTypes.ClearLocalCache);
        lock (_memoryCache)
        {
            _memoryCache.Dispose();
            _recentlySetKeys.Dispose();
            CreateLocalCache();
            LogMessage($"clear all local cache");
        }
    }

    private string GetCacheKey(string key, bool isLock = false) =>
        $"{_options.InstancesSharedName}:" + (isLock ? LockKeyPrefix : string.Empty) + key;

    private void KeepRecentSetKey(params string[] keys)
    {
        if (keys.Length == 0) return;

        foreach (var key in keys)
        {
            // Add the key to the cache with an expiration policy
            _recentlySetKeys.Set(key, DateTime.UtcNow, _timeWindow);
        }
    }

    private async ValueTask PublishBusAsync(MessageType type, params string[] cacheKeys)
    {
        try
        {
            if (cacheKeys?.Any() != true) return;

            if (_options.SupportOldInvalidateBus)
            {
                // include the instance ID in the pub/sub message payload to update another instances
                // Note: in new version, HybridCache uses the redis keyspace feature to invalidate the local cache
                var message = new CacheInvalidationMessage(_instanceId, cacheKeys);
                await _redisDb.PublishAsync(_invalidateChannel, message.Serialize(), CommandFlags.FireAndForget)
                    .ConfigureAwait(false);
            }

            if (type == MessageType.ClearLocalCache)
            {
                await _redisDb.PublishAsync(_keySpaceChannel.ToString().Replace("*", cacheKeys[0]),
                        type.GetValue(), CommandFlags.FireAndForget)
                    .ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            LogMessage("PublishBusAsync error: " + ex.Message);
        }
    }

    private void PublishBus(MessageType type, params string[] cacheKeys)
    {
        try
        {
            if (cacheKeys?.Any() != true) return;

            if (_options.SupportOldInvalidateBus)
            {
                // include the instance ID in the pub/sub message payload to update another instances
                // Note: in new version, HybridCache uses the redis keyspace feature to invalidate the local cache
                var message = new CacheInvalidationMessage(_instanceId, cacheKeys);
                _redisDb.Publish(_invalidateChannel, message.Serialize(), CommandFlags.FireAndForget);
            }

            if (type == MessageType.ClearLocalCache)
            {
                _redisDb.Publish(_keySpaceChannel.ToString().Replace("*", cacheKeys[0]),
                    type.GetValue(), CommandFlags.FireAndForget);
            }
        }
        catch (Exception ex)
        {
            LogMessage("PublishBusAsync error: " + ex.Message);
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
        KeepRecentSetKey(cacheKey);
        return true;
    }

    private void SetExpiryTimes(ref TimeSpan? localExpiry, ref TimeSpan? redisExpiry)
    {
        localExpiry ??= _options.DefaultLocalExpirationTime;
        redisExpiry ??= _options.DefaultDistributedExpirationTime;
        if (localExpiry.Value > redisExpiry.Value)
            localExpiry = redisExpiry;
    }

    private bool TryUpdateLocalCache<T>(string cacheKey, RedisValueWithExpiry redisValue, TimeSpan? localExpiry,
        out T value)
    {
        value = default;
        if (!redisValue.Expiry.HasValue) return false;
        value = redisValue.Value.ToString().Deserialize<T>();
        if (value is null) return false;

        localExpiry ??= _options.DefaultLocalExpirationTime;
        if (localExpiry > redisValue.Expiry.Value)
            localExpiry = redisValue.Expiry.Value;

        if (localExpiry.Value <= TimeSpan.Zero) return false;

        SetLocalMemory(cacheKey, value, localExpiry.Value, Condition.Always);
        return true;
    }

    private IServer[] GetServers(Flags flags)
    {
        // there may be multiple endpoints behind a multiplexer
        var servers = _redisDb.Multiplexer.GetServers(); //.GetEndPoints(configuredOnly: true);

        if (flags.HasFlag(Flags.PreferReplica) && servers.Any(s => s.IsConnected && s.IsReplica))
            return servers.Where(s => s.IsReplica).ToArray();

        if (flags.HasFlag(Flags.PreferMaster))
            return servers.Where(s => s.IsConnected && !s.IsReplica).ToArray();

        return servers.Where(s => s.IsConnected).ToArray();
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
        _redisDb?.Multiplexer.Dispose();
        _memoryCache?.Dispose();
    }
}