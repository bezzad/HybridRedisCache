using HybridRedisCache;

// Create a new instance of HybridCache with cache options
var options = new HybridCachingOptions()
{
    DefaultLocalExpirationTime = TimeSpan.FromMinutes(1),
    DefaultDistributedExpirationTime = TimeSpan.FromDays(1),
    InstancesSharedName = "SampleApp",
    ThrowIfDistributedCacheError = false,
    RedisConnectionString = "localhost:6379,allowAdmin=true,keepAlive=180,defaultDatabase=0",
    ConnectRetry = 10,
    ConnectionTimeout = 2000,
    SyncTimeout = 1000,
    AsyncTimeout = 1000,
    AbortOnConnectFail = true,
    ReconfigureOnConnectFail = true,
    MaxReconfigureAttempts = 2,
    EnableLogging = true,
    EnableTracing = true,
    ThreadPoolSocketManagerEnable = true,
    FlushLocalCacheOnBusReconnection = true,
    TracingActivitySourceName = nameof(HybridRedisCache),
    EnableRedisClientTracking = true
};
var cache = new HybridCache(options);

Console.WriteLine("Welcome Hybrid Redis Cache");
Help();

while (true)
{
    var line = Console.ReadLine()?.Trim();
    var words = line?.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    var command = words?.FirstOrDefault()?.ToLower();

    switch (command)
    {
        case "-set": Set(words); break;
        case "-rem": Remove(words); break;
        case "-get": Get(words); break;
        case "-help": Help(); break;
        default:
            InvalidMsg();
            Help();
            break;
    }
}

void InvalidMsg() => Console.WriteLine("Invalid command!");
void SetHelp() => Console.WriteLine("\t-set {key} {value} {expiry ms} \t add new value to cache");
void RemoveHelp() => Console.WriteLine("\t-rem {key} \t remove cache key");
void GetHelp() => Console.WriteLine("\t-get {key} \t get value of the cache key");

void Help()
{
    Console.WriteLine("Type your command to Redis with HybridRedisCache\n\n");
    SetHelp();
    RemoveHelp();
    GetHelp();
    Console.WriteLine("\t-help \t show commands list");
    Console.WriteLine();
}

void Set(string[] words)
{
    if (words.Length == 4)
    {
        cache.Set(words[1], words[2],
            redisExpiry: TimeSpan.FromMilliseconds(int.Parse(words[3])),
            localCacheEnable: false);
        Console.WriteLine($"[{words[1]}]: {words[2]} added with {words[3]}ms expiry");
    }
    else if (words.Length == 3)
    {
        cache.Set(words[1], words[2], localCacheEnable: false);
        Console.WriteLine($"[{words[1]}]: {words[2]} added no expiry");
    }
    else
    {
        InvalidMsg();
        SetHelp();
    }
}

void Remove(string[] words)
{
    if (words.Length == 2)
    {
        cache.Remove(words[1]);
        Console.WriteLine($"{words[1]} removed from Redis.");
    }
    else
    {
        InvalidMsg();
        RemoveHelp();
    }
}

void Get(string[] words)
{
    if (words.Length == 2)
    {
        var val = cache.Get<string>(words[1], s => s + "_defaultOfDataRetriever");
        Console.WriteLine(val ?? "NULL");
    }
    else
    {
        InvalidMsg();
        RemoveHelp();
    }
}