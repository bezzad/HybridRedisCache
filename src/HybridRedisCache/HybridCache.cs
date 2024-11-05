using System.Collections.Concurrent;
using HybridRedisCache.Utilities;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using System.Diagnostics;

namespace HybridRedisCache;

/// <summary>
/// The HybridCache class provides a hybrid caching solution that stores cached items in both
/// an in-memory cache and a Redis cache. 
/// </summary>
public partial class HybridCache : IHybridCache, IDisposable
{
    private record ChannelMessage(string InstanceId, RedisMessageBusActionType BusActionType, params string[] Keys);

    private readonly ConcurrentDictionary<string, ConcurrentBag<TaskCompletionSource>> _lockTasks = new();
    private readonly ActivitySource _activity;
    private readonly IDatabase _redisDb;
    private readonly string _instanceId;
    private readonly HybridCachingOptions _options;
    private readonly ISubscriber _redisSubscriber;
    private readonly ILogger _logger;
    private readonly RedisChannel _messagesChannel;
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
        _messagesChannel =
            new RedisChannel(_options.InstancesSharedName + ":messages", RedisChannel.PatternMode.Literal);

        // Subscribe to Redis key-space events to invalidate cache entries on all instances
        _redisSubscriber.Subscribe(_messagesChannel, OnMessage, CommandFlags.FireAndForget);
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

        var activity = _activity.StartActivity(nameof(HybridCache));
        activity?.SetTag(nameof(HybridRedisCache) + ".OperationType", operationType.ToString("G"));
        return activity;
    }

    private void OnMessage(RedisChannel channel, RedisValue value)
    {
        // With this implementation, when a key is updated or removed in Redis,
        // all instances of HybridCache that are subscribed to the pub/sub channel will receive a message
        // and invalidate the corresponding key in their local MemoryCache.

        var message = value.ToString().Deserialize<ChannelMessage>();

        if (message?.Keys is null ||
            message.Keys.Length == 0)
            return; // filter out messages from the current instance

        var firstKey = message.Keys.First();
        LogMessage(
            $"OnMessage: A {message.BusActionType} message received from instance {message.InstanceId} with first key: {firstKey}");

        switch (message.BusActionType)
        {
            case RedisMessageBusActionType.InvalidateCacheKeys when message.InstanceId != _instanceId:
            {
                foreach (var key in message.Keys)
                {
                    _memoryCache.Remove(key);
                    LogMessage($"OnMessage: remove local cache with key '{key}'");
                }

                break;
            }
            case RedisMessageBusActionType.ClearAllLocalCache when firstKey.Equals(ClearAllKey):
            {
                ClearLocalMemory();
                break;
            }
            case RedisMessageBusActionType.NotifyLockReleased:
            {
                if (_lockTasks.TryGetValue(firstKey, out var bag))
                {
                    while (bag.TryTake(out var tcs))
                    {
                        LogMessage($"OnMessage: Called the SetResult() of TaskCompletionSource of `{firstKey}` key");
                        tcs.SetResult();
                    }
                }

                break;
            }
            default:
                LogMessage($"OnMessage: Unknown message caught: '{firstKey}' as {message.BusActionType} type");
                break;
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
            CreateLocalCache();
            LogMessage($"clear all local cache");
        }
    }

    private string GetCacheKey(string key) => $"{_options.InstancesSharedName}:{key}";

    private async Task PublishBusAsync(RedisMessageBusActionType busActionType, params string[] cacheKeys)
    {
        cacheKeys.NotNullAndCountGTZero(nameof(cacheKeys));

        try
        {
            // include the instance ID in the pub/sub message payload to update another instances
            var message = new ChannelMessage(_instanceId, busActionType, cacheKeys);
            await _redisDb.PublishAsync(_messagesChannel, message.Serialize(), CommandFlags.FireAndForget)
                .ConfigureAwait(false);
        }
        catch
        {
            // Retry to publish message
            if (_retryPublishCounter++ < _options.ConnectRetry)
            {
                await Task.Delay(_exponentialRetryMilliseconds * _retryPublishCounter).ConfigureAwait(false);
                await PublishBusAsync(busActionType, cacheKeys).ConfigureAwait(false);
            }
        }
    }

    private void PublishBus(RedisMessageBusActionType busActionType, params string[] cacheKeys)
    {
        cacheKeys.NotNullAndCountGTZero(nameof(cacheKeys));

        try
        {
            // include the instance ID in the pub/sub message payload to update another instances
            var message = new ChannelMessage(_instanceId, busActionType, cacheKeys);
            _redisDb.Publish(_messagesChannel, message.Serialize(), CommandFlags.FireAndForget);
            LogMessage(
                $"Published a {message.BusActionType} message on the Redis bus from instance {message.InstanceId}" +
                $" for first key: {cacheKeys.FirstOrDefault()}");
        }
        catch
        {
            // Retry to publish message
            if (_retryPublishCounter++ < _options.ConnectRetry)
            {
                Thread.Sleep(_exponentialRetryMilliseconds * _retryPublishCounter);
                PublishBus(busActionType, cacheKeys);
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
        if (!redisValue.Expiry.HasValue) return false;
        value = redisValue.Value.ToString().Deserialize<T>();
        if (value is null) return false;

        localExpiry ??= _options.DefaultLocalExpirationTime;
        if (localExpiry > redisValue.Expiry.Value)
            localExpiry = redisValue.Expiry.Value;

        _memoryCache.Set(cacheKey, value, localExpiry.Value);
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