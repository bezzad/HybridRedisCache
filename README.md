[![NuGet](https://img.shields.io/nuget/dt/HybridRedisCache.svg)](https://www.nuget.org/packages/HybridRedisCache)
[![NuGet](https://img.shields.io/nuget/vpre/HybridRedisCache.svg)](https://www.nuget.org/packages/HybridRedisCache)
[![Generic badge](https://img.shields.io/badge/support-.Net_Core-blue.svg)](https://github.com/bezzad/HybridRedisCache)
[![Generic badge](https://img.shields.io/badge/support-.Net_Standard-blue.svg)](https://github.com/bezzad/HybridRedisCache)

# HybridRedisCache

`HybridRedisCache` is a simple in-memory and Redis hybrid caching solution for .NET applications. 
It provides a way to cache frequently accessed data in memory for fast access and automatically falls back to using Redis as a persistent cache when memory cache capacity is exceeded.

## Types of Cache
Basically, there are two types of caching .NET Core supports

1. In-Memory Caching
2. Distributed Caching

When we use In-Memory Cache then in that case data is stored in the application server memory and whenever we need then we fetch data from that and use it wherever we need it. And in Distributed Caching there are many third-party mechanisms like Redis and many others. But in this section, we look into the Redis Cache in detail and how it works in the .NET Core

## Distributed Caching
Basically, in the distributed cachin,g data are stored and shared between multiple servers
Also, it’s easy to improve scalability and performance of the application after managing the load between multiple servers when we use multi-tenant application
Suppose, In the future, if one server is crashed and restarted then the application does not have any impact because multiple servers are as per our need if we want
Redis is the most popular cache which is used by many companies nowadays to improve the performance and scalability of the application. So, we are going to discuss Redis and usage one by one.

## Redis Cache
Redis is an Open Source (BSD Licensed) in-memory Data Structure store used as a database.
Basically, it is used to store the frequently used and some static data inside the cache and use and reserve that as per user requirement.
There are many data structures present in the Redis which we are able to use like List, Set, Hashing, Stream, and many more to store the data.

## Redis vs. In-Memory caching in single instance benchmark

![Redis vs. InMemory](https://raw.githubusercontent.com/bezzad/HybridRedisCache/main/img/Redis%20vs.%20MemoryCache%20-%20Single%20Instance.png)

## Installation

You can install the `HybridRedisCache` package using NuGet:

> `PM> Install-Package HybridRedisCache`

## Usage

### Simple usage in console applications

To use `HybridCache`, you can create an instance of the `HybridCache` class and then call its `Set` and `Get` methods to cache and retrieve data, respectively.
Here's an example:

```csharp
using HybridRedisCache;

...

// Create a new instance of HybridCache with cache options
var options = new HybridCachingOptions()
{
    DefaultLocalExpirationTime = TimeSpan.FromMinutes(1),
    DefaultDistributedExpirationTime = TimeSpan.FromDays(1),
    InstancesSharedName = "SampleApp",
    ThrowIfDistributedCacheError = true,
    RedisCacheConnectString = "localhost:6379",
    BusRetryCount = 10,
    EnableLogging = true,
    FlushLocalCacheOnBusReconnection = true,
};
var cache = new HybridCache(options);

// Cache a string value with key "mykey" for 1 minute
cache.Set("mykey", "myvalue", TimeSpan.FromMinutes(1));

// Retrieve the cached value with key "mykey"
var value = cache.Get<string>("mykey");
```

### Configure Startup class for Web APIs

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHybridRedisCaching(options =>
{
    options.InstancesSharedName = "RedisCacheSystem.Demo";
    options.DefaultLocalExpirationTime = TimeSpan.FromMinutes(1);
    options.DefaultDistributedExpirationTime = TimeSpan.FromDays(10);
    options.ThrowIfDistributedCacheError = true;
    options.RedisCacheConnectString = "localhost:6379";
    options.BusRetryCount = 10;
    options.EnableLogging = true;
    options.FlushLocalCacheOnBusReconnection = true;
});
```

### Write code in your controller

```csharp
[Route("api/[controller]")]
public class WeatherForecastController : Controller
{
    private readonly IHybridCache _cacheService;

    public VWeatherForecastController(IHybridCache cacheService)
    {
        this._cacheService = cacheService;
    }

    [HttpGet]
    public string Handle()
    {
        //Set
        _cacheService.Set("demo", "123", TimeSpan.FromMinutes(1));
            
        //Set Async
        await _cacheService.SetAsync("demo", "123", TimeSpan.FromMinutes(1));                  
    }

    [HttpGet)]
    public async Task<WeatherForecast> Get(int id)
    {
        var data = await _cacheService.GetAsync<WeatherForecast>(id);
        return data;
    }

    [HttpGet)]
    public IEnumerable<WeatherForecast> Get()
    {
        var data = _cacheService.Get<IEnumerable<WeatherForecast>>("demo");
        return data;
    }
}
```


## Features
`HybridCache` is a caching library that provides a number of advantages over traditional `in-memory` caching solutions. 
One of its key features is the ability to persist caches between instances and sync data for all instances.

With `HybridCache`, you can create multiple instances of the cache that share the same `Redis` cache, 
allowing you to scale out your application and distribute caching across multiple instances. 
This ensures that all instances of your application have access to the same cached data, 
regardless of which instance originally created the cache.

When a value is set in the cache using one instance, the cache invalidation message is sent to all other instances, 
ensuring that the cached data is synchronized across all instances. 
This allows you to take advantage of the benefits of caching, 
such as reduced latency and **improved performance**, while ensuring that the cached data is consistent across all instances.

Other features of HybridCache include:

* Support for multiple cache layers, including in-memory and Redis caching layers
* **Automatic expiration** of cached data based on time-to-live (TTL) or sliding expiration policies
* Support for fire-and-forget caching, which allows you to quickly set a value in the cache without waiting for a response
* Support for asynchronous caching operations, which allows you to perform cache operations asynchronously and improve the responsiveness of your application

Overall, `HybridCache` provides a powerful and flexible caching solution that can help you improve the performance and scalability of your applications, while ensuring that the cached data is consistent across all instances.

## Installation of Redis Cache with docker

### Step 1
Install docker on your OS.

### Step 2 
Open bash and type below commands:
```cmd
$ docker pull redis:latest
$ docker run --name redis -p 6379:6379 -d redis:latest
```

Test is redis running:
```cmd
$ docker exec -it redis redis-cli
$ ping
```

## Contributing

Contributions are welcome! If you find a bug or have a feature request, please open an issue or submit a pull request.
If you'd like to contribute to `HybridRedisCache`, please follow these steps:

1. Fork the repository.
2. Create a new branch for your changes.
3. Make your changes and commit them.
4. Push your changes to your fork.
5. Submit a pull request.

## License

`HybridRedisCache` is licensed under the Apache License, Version 2.0. See the [LICENSE](https://raw.githubusercontent.com/bezzad/HybridRedisCache/dev/LICENSE) file for more information.