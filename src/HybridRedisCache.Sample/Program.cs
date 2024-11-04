using HybridRedisCache;

const string Key = "HybridRedisCache.Sample.Console.Key";
Console.WriteLine("Welcome Hybrid Redis Cache");

// Create a new instance of HybridCache with cache options
var options = new HybridCachingOptions()
{
    DefaultLocalExpirationTime = TimeSpan.FromMinutes(1),
    DefaultDistributedExpirationTime = TimeSpan.FromDays(1),
    InstancesSharedName = "SampleApp",
    ThrowIfDistributedCacheError = true,
    RedisConnectString = "localhost:6379,allowAdmin=true,keepAlive=180,defaultDatabase=0",
    ConnectRetry = 10,
    EnableLogging = true,
    EnableTracing = true,
    ThreadPoolSocketManagerEnable = true,
    FlushLocalCacheOnBusReconnection = true,
    TracingActivitySourceName = nameof(HybridRedisCache)
};
var cache = new HybridCache(options);

Console.WriteLine($"retrieving [{Key}] value from redis cache...");
var retrivedValue = cache.Get<string>(Key);
Console.WriteLine($"value is: {retrivedValue}\n\n");

// Cache a string value with key for 1 minute
cache.Set(Key, Guid.NewGuid().ToString("N"), TimeSpan.FromDays(100));
Console.WriteLine($"[{Key}] cached with a new GUID value for 100 days");

// Retrieve the cached value with key
Console.WriteLine($"retrieving [{Key}] value from local memory cache...");
retrivedValue = cache.Get<string>(Key);
Console.WriteLine($"value is: {retrivedValue}");

Console.ReadLine();
