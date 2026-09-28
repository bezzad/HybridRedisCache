namespace HybridRedisCache;

/// <summary>
/// The HybridCache class provides a hybrid caching solution that stores cached items in both
/// an in-memory cache and a Redis cache. 
/// </summary>
public partial class HybridCache : IHybridCache, IDisposable, IAsyncDisposable
{
    private readonly ConcurrentDictionary<string, TaskCompletionSource> _lockTasks = new();
    private readonly ConcurrentDictionary<string, Lazy<Task>> _dataRetrieverTasks = new();
    private readonly ActivitySource _activity;
    private readonly string _instanceId;
    private readonly SemaphoreSlim _reconnectSemaphore = new(1, 1);
    private readonly HybridCachingOptions _options;
    private readonly ILogger _logger;
    private string _keySpaceChannelName;
    private bool _disposed;
    private ConnectionMultiplexer _connection;
    private ISubscriber _redisSubscriber;
    // Concrete MemoryCache, and readonly: clearing used to dispose these and reassign the fields,
    // which raced with every unsynchronised read of them. Clear() empties an instance in place, so
    // the references never change and readers cannot land on a disposed cache.
    private readonly MemoryCache _memoryCache = new(new MemoryCacheOptions());
    private readonly MemoryCache _recentlySetKeys = new(new MemoryCacheOptions());
    private int _reconfigureAttemptCount;

    private static readonly TimeSpan ReconnectBackOff = TimeSpan.FromMilliseconds(500);

    private readonly KeyMeter _keyMeter;
    public IDatabase RedisDb { get; private set; }

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
        _options.Serializer ??= option.GetDefaultSerializer();

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

        // Note: ConfigurationOptions.SocketManager was removed in StackExchange.Redis 3.x.
        // The client now always uses its own dedicated socket scheduling, so
        // HybridCachingOptions.ThreadPoolSocketManagerEnable is no longer honoured.

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
            // No command is in flight at connect time, hence CommandFlags.None.
            throw new RedisConnectionException(ConnectionFailureType.UnableToConnect,
                CommandFlags.None, "Unable to connect Redis in initializing!",
                innerException: null, commandStatus: CommandStatus.Unknown);
        }

        _connection.ConnectionRestored += OnReconnect;
        _connection.ConnectionFailed += OnConnectionFailed;
        _connection.ErrorMessage += OnErrorMessage;
        RedisDb = _connection.GetDatabase();
        _redisSubscriber = _connection.GetSubscriber();

        // Subscribe to Redis key-space events to invalidate cache entries on all instances
        _keySpaceChannelName = $"__keyspace@{RedisDb.Database}__:{_options.InstancesSharedName}:";
        var keySpaceChannel = GetRedisKeySpaceChannel("*", RedisChannel.PatternMode.Pattern);
        _redisSubscriber.Subscribe(keySpaceChannel, OnBusMessage, CommandFlags.FireAndForget);
        SetRedisServersConfigs();

        LogMessage("HybridRedisCache connected and configured at endpoints: " +
                   string.Join(", ", redisConfig.EndPoints));
    }

    private async Task TryConnectAsync()
    {
        while (true)
        {
            // Set whenever an iteration ends without reaching a connected state. Without it the
            // loop would re-run immediately and spin the CPU while Redis is unreachable.
            var backOff = false;

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
                var pingSucceeded = await RedisDb.PingAsync(_options.ConnectRetry).ConfigureAwait(false);
                if (!pingSucceeded)
                {
                    LogMessage($"Redis ping failed after {_options.ConnectRetry} attempts. Proceeding to reconfigure.");
                    Connect(redisConfig);
                    _reconfigureAttemptCount = 0;
                }
                else
                {
                    // Redis answered, but the multiplexer has not reported itself connected yet;
                    // wait for it to settle instead of re-checking in a tight loop.
                    backOff = true;
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

                backOff = true;
            }
            finally
            {
                _reconnectSemaphore.Release();
            }

            // Delay outside the semaphore so a reconnect attempt never blocks other callers.
            if (backOff)
                await Task.Delay(ReconnectBackOff).ConfigureAwait(false);
        }
    }

    private void OnErrorMessage(object sender, RedisErrorEventArgs e)
    {
        LogMessage("Redis Internal Error: " + e.Message);
    }

    private void SetRedisServersConfigs()
    {
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
        //
        // Managed Redis services (Azure Cache for Redis, AWS ElastiCache, ...) and Redis-compatible
        // servers block or omit CONFIG SET. Failing here would make the whole cache unusable on those
        // servers, so the error is logged and startup continues: key-space notifications may already
        // be enabled server-side, and if they are not, the local cache simply relies on its own TTL.
        try
        {
            RedisDb.Execute("CONFIG", "SET", "notify-keyspace-events", "KA");
        }
        catch (Exception ex)
        {
            LogMessage(
                "Unable to enable key-space notifications (CONFIG SET notify-keyspace-events KA). " +
                "Cross-instance local cache invalidation will not work unless 'notify-keyspace-events' " +
                "is enabled on the server. Local cache entries will still expire via their own TTL.", ex);
        }

        if (!_options.EnableRedisClientTracking)
            return;

        try
        {
            var clientId = RedisDb.Execute("CLIENT", "ID");

            // Enable tracking with specific key prefixes to reduce overhead
            RedisDb.Execute($"CLIENT", "TRACKING", "ON", "REDIRECT", (long)clientId, "BCAST", "PREFIX",
                $"{_options.InstancesSharedName}:*", "NOLOOP");
        }
        catch (Exception ex)
        {
            // Client tracking is not available on every server (e.g. Redis Enterprise Cloud, issue #16).
            LogMessage("Unable to enable Redis client tracking (CLIENT TRACKING ON).", ex);
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

    private Activity PopulateActivity(OperationTypes operationType)
    {
        if (!_options.EnableTracing)
            return null;

        var activity = _activity.StartActivity(nameof(HybridCache));
        activity?.SetTag(nameof(HybridRedisCache) + ".OperationType", operationType.ToString("G"));
        return activity;
    }

    private void OnBusMessage(RedisChannel channel, RedisValue val)
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
                    LogMessage($"{nameof(OnBusMessage)}: Notification ignored for key: {key}");
                    _recentlySetKeys.Remove(key);
                    return;
                }

                _memoryCache.Remove(key);
                return;
            }

            if (val.Is(MessageType.RemoveKey) ||
                val.Is(MessageType.ExpiredKey) ||
                val.GetMessageType() == MessageType.RenameKey)
            {
                _memoryCache.Remove(key);
                _recentlySetKeys.Remove(key);
                if (_lockTasks.TryRemove(key, out var tcs))
                {
                    LogMessage($"{nameof(OnBusMessage)}: Continue to lock `{key}` key.");
                    tcs.SetResult();
                    return;
                }
            }

            if (val.Is(MessageType.ClearLocalCache) &&
                key != GetCacheKey(_instanceId)) // ignore self-instance from duplicate clearing
            {
                LogMessage($"{nameof(OnBusMessage)}: Clearing local cache");
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

        // MemoryCache is thread-safe and Clear() empties it in place, so this needs no lock and
        // cannot pull the cache out from under a concurrent reader. It runs on the bus thread
        // (a ClearLocalCache message) and on reconnect, both of which race with application reads.
        _memoryCache.Clear();
        _recentlySetKeys.Clear();
        LogMessage($"clear all local cache");
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
            // Remembered only for a window: if the notification for this write never arrives (a
            // server with key-space events disabled), the marker has to expire on its own or this
            // cache grows without bound.
            _recentlySetKeys.Set(key, DateTime.UtcNow, _options.SelfWriteNotificationWindow);
        }
    }

    private async ValueTask PublishBusAsync(MessageType type, string cacheKey)
    {
        try
        {
            await RedisDb.PublishAsync(GetRedisKeySpaceChannel(cacheKey), type.GetValue(), CommandFlags.FireAndForget)
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
            RedisDb.Publish(GetRedisKeySpaceChannel(cacheKey), type.GetValue(), CommandFlags.FireAndForget);
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

    private void SetValidExpiryTimes(HybridCacheEntry entry)
    {
        entry.LocalExpiry ??= _options.DefaultLocalExpirationTime;
        entry.RedisExpiry ??= _options.DefaultDistributedExpirationTime;
        if (entry.LocalExpiry.Value >  entry.RedisExpiry.Value)
            entry.LocalExpiry =  entry.RedisExpiry;
    }

    private byte[] Serialize(string key, object value)
    {
        var bytes = _options.Serializer.Serialize(value);
        if (_options.EnableMeterData)
            _keyMeter.RecordHeavyDataUsage(key, bytes.LongLength);

        return bytes;
    }

    private bool TryGetMemoryValue<T>(string cacheKey, Activity activity, out T value)
    {
        var hit = _memoryCache.TryGetValue(cacheKey, out value);
        _keyMeter.RecordLookup(KeyMeter.LocalLayer, hit);
        if (hit)
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
        var val = redisValue.Value;
        if (!val.HasValue)
        {
            _keyMeter.RecordLookup(KeyMeter.RedisLayer, hit: false);
            return false;
        }

        var localExpiry = TimeSpan.Zero;

        if (redisValue.Expiry.HasValue) // should be cached in local memory
            localExpiry = redisValue.Expiry.Value;

        value = _options.Serializer.Deserialize<T>(val);

        if (localExpiry > TimeSpan.Zero && localCacheEnable)
            SetLocalMemory(cacheKey, value, localExpiry, Condition.Always, false);

        _keyMeter.RecordLookup(KeyMeter.RedisLayer, hit: true);
        activity?.SetRetrievalStrategyActivity(RetrievalStrategy.RedisCache);
        activity?.SetCacheHitActivity(CacheResultType.Hit, cacheKey);

        return true;
    }

    private (Dictionary<string, T> found, List<string> missed) GetAllFromLocalMemory<T>(IEnumerable<string> keys,
        Activity activity)
    {
        var found = new Dictionary<string, T>();
        var missed = new List<string>();
        foreach (var key in keys.Distinct())
        {
            if (TryGetMemoryValue(GetCacheKey(key), activity, out T value))
                found[key] = value;
            else
                missed.Add(key);
        }

        return (found, missed);
    }

    private void AddRedisValues<T>(Dictionary<string, T> found, List<string> missed,
        RedisValueWithExpiry[] redisValues, bool localCacheEnable, Activity activity)
    {
        for (var i = 0; i < missed.Count; i++)
        {
            try
            {
                if (TryUpdateRedisValueOnLocalCache(GetCacheKey(missed[i]), redisValues[i], localCacheEnable,
                        activity, out T value))
                    found[missed[i]] = value;
            }
            catch (JsonSerializationException ex)
            {
                LogMessage($"Redis cache deserialization error, [{missed[i]}]", ex);
                _keyMeter.RecordLookup(KeyMeter.RedisLayer, hit: false);
            }
        }
    }

    private IServer[] GetServers(Flags flags)
    {
        // there may be multiple endpoints behind a multiplexer
        var servers = RedisDb.Multiplexer.GetServers(); //.GetEndPoints(configuredOnly: true);

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

    /// <summary>
    /// Runs <paramref name="dataRetriever"/> once per key even when many callers miss the cache at
    /// the same time: the first caller executes it and the others await that same task. Useful when
    /// the retriever is expensive, and it keeps a cache miss from turning into a stampede.
    /// </summary>
    /// <remarks>
    /// Callers that share a key therefore share one result, even if they passed different retriever
    /// delegates — the key identifies the value, so the retriever that wins the race populates it
    /// for everyone. Store per-caller values under different keys.
    /// </remarks>
    private async Task<T> FetchDataSafely<T>(string key, Func<string, Task<T>> dataRetriever)
    {
        // The dictionary holds the in-flight task, not the delegate: holding the delegate made every
        // caller invoke it (no de-duplication at all) and made losers of the race run the winner's
        // delegate. Lazy gives one execution even when several callers reach Value together.
        var inFlight = _dataRetrieverTasks.GetOrAdd(key,
            k => new Lazy<Task>(() => dataRetriever(k), LazyThreadSafetyMode.ExecutionAndPublication));

        try
        {
            if (inFlight.Value is Task<T> shared)
                return await shared.ConfigureAwait(false);

            // A retrieval for this key is already running for a different T, so its task is not
            // ours to await. Run our own rather than silently returning no value.
            return await dataRetriever(key).ConfigureAwait(false);
        }
        finally
        {
            // Remove only our own entry: a plain TryRemove(key) would let a late caller evict the
            // task that newer callers are still awaiting.
            _dataRetrieverTasks.TryRemove(new KeyValuePair<string, Lazy<Task>>(key, inFlight));
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

    private void ChannelHandler(RedisChannel channel, RedisValue value, RedisChannelMessage handler)
    {
        var strChannel = (string)channel ?? "";
        var index = strChannel.IndexOf(':');
        var key = index > 0 && index < strChannel.Length - 1
            ? strChannel[(index + 1)..]
            : strChannel;

        handler(key, value);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _redisSubscriber?.UnsubscribeAll();
        _memoryCache?.Dispose();
        // Both local caches hold a timer; disposing only the main one leaked this per instance.
        _recentlySetKeys?.Dispose();
        _connection?.Dispose();
        _reconnectSemaphore?.Dispose();

        LogMessage("HybridRedisCache disposed.");
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        _memoryCache?.Dispose();
        // Both local caches hold a timer; disposing only the main one leaked this per instance.
        _recentlySetKeys?.Dispose();
        _reconnectSemaphore?.Dispose();

        await (_redisSubscriber?.UnsubscribeAllAsync() ?? Task.CompletedTask);
        // RedisDb.Multiplexer is _connection, so disposing the connection is enough.
        await (_connection?.DisposeAsync() ?? ValueTask.CompletedTask);

        LogMessage("HybridRedisCache disposed.");
    }
}
