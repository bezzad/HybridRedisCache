using StackExchange.Redis;
using System.Runtime.CompilerServices;
[assembly: InternalsVisibleTo("HybridRedisCache.Test")]
namespace HybridRedisCache;

internal static class InternalExtensions
{
    /// <summary>
    /// Extension method for converting hybrid cache options to StackExchange configuration options
    /// </summary>
    /// <param name="option"></param>
    /// <returns></returns>
    public static ConfigurationOptions ConvertHybridCacheOptionsToRedisOptions(this HybridCachingOptions option)
    {
        var redisConfig = ConfigurationOptions.Parse(option.RedisConnectString, true);
        redisConfig.AbortOnConnectFail = option.AbortOnConnectFail;
        redisConfig.ConnectRetry = option.ConnectRetry;
        redisConfig.ClientName = option.InstancesSharedName + ":" + option.InstanceId;
        redisConfig.AsyncTimeout = option.AsyncTimeout;
        redisConfig.SyncTimeout = option.SyncTimeout;
        redisConfig.ConnectTimeout = option.ConnectionTimeout;
        redisConfig.KeepAlive = option.KeepAlive;
        redisConfig.AllowAdmin = option.AllowAdmin;

        return redisConfig;
    }

    public static HybridCachingOptions DeepCopyCachingOptions(this HybridCachingOptions options)
    {
        return new HybridCachingOptions()
        {
            AbortOnConnectFail = options.AbortOnConnectFail,
            AllowAdmin = options.AllowAdmin,
            AsyncTimeout = options.AsyncTimeout,
            ConnectRetry = options.ConnectRetry,
            ConnectionTimeout = options.ConnectionTimeout,
            DefaultDistributedExpirationTime = options.DefaultDistributedExpirationTime,
            DefaultLocalExpirationTime = options.DefaultLocalExpirationTime,
            EnableLogging = options.EnableLogging,
            FlushLocalCacheOnBusReconnection = options.FlushLocalCacheOnBusReconnection,
            InstancesSharedName = options.InstancesSharedName,
            KeepAlive = options.KeepAlive,
            RedisConnectString = options.RedisConnectString,
            SyncTimeout = options.SyncTimeout,
            ThrowIfDistributedCacheError = options.ThrowIfDistributedCacheError
        };
    }
}