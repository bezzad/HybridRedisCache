using HybridRedisCache;

Console.WriteLine("Welcome Hybrid Redis Cache");

// Create a new instance of HybridCache with cache options
var options = new HybridCachingOptions()
{
    DefaultExpirationTime = TimeSpan.FromSeconds(1),
    InstanceName = "SampleApp",
    ThrowIfDistributedCacheError = true,
    RedisCacheConnectString = "localhost:6379",
    BusRetryCount = 10,
    EnableLogging = true,
    FlushLocalCacheOnBusReconnection = true,
};
var cache = new HybridCache(options);

Console.WriteLine($"retrieving 'mykey' value from redis cache...");
var retrivedValue = cache.Get<string>("mykey");
Console.WriteLine($"value is: {retrivedValue}\n\n");

// Cache a string value with key "mykey" for 1 minute
cache.Set("mykey", Guid.NewGuid().ToString("N"), TimeSpan.FromMinutes(100));
Console.WriteLine($"mykey cached with a new GUID value for 100min");

// Retrieve the cached value with key "mykey"
Console.WriteLine($"retrieving mykey value from local memory cache...");
retrivedValue = cache.Get<string>("mykey");
Console.WriteLine($"value is: {retrivedValue}");
