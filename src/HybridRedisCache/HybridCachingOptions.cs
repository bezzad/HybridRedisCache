using Microsoft.Extensions.Logging;

namespace HybridRedisCache;

public class HybridCachingOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether the connection should be aborted if the connection fails to reach the distributed cache.
    /// If true, Redis connect will not create a connection while no servers are available.
    /// </summary>
    public bool AbortOnConnectFail { get; set; } = true;
    
    /// <summary>
    /// Gets or sets a value indicating whether loggin is enable or not.
    /// </summary>
    /// <value><c>true</c> if enable logging; otherwise, <c>false</c>.</value>
    public bool EnableLogging { get; set; } = false;

    /// <summary>
    /// Gets or sets a value indicating whether an exception should be thrown if an error on the distributed cache has occurred
    /// </summary>
    /// <value><c>true</c> if distributed cache exceptions should not be ignored; otherwise, <c>false</c>.</value>
    public bool ThrowIfDistributedCacheError { get; set; } = false;

    /// <summary>
    /// Redis connection string
    /// </summary>
    public string RedisConnectString { get; set; } = "localhost:6379";

    /// <summary>
    /// Gets or sets the name of the instance which is shared between instances.
    /// </summary>
    public string InstancesSharedName { get; set; } = nameof(HybridCache);

    /// <summary>
    /// Gets or sets a expiry time of redis cache
    /// </summary>        
    public TimeSpan DefaultDistributedExpirationTime { get; set; } = TimeSpan.FromDays(1);

    /// <summary>
    /// Gets or sets a expiry time of local cache
    /// </summary>        
    public TimeSpan DefaultLocalExpirationTime { get; set; } = TimeSpan.FromMinutes(60);

    /// <summary>
    /// The Redis bus and connect retry count.
    /// </summary>
    /// <remarks>
    /// When sending message failed, we will retry some times, default is 3 times.
    /// </remarks>
    public int ConnectRetry { get; set; } = 3;

    /// <summary>
    /// Flush the local cache on bus disconnection/reconnection
    /// </summary>
    /// <remarks>
    /// Flushing the local cache will avoid using stale data but may cause app jitters until the local cache get's re-populated.
    /// </remarks>
    public bool FlushLocalCacheOnBusReconnection { get; set; } = false;

    /// <summary>
    /// The timeout amount of Redis sync operations. Default is 5000ms
    /// </summary>
    public int SyncTimeout { get; set; } = 5000;

    /// <summary>
    /// The timeout amount of Redis Async operations. Default is 5000ms
    /// </summary>
    public int AsyncTimeout { get; set; } = 5000;

    /// <summary>
    /// The timeout amount of making a connection to Redis. Default is 5000ms 
    /// </summary>
    public int ConnectionTimeout { get; set; } = 5000;

    /// <summary>
    /// Flag to set whether administrator operations are allowed. Default is false
    /// </summary>
    public bool AllowAdmin { get; set; } = false;

    /// <summary>
    /// Set the connection keep alive value in seconds. Default is 60s
    /// </summary>
    public int KeepAlive { get; set; } = 60;

    /// <summary>
    /// Sets the level of logs that are without exceptions. Default is debug 
    /// </summary>
    public LogLevel TracingLogLevel { get; set; } = LogLevel.Debug;

}
