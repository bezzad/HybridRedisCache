[![NuGet](https://img.shields.io/nuget/dt/HybridRedisCache.svg)](https://www.nuget.org/packages/HybridRedisCache)
[![NuGet](https://img.shields.io/nuget/vpre/HybridRedisCache.svg)](https://www.nuget.org/packages/HybridRedisCache)
[![codecov](https://codecov.io/github/bezzad/HybridRedisCache/graph/badge.svg)](https://codecov.io/github/bezzad/HybridRedisCache)
[![Generic badge](https://img.shields.io/badge/support-.Net_Core-blue.svg)](https://github.com/bezzad/HybridRedisCache)

# HybridRedisCache

`HybridRedisCache` is a Redis-focused, two-level caching library for .NET applications. It combines a fast
in-process memory cache (L1) with a shared Redis cache (L2). Reads check L1 first, then Redis on a local miss;
values retrieved from Redis can repopulate L1.

This is not a capacity-triggered fallback. L1 and L2 have independent expiration policies, and Redis provides the
shared cache used by all application instances.

## Cache layers

### In-memory cache (L1)

The local cache lives inside each application process. It offers the lowest latency, but every server or pod has its
own copy and loses that copy when the process stops.

### Redis cache (L2)

Redis is a shared, in-memory data store available to all application instances. It allows instances to reuse cached
data after local misses and helps preserve cache availability when an application instance restarts.

The important challenge in a two-level cache is invalidation: when one instance changes a Redis key, local copies held
by other instances can become stale. `HybridRedisCache` uses Redis keyspace notifications to invalidate those local
copies across instances. See [Server requirements](#server-requirements).

## Redis vs. In-Memory caching in single instance benchmark

![Redis vs. InMemory](https://raw.githubusercontent.com/bezzad/HybridRedisCache/main/img/Redis%20vs.%20MemoryCache%20-%20Single%20Instance.png)

## Installation

You can install the `HybridRedisCache` package using NuGet:

> `PM> Install-Package HybridRedisCache`

Installing via the .NET Core command line interface:

> `dotnet add package HybridRedisCache`

## Usage

### Simple usage in console applications

To use `HybridCache`, you can create an instance of the `HybridCache` class and then call its `Set` and `Get` methods to
cache and retrieve data, respectively.
Here's an example:

```csharp
using HybridRedisCache;
using HybridRedisCache.Serializers;

...

// Create a new instance of HybridCache with cache options
var options = new HybridCachingOptions()
{
    DefaultLocalExpirationTime = TimeSpan.FromMinutes(1),
    DefaultDistributedExpirationTime = TimeSpan.FromDays(1),
    InstancesSharedName = "SampleApp",
    ThrowIfDistributedCacheError = true,
    RedisConnectionString = "localhost:6379",
    ConnectRetry = 10,
    AbortOnConnectFail = true,
    ReconfigureOnConnectFail = true,
    MaxReconfigureAttempts = 10,
    EnableLogging = true,
    EnableTracing = true,
    FlushLocalCacheOnBusReconnection = true,
    TracingActivitySourceName = nameof(HybridRedisCache),
    EnableRedisClientTracking = true,
    EnableMeterData = true,
    WarningHeavyDataThresholdBytes = 20 * 1024, // 20KB
    DataSizeHistogramMetricName = "my_app_keys_data_size_histogram_metric",
    SerializerType = SerializerType.Bson, // Bson, MessagePack, MemoryPack, or custom
    // Serializer = new CustomBinarySerializer(),
};
var cache = new HybridCache(options);

// Cache a string value with the key "mykey" for 1 minute
cache.Set("mykey", "myvalue", TimeSpan.FromMinutes(1));

// Retrieve the cached value with the key "mykey"
var value = cache.Get<string>("mykey");

// Retrieve the value or create and cache it when it does not exist
var retrievedValue = await cache.GetAsync(
    "mykey",
    dataRetriever: key => CreateValueTaskAsync(key, ...),
    localExpiry: TimeSpan.FromMinutes(1),
    redisExpiry: TimeSpan.FromHours(6));

```

### Configure Startup class for Web APIs

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHybridRedisCaching(options =>
{
    options.AbortOnConnectFail = false;
    options.InstancesSharedName = "RedisCacheSystem.Demo";
    options.DefaultLocalExpirationTime = TimeSpan.FromMinutes(1);
    options.DefaultDistributedExpirationTime = TimeSpan.FromDays(10);
    options.ThrowIfDistributedCacheError = true;
    options.RedisConnectionString = "localhost:6379,redis0:6380,redis1:6380,allowAdmin=true,keepAlive=180";
    options.ConnectRetry = 10;
    options.EnableLogging = true;
    options.EnableTracing = true;
    options.TracingActivitySourceName = nameof(HybridRedisCache);
    options.FlushLocalCacheOnBusReconnection = true;
});
```

### Use the cache in a controller

```csharp
[ApiController]
[Route("api/[controller]")]
public sealed class WeatherForecastController : ControllerBase
{
    private readonly IHybridCache _cache;

    public WeatherForecastController(IHybridCache cache)
    {
        _cache = cache;
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Set(
        int id,
        WeatherForecast forecast,
        CancellationToken token)
    {
        await _cache.SetAsync(
            $"weather:{id}",
            forecast,
            localExpiry: TimeSpan.FromMinutes(1),
            redisExpiry: TimeSpan.FromHours(6),
            token: token);

        return NoContent();
    }

    [HttpGet("{id:int}")]
    public Task<WeatherForecast> Get(int id, CancellationToken token)
    {
        return _cache.GetAsync<WeatherForecast>($"weather:{id}", token: token);
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

When a Redis key is changed or removed, Redis keyspace notifications tell the other application instances to evict
their local copies. A subsequent read reloads the current value from Redis. This reduces latency while limiting the
window in which another instance could serve stale local data.

Other features of `HybridCache` include:

* Multiple cache layers: Supports both in-memory and Redis caching layers, allowing for flexible caching strategies.
* Automatic expiration: Cached data can automatically expire based on time-to-live (TTL) or sliding expiration policies.
* Fire-and-forget caching: Enables quickly setting a value in the cache without waiting for a response, improving
  performance for non-critical cache operations.
* Asynchronous caching operations: Provides asynchronous cache operations to enhance application responsiveness and
  scalability.
* Distributed key locking: Ensures control over race conditions across multiple services, preventing conflicts with
  shared resources.
* Client synchronization with Redis messages: Keeps all clients in sync through Redis bus messages. For example, if a
  key is updated or removed by one client, other clients will automatically clear the key from their local cache,
  ensuring consistency across instances.

Overall, `HybridCache` provides a powerful and flexible caching solution that helps enhance the performance and
scalability of your applications while ensuring that cached data remains consistent across all instances.

## Why not use Microsoft's HybridCache?

Microsoft's [`HybridCache`](https://learn.microsoft.com/aspnet/core/performance/caching/hybrid) is an excellent
general-purpose cache. It uses `MemoryCache` as its primary cache and any configured `IDistributedCache`
implementation as its secondary cache. Redis is one possible secondary backend, but the abstraction is intentionally
not Redis-specific.

The main difference for applications running on multiple servers or pods is local-cache invalidation. Microsoft
documents that removing a key or tag invalidates the current server and the secondary cache, but
[does not affect in-memory entries on other servers](https://learn.microsoft.com/aspnet/core/performance/caching/hybrid#cache-storage).
Those servers can continue serving their existing L1 value until its local expiration.

`HybridRedisCache` is designed specifically for Redis and uses Redis keyspace notifications to evict matching L1
entries in other application instances.

| Feature | Microsoft `HybridCache` | `HybridRedisCache` |
| --- | --- | --- |
| L1 in-process memory cache | Yes | Yes |
| L2 cache | Any `IDistributedCache` provider | Redis |
| Read-through caching and concurrent request coalescing | Yes | Yes |
| Cross-instance L1 invalidation after a Redis key changes | No | Yes, through Redis keyspace notifications |
| Direct Redis `IDatabase` access | No | Yes |
| Redis pub/sub | No | Yes |
| Distributed Redis locks | No | Yes |
| Redis hashes and Lua scripts | No | Yes |
| Redis Sentinel and server operations | No | Yes |
| Serialization | Built-in JSON/string support and custom serializers | BSON, MessagePack, MemoryPack, or a custom serializer |
| Tag-based logical invalidation | Yes | No |

Choose Microsoft `HybridCache` when you want a general cache abstraction and short L1 staleness bounded by local TTL
is acceptable. Choose `HybridRedisCache` when Redis-specific operations or prompt cross-instance L1 invalidation are
requirements.

> Cross-instance invalidation requires Redis keyspace notifications. If they are not enabled, other instances can keep
> stale local entries until their L1 TTL expires, just as with a cache that has no cross-instance invalidation channel.

## Cancellation tokens

Every asynchronous API accepts an optional trailing `CancellationToken`:

```csharp
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

await cache.SetAsync("mykey", "myvalue", token: cts.Token);
var value = await cache.GetAsync<string>("mykey", token: cts.Token);
```

> **What cancelling actually does.** `StackExchange.Redis` does not accept a `CancellationToken` on its
> command APIs. Cancelling therefore stops *your* call from waiting and throws `OperationCanceledException`;
> the command has already been handed to the multiplexer and the server may still apply it. Use the token to
> bound how long a caller waits, not to guarantee a write never lands.

## Server requirements

* **Redis 6.0+** for general use.
* **Redis 8.0+** for `HashSetAsync(key, IDictionary<string, string>, ...)` (issues `HSETEX`) and
  `HashFieldGetAndDeleteAsync` (issues `HGETDEL`).
* **`notify-keyspace-events`** must be enabled for cross-instance local cache invalidation. `HybridCache`
  tries to enable it at startup with `CONFIG SET notify-keyspace-events KA`. Managed services such as
  **Azure Cache for Redis** and **AWS ElastiCache** block `CONFIG SET`; there the call is logged as an error
  and startup continues. Enable `notify-keyspace-events` through the provider's own configuration, otherwise
  local cache entries only expire via their own TTL and may serve stale data until then.

## When should I enable caching?

Each time the value of a cached key is modified in the database,
Redis pushes an invalidation message to all the clients that are caching the key.
This tells the clients to flush the key’s locally cached value, which is invalid.
This behavior implies a trade-off between local cache hits and invalidation messages:
keys that show a local cache hit rate greater than the invalidation message rate are the best candidates for local
tracking and caching.

## Installation of Redis Cache with docker

### Step 1

Install docker on your OS.

### Step 2

Open bash and type below commands:

```cmd
$ docker pull redis:8.2
$ docker run --name redis -p 6379:6379 -d redis:8.2
```

> Use a **Redis 8.x** tag. `HashSetAsync(key, IDictionary<string, string>, ...)` issues `HSETEX` and
> `HashFieldGetAndDeleteAsync` issues `HGETDEL`; both are Redis 8.0+. See
> [Server requirements](#server-requirements). Avoid `redis:latest` — it silently moves between major
> versions.

Verify that Redis is running:

```cmd
$ docker exec -it redis redis-cli
$ ping
```

## Building and testing

### Prerequisites

* **.NET 10 SDK** — the library multi-targets `net8.0`, `net9.0` and `net10.0`, so building the solution
  needs the newest of those installed.
* **Docker** — required only for the container-backed test suite, described below.

### The two test suites

The tests are split by what they need to run:

| Suite | Base class | Needs Docker? |
| --- | --- | --- |
| In-process | `InProcessCacheTest` | No |
| Container-backed | `BaseCacheTest` | **Yes** |

The **in-process** suite runs [Microsoft Garnet](https://github.com/microsoft/garnet), a Redis-compatible
server, inside the test process. No daemon, no image pull. Run it anywhere with:

```bash
dotnet test src/HybridRedisCache.Test --filter "FullyQualifiedName~InProcess|FullyQualifiedName~SerializerTests|FullyQualifiedName~ArgumentCheckTest|FullyQualifiedName~ObjectHelperTest|FullyQualifiedName~SetAllBehaviorTests|FullyQualifiedName~CancellationTokenTests"
```

The **container-backed** suite uses [Testcontainers](https://dotnet.testcontainers.org/) to start a real
Redis. It exists because Garnet does not implement everything this library uses — key-space notifications
(`CONFIG SET notify-keyspace-events`), pub/sub, and the Redis 8 `HSETEX` / `HGETDEL` hash commands. Anything
covering those behaviours must live here.

### Docker prerequisites for the container-backed suite

**1. A running Docker daemon.** Testcontainers talks to `/var/run/docker.sock`.

**2. Non-root access to the daemon.** Testcontainers connects to the socket as whoever owns the test
process and has no way to escalate, so **running the tests with `sudo` does not help** — an IDE such as
Rider runs as your own user. Your user must be able to reach the socket unaided. On Linux:

```bash
sudo usermod -aG docker $USER
```

Then **log out and back in** — group membership is applied at login, so an existing shell or IDE will keep
failing until you start a new session. Symptoms of missing this step:

```
Docker is either not running or misconfigured. Please ensure that Docker is running
and that the endpoint is properly configured.
  Details: Failed to connect to Docker endpoint at 'unix:///var/run/docker.sock'.
```

```
permission denied while trying to connect to the docker API at unix:///var/run/docker.sock
```

Verify with:

```bash
docker info --format '{{.ServerVersion}}'   # must succeed without sudo
```

> If Docker was installed as a **snap**, the `docker` group may not exist yet. Create it and restart the
> service first: `sudo addgroup --system docker && sudo snap disable docker && sudo snap enable docker`.

**3. The images.** Testcontainers pulls these on first run; pre-pulling avoids a first-run timeout:

```bash
docker pull redis:8.2
docker pull testcontainers/ryuk:0.14.0   # Testcontainers' container-cleanup sidecar
```

> The `ryuk` tag is chosen by the `Testcontainers` package, not by this repo, so it changes when that
> package is upgraded. If the pull 404s, let Testcontainers pull it itself on the first test run, or read
> the current tag from the package.

You do **not** need to start Redis yourself — each test class starts and disposes its own container on a
random port. The image tag is pinned in `BaseCacheTest.RedisImage` and must stay on Redis 8.x.

Once the above is in place, run everything:

```bash
dotnet test src/HybridRedisCache.sln
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

`HybridRedisCache` is licensed under the Apache License, Version 2.0. See
the [LICENSE](https://raw.githubusercontent.com/bezzad/HybridRedisCache/main/LICENSE) file for more information.
