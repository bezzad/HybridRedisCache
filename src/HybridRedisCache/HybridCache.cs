namespace HybridRedisCache;

/// <summary>
/// The HybridCache class provides a hybrid caching solution that stores cached items in both
/// an in-memory cache and a Redis cache. 
/// </summary>
public partial class HybridCache : IHybridCache, IDisposable, IAsyncDisposable
{
    private readonly ConcurrentDictionary<string, ConcurrentBag<TaskCompletionSource>> _lockTasks = new();
    private readonly ActivitySource _activity;
    private readonly string _instanceId;
    private readonly SemaphoreSlim _reconnectSemaphore = new(1, 1);
    private readonly HybridCachingOptions _options;
    private readonly ILogger _logger;
    private string _keySpaceChannelName;
    private bool _disposed;
    private ConnectionMultiplexer _connection;
    private IDatabase _redisDb;
    private ISubscriber _redisSubscriber;
    private IMemoryCache _memoryCache;
    private IMemoryCache _recentlySetKeys;
    private int _reconfigureAttemptCount = 0;
    private readonly TimeSpan _timeWindow = TimeSpan.FromSeconds(5); // Expiration time window
    internal const string LockKeyPrefix = "lock/";

    /// <summary>
    /// This method initializes the HybridCache instance and subscribes to Redis key-space events 
    /// to invalidate cache entries on all instances. 
    /// </summary>
    /// <param name="option">Redis connection string and order settings</param>
    /// <param name="loggerFactory">
    /// Microsoft.Extensions.Logging a factory object to configure the logging system and
    /// create instances of ILogger.
    /// </param>
    public HybridCache(HybridCachingOptions option, ILoggerFactory loggerFactory = null)
    {
        option.NotNull(nameof(option));
        _logger = loggerFactory?.CreateLogger(nameof(HybridCache));
        _instanceId = Guid.NewGuid().ToString("N");
        _options = option;
        _activity = new TracingActivity(option.TracingActivitySourceName).Source;

        CreateLocalCache();
        Connect();
    }

    private void Connect()
    {
        _reconnectSemaphore.Wait();
        try
        {
            // TODO: first ping Redis server if doesn't pong then create a new one
            
            if (_connection != null)
            {
                if (_connection.IsConnected)
                {
                    LogMessage("Connected to Redis successfully.");
                    _reconfigureAttemptCount = 0; // reset retry count to use next times
                    return;
                }

                // disconnect all handlers of _connection to create a new one
                _connection.ConnectionRestored -= OnReconnect;
                _connection.ConnectionFailed -= OnConnectionFailed;
                _connection.ErrorMessage -= OnErrorMessage;
                _connection.Dispose();
            }

            LogMessage($"Attempting reconfiguration (Attempt {_reconfigureAttemptCount}).");
            var redisConfig = ConfigurationOptions.Parse(_options.RedisConnectionString, true);
            redisConfig.AbortOnConnectFail = _options.AbortOnConnectFail;
            redisConfig.ConnectRetry = _options.ConnectRetry;
            redisConfig.ReconnectRetryPolicy = new LinearRetry(1000);
            redisConfig.ClientName = _options.InstancesSharedName + ":" + _instanceId;
            redisConfig.AsyncTimeout = _options.AsyncTimeout;
            redisConfig.SyncTimeout = _options.SyncTimeout;
            redisConfig.ConnectTimeout = _options.ConnectionTimeout;
            redisConfig.KeepAlive = _options.KeepAlive;
            redisConfig.AllowAdmin = _options.AllowAdmin;
            redisConfig.SocketManager = _options.ThreadPoolSocketManagerEnable
                ? SocketManager.ThreadPool
                : SocketManager.Shared;

            _connection = ConnectionMultiplexer.Connect(redisConfig);
            if (_connection.IsConnected)
            {
                _connection.ConnectionRestored += OnReconnect;
                _connection.ConnectionFailed += OnConnectionFailed;
                _connection.ErrorMessage += OnErrorMessage;
                _redisDb = _connection.GetDatabase();
                _redisSubscriber = _connection.GetSubscriber();

                // Subscribe to Redis key-space events to invalidate cache entries on all instances
                _keySpaceChannelName = $"__keyspace@{_redisDb.Database}__:{_options.InstancesSharedName}:";
                var keySpaceChannel = GetRedisKeySpaceChannel("*", RedisChannel.PatternMode.Pattern);
                _redisSubscriber.Subscribe(keySpaceChannel, OnMessage, CommandFlags.FireAndForget);

                SetRedisServersConfigs();

                LogMessage($"HybridRedisCache connected to Redis at {string.Join(", ", redisConfig.EndPoints)}");
                _reconfigureAttemptCount = 0; // reset retry count to use next times
                return;
            }

            throw new RedisConnectionException(ConnectionFailureType.UnableToConnect,
                "Attempting to connect Redis failed!");
        }
        catch (Exception ex)
        {
            LogMessage("Failed to connect to Redis.", ex);
            if (!_options.ReconfigureOnConnectFail ||
                (_options.MaxReconfigureAttempts != 0 && // zero is max retry
                 _options.MaxReconfigureAttempts < _reconfigureAttemptCount++))
            {
                LogMessage($"Redis reconfiguration failed after {_reconfigureAttemptCount} attempts. " +
                           "Marking Redis cache as down.", ex);
                throw;
            }
        }
        finally
        {
            _reconnectSemaphore.Release();
        }

        Connect();
    }

    private void OnErrorMessage(object sender, RedisErrorEventArgs e)
    {
        LogMessage("Redis Internal Error: " + e.Message);
    }

    private void SetRedisServersConfigs()
    {
        // _redisDb.Execute("CLIENT", "SETNAME", _options.InstancesSharedName + ":" + _instanceId);
        var clientId = (long)_redisDb.Execute("CLIENT", "ID");

        // Set the notify-keyspace-events configuration
        // Explanation of notify-keyspace-events Flags
        //
        //    K     KeySpace events, published with __keyspace@<db>__ prefix.
        //    E     KeyEvent events, published with __keyevent@<db>__ prefix.
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

        if (_options.EnableRedisClientTracking)
        {
            // Enable tracking with specific key prefixes to reduce overhead
            _redisDb.Execute($"CLIENT", "TRACKING", "ON", "REDIRECT", clientId, "BCAST", "PREFIX",
                $"{_options.InstancesSharedName}:*", "NOLOOP");
        }

        // Now you can use CLIENT CACHING YES
        // _redisDb.Execute("CLIENT", "CACHING", "YES");
    }

    private void OnConnectionFailed(object sender, ConnectionFailedEventArgs e)
    {
        LogMessage($"Redis connection failed ({e.FailureType}) at {e.EndPoint}. ", e.Exception);

        if (!_options.ReconfigureOnConnectFail)
            return;

        Connect();
        
        if (_connection?.IsConnected == true)
            OnReconnect(sender, e);
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

    private void OnMessage(RedisChannel channel, RedisValue val)
    {
        // With this implementation, when a key is updated or removed in Redis,
        // all instances of HybridCache that are subscribed to the pub/sub channel will receive a message
        // and invalidate the corresponding key in their local MemoryCache.
        var strChannel = (string)channel ?? "";
        var index = strChannel.IndexOf(':');
        var key = index >= 0 && index < strChannel.Length - 1
            ? strChannel[(index + 1)..]
            : strChannel;

        try
        {
            if (val.Is(MessageType.ExpireKey))
            {
                // ignore the set TTL events
                return;
            }

            if (val.Is(MessageType.SetCache))
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

            if (val.Is(MessageType.RemoveKey) ||
                val.Is(MessageType.ExpiredKey))
            {
                _memoryCache.Remove(key);
                _recentlySetKeys.Remove(key);
                if (key.StartsWith(GetCacheKey(LockKeyPrefix)) &&
                    _lockTasks.TryGetValue(key, out var bag))
                {
                    while (bag.TryTake(out var tcs))
                    {
                        LogMessage($"{nameof(OnMessage)}: Continue to lock `{key}` key.");
                        tcs.SetResult();
                    }

                    return;
                }
            }

            if (val.Is(MessageType.ClearLocalCache) &&
                key != GetCacheKey(_instanceId)) // ignore self-instance from duplicate clearing
            {
                LogMessage($"{nameof(OnMessage)}: Clearing local cache");
                ClearLocalMemory();
            }
        }
        finally
        {
            OnRedisBusMessage?.Invoke(GetPureCacheKey(key), val.GetMessageType());
        }
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

    private string GetPureCacheKey(string key)
    {
        return key.StartsWith(_options.InstancesSharedName + ":")
            ? key[(_options.InstancesSharedName.Length + 1)..]
            : key;
    }

    private void KeepRecentSetKey(params string[] keys)
    {
        if (keys.Length == 0) return;

        foreach (var key in keys)
        {
            // Add the key to the cache with an expiration policy
            _recentlySetKeys.Set(key, DateTime.UtcNow, _timeWindow);
        }
    }

    private async ValueTask PublishBusAsync(MessageType type, string cacheKey)
    {
        try
        {
            await _redisDb.PublishAsync(GetRedisKeySpaceChannel(cacheKey), type.GetValue(), CommandFlags.FireAndForget)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            LogMessage("PublishBusAsync error: " + ex.Message);
        }
    }

    private void PublishBus(MessageType type, string cacheKey = "")
    {
        try
        {
            _redisDb.Publish(GetRedisKeySpaceChannel(cacheKey), type.GetValue(), CommandFlags.FireAndForget);
        }
        catch (Exception ex)
        {
            LogMessage("PublishBusAsync error: " + ex.Message);
        }
    }

    private RedisChannel GetRedisKeySpaceChannel(string cacheKey,
        RedisChannel.PatternMode patternMode = RedisChannel.PatternMode.Auto)
    {
        // Note: _keySpaceChannelName included with instance shared name
        return new RedisChannel(_keySpaceChannelName + cacheKey, patternMode);
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
        if (_disposed) return;
        _disposed = true;

        _redisSubscriber?.UnsubscribeAll();
        _redisDb?.Multiplexer.Dispose();
        _memoryCache?.Dispose();
        _connection?.Dispose();
        _reconnectSemaphore?.Dispose();

        LogMessage("HybridRedisCache disposed.");
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        _memoryCache?.Dispose();
        _reconnectSemaphore?.Dispose();

        await (_redisSubscriber?.UnsubscribeAllAsync() ?? Task.CompletedTask);
        await (_redisDb?.Multiplexer?.DisposeAsync() ?? ValueTask.CompletedTask);
        await (_connection?.DisposeAsync() ?? ValueTask.CompletedTask);

        LogMessage("HybridRedisCache disposed.");
    }
}