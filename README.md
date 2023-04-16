[![Build Status](https://ci.appveyor.com/api/projects/status/github/bezzad/HybridRedisCache?branch=master&svg=true)](https://ci.appveyor.com/project/bezzad/HybridRedisCache)
[![NuGet](https://img.shields.io/nuget/dt/HybridRedisCache.svg)](https://www.nuget.org/packages/HybridRedisCache)
[![NuGet](https://img.shields.io/nuget/vpre/HybridRedisCache.svg)](https://www.nuget.org/packages/HybridRedisCache)
[![Generic badge](https://img.shields.io/badge/support-.Net_Core-blue.svg)](https://github.com/bezzad/HybridRedisCache)
[![Generic badge](https://img.shields.io/badge/support-.Net_Standard-blue.svg)](https://github.com/bezzad/HybridRedisCache)

# HybridRedisCache

`HybridRedisCache` is a simple in-memory and Redis hybrid caching solution for .NET applications. 
It provides a way to cache frequently accessed data in memory for fast access and automatically falls back to using Redis as a persistent cache when memory cache capacity is exceeded.

## Installation

You can install the `HybridRedisCache` package using NuGet:

> `PM> Install-Package HybridRedisCache`

## Usage

To use `HybridCache`, you can create an instance of the `HybridCache` class and then call its `Set` and `Get` methods to cache and retrieve data, respectively.
Here's an example:

```csharp
using HybridRedisCache;

...

// Create a new instance of HybridCache with Redis connection string and instance name
var cache = new HybridCache("localhost:6379", "myapp");

// Cache a string value with key "mykey" for 1 minute
cache.Set("mykey", "myvalue", TimeSpan.FromMinutes(1));

// Retrieve the cached value with key "mykey"
var value = cache.Get<string>("mykey");
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