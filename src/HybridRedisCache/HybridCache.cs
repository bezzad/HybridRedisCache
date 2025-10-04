namespace HybridRedisCache;

/// <summary>
/// The HybridCache class provides a hybrid caching solution that stores cached items in both
/// an in-memory cache and a Redis cache. 
/// </summary>
public partial class HybridCache : IHybridCache, IDisposable, IAsyncDisposable
{
    private readonly ConcurrentDictionary<string, TaskCompletionSource> _lockTasks = new();
    private readonly ConcurrentDictionary<string, Func<string, Task>> _dataRetrieverTasks = new();
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
    private int _reconfigureAttemptCount;
    private readonly TimeSpan _timeWindow = TimeSpan.FromSeconds(5); // Expiration time window
    private const string LocalCacheValuePrefix = "#__LEXP__"; // to keep local expiration time in redis value
    private const char LocalCacheValuePostfix = '$'; // to keep local expiration time in redis value
    private readonly KeyMeter _keyMeter;

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
        _logger = loggerFactory?.CreateLogger<HybridCache>();
        _instanceId = Guid.NewGuid().ToString("N");
        _options = option;
        _activity = new TracingActivity(option.TracingActivitySourceName).Source;
        _keyMeter = new KeyMeter(loggerFactory?.CreateLogger<KeyMeter>(), option);

        CreateLocalCache();
        var redisConfig = GetConfigurationOptions();
        Connect(redisConfig);
    }

    private ConfigurationOptions GetConfigurationOptions()
    {
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

        return redisConfig;
    }

    private void Connect(ConfigurationOptions redisConfig)
    {
        if (_connection?.IsConnected == true)
            return;

        // Dispose old connection if exists
        if (_connection != null)
        {
            _connection.ConnectionRestored -= OnReconnect;
            _connection.ConnectionFailed -= OnConnectionFailed;
            _connection.ErrorMessage -= OnErrorMessage;
            _connection.Dispose();
        }

        // Create a new connection 
        _connection = ConnectionMultiplexer.Connect(redisConfig);
        if (!_connection.IsConnected)
        {
            throw new RedisConnectionException(ConnectionFailureType.UnableToConnect,
                "Unable to connect Redis in initializing!");
        }

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

        LogMessage("HybridRedisCache connected and configured at endpoints: " +
                   string.Join(", ", redisConfig.EndPoints));
    }

    private async Task TryConnectAsync()
    {
        while (true)
        {
            await _reconnectSemaphore.WaitAsync().ConfigureAwait(false);
            try
            {
                if (_connection?.IsConnected == true)
                {
                    LogMessage("Redis is already connected.");
                    _reconfigureAttemptCount = 0; // reset retry count to use next times
                    return;
                }

                var redisConfig = GetConfigurationOptions();
                // Ping retry strategy before reconfiguration
                var pingSucceeded = await _redisDb.PingAsync(_options.ConnectRetry).ConfigureAwait(false);
                if (!pingSucceeded)
                {
                    LogMessage($"Redis ping failed after {_options.ConnectRetry} attempts. Proceeding to reconfigure.");
                    Connect(redisConfig);
                    _reconfigureAttemptCount = 0;
                }
            }
            catch (Exception ex)
            {
                LogMessage("Failed to connect to Redis.", ex);
                if (!_options.ReconfigureOnConnectFail || (_options.MaxReconfigureAttempts != 0 && // zero is max retry
                                                           _options.MaxReconfigureAttempts <
                                                           _reconfigureAttemptCount++))
                {
                    LogMessage(
                        $"Redis reconfiguration failed after {_reconfigureAttemptCount} attempts. " +
                        "Marking Redis cache as down.", ex);
                    throw;
                } // else continue the while( true )
            }
            finally
            {
                _reconnectSemaphore.Release();
            }
        }
    }

    private void OnErrorMessage(object sender, RedisErrorEventArgs e)
    {
        LogMessage("Redis Internal Error: " + e.Message);
    }

    private void SetRedisServersConfigs()
    {
        var clientId = _redisDb.Execute("CLIENT", "ID");

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
            _redisDb.Execute($"CLIENT", "TRACKING", "ON", "REDIRECT", (long)clientId, "BCAST", "PREFIX",
                $"{_options.InstancesSharedName}:*", "NOLOOP");
        }
    }

    private async void OnConnectionFailed(object sender, ConnectionFailedEventArgs e)
    {
        try
        {
            LogMessage($"Redis connection failed ({e.FailureType}) at {e.EndPoint}. ", e.Exception);

            // ignore error handling if the user doesn't want to reconfigure connection
            if (!_options.ReconfigureOnConnectFail)
                return;

            await TryConnectAsync().ConfigureAwait(false);

            if (_connection?.IsConnected == true)
                OnReconnect(sender, e);
        }
        catch (Exception exp)
        {
            LogMessage(exp.Message, exp);
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
                if (_lockTasks.TryRemove(key, out var tcs))
                {
                    LogMessage($"{nameof(OnMessage)}: Continue to lock `{key}` key.");
                    tcs.SetResult();
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

    private RedisChannel GetRedisPatternChannel(string channel, string key = "*") => new(channel + ":" + key, RedisChannel.PatternMode.Auto);

    private string GetCacheKey(string key)
    {
        key.NotNullOrWhiteSpace(nameof(key));
        return $"{_options.InstancesSharedName}:" + key;
    }

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

    private bool SetLocalMemory<T>(string cacheKey, T value, TimeSpan? localExpiry, Condition when, bool keepAsRecentSets = true)
    {
        if (when != Condition.Always)
        {
            var valueIsExist = _memoryCache.TryGetValue(cacheKey, out T _);
            if ((when == Condition.Exists && !valueIsExist) ||
                (when == Condition.NotExists && valueIsExist))
                return false;
        }

        _memoryCache.Set(cacheKey, value, localExpiry ?? _options.DefaultLocalExpirationTime);

        if (keepAsRecentSets)
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

    private string SerializeWithExpiryPrefix(string key, object value, TimeSpan? expiry = null)
    {
        var json = value.Serialize();

        if (_options.EnableMeterData)
        {
            // Measure UTF8 byte size (cheap, no allocation)
            var dataSize = System.Text.Encoding.UTF8.GetByteCount(json);
            _keyMeter.RecordHeavyDataUsage(key, dataSize);
        }

        if (expiry is null)
            return json;

        return LocalCacheValuePrefix + expiry.Value.Ticks + LocalCacheValuePostfix + json;
    }

    private bool TryGetMemoryValue<T>(string cacheKey, Activity activity, out T value)
    {
        if (_memoryCache.TryGetValue(cacheKey, out value))
        {
            activity?.SetRetrievalStrategyActivity(RetrievalStrategy.MemoryCache);
            activity?.SetCacheHitActivity(CacheResultType.Hit, cacheKey);
            return true;
        }

        return false;
    }

    private bool TryUpdateRedisValueOnLocalCache<T>(string cacheKey, RedisValueWithExpiry redisValue, bool localCacheEnable, Activity activity, out T value)
    {
        value = default;
        if (!redisValue.Value.HasValue) return false;

        var localExpiry = TimeSpan.Zero;
        var text = redisValue.Value.ToString();
        if (string.IsNullOrEmpty(text)) return false;

        if (redisValue.Expiry.HasValue && text.StartsWith(LocalCacheValuePrefix)) // should be cached in local memory
        {
            var indexOfPostfix = text.IndexOf(LocalCacheValuePostfix, LocalCacheValuePrefix.Length);
            if (indexOfPostfix > LocalCacheValuePrefix.Length)
            {
                var expiry = text.Substring(LocalCacheValuePrefix.Length,
                    indexOfPostfix - LocalCacheValuePrefix.Length);
                text = text.Substring(indexOfPostfix + 1); // delete prefix value of local cache

                localExpiry = long.TryParse(expiry, out var longTime)
                    ? new TimeSpan(longTime)
                    : _options.DefaultLocalExpirationTime;

                if (localExpiry > redisValue.Expiry.Value)
                    localExpiry = redisValue.Expiry.Value;
            }
        }

        value = text.Deserialize<T>();

        if (localExpiry > TimeSpan.Zero && localCacheEnable)
            SetLocalMemory(cacheKey, value, localExpiry, Condition.Always, false);

        activity?.SetRetrievalStrategyActivity(RetrievalStrategy.RedisCache);
        activity?.SetCacheHitActivity(CacheResultType.Hit, cacheKey);

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

    private async Task<T> FetchDataSafely<T>(string key, Func<string, Task<T>> dataRetriever)
    {
        // We don't have multiple calls to data retriever with same key.
        // This is very useful when you have a heavy data retriever function
        // And, you have multiple requests with same key at the same time.
        // This will prevent multiple calls to data retriever function.
        // So, will improve the performance of your application
        var lastRetrieverTask = _dataRetrieverTasks.GetOrAdd(key, dataRetriever) as Func<string, Task<T>>;

        try
        {
            // This is the first request, execute the data retriever
            if (lastRetrieverTask != null)
            {
                return await lastRetrieverTask(key).ConfigureAwait(false);
            }
        }
        finally
        {
            // Remove the task from dictionary after completion to prevent memory leaks
            _dataRetrieverTasks.TryRemove(key, out _);
        }

        return default;
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
        await (_redisDb?.Multiplexer.DisposeAsync() ?? ValueTask.CompletedTask);
        await (_connection?.DisposeAsync() ?? ValueTask.CompletedTask);

        LogMessage("HybridRedisCache disposed.");
    }
}
