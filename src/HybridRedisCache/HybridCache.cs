namespace HybridRedisCache;

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
    private const int ExponentialRetryMilliseconds = 100;
    private IMemoryCache _memoryCache;
    private IMemoryCache _recentlySetKeys;
    private readonly TimeSpan _timeWindow = TimeSpan.FromSeconds(5); // Expiration time window

    private int _retryPublishCounter;
    internal const string LockKeyPrefix = "lock/";

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
        SetRedisServersConfigs();
        _keySpaceChannel = new RedisChannel($"__keyspace@{_redisDb.Database}__:{option.InstancesSharedName}:*",
            RedisChannel.PatternMode.Pattern);

        // Subscribe to Redis key-space events to invalidate cache entries on all instances
        _redisSubscriber.Subscribe(_keySpaceChannel, OnMessage, CommandFlags.FireAndForget);
        redis.ConnectionRestored += OnReconnect;
    }

    private void SetRedisServersConfigs()
    {
        var servers = GetServers(Flags.PreferMaster);
        foreach (var server in servers)
        {
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
            server.ConfigSet("notify-keyspace-events", "KA");
        }
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
        LogMessage($"{nameof(OnMessage)}: {type} => {key}");

        if (type.Is(MessageType.SetKey))
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
        }
        else if (type.Is(MessageType.RemoveKey) ||
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
            }
        }
        else if (type.Is(MessageType.ClearLocalCache) &&
                 key != GetCacheKey(_instanceId)) // ignore self instance from duplicate clearing
        {
            ClearLocalMemory();
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

    private async Task PublishBusAsync(MessageType type, string key)
    {
        try
        {
            await _redisDb.PublishAsync(_keySpaceChannel.ToString().Replace("*", key),
                    type.GetValue(), CommandFlags.FireAndForget)
                .ConfigureAwait(false);
        }
        catch
        {
            // Retry to publish message
            if (_retryPublishCounter++ < _options.ConnectRetry)
            {
                await Task.Delay(ExponentialRetryMilliseconds * _retryPublishCounter).ConfigureAwait(false);
                await PublishBusAsync(type, key).ConfigureAwait(false);
            }
        }
    }

    private void PublishBus(MessageType type, string key)
    {
        try
        {
            // include the instance ID in the pub/sub message payload to update another instances
            _redisDb.Publish(_keySpaceChannel.ToString().Replace("*", key),
                type.GetValue(), CommandFlags.FireAndForget);
        }
        catch
        {
            // Retry to publish message
            if (_retryPublishCounter++ < _options.ConnectRetry)
            {
                Thread.Sleep(ExponentialRetryMilliseconds * _retryPublishCounter);
                PublishBus(type, key);
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