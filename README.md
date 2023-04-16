# HybridRedisCache

`HybridRedisCache` is a simple in-memory and Redis hybrid caching solution for .NET applications. 
It provides a way to cache frequently accessed data in memory for fast access and automatically falls back to using Redis as a persistent cache when memory cache capacity is exceeded.

## Installation

You can install the `HybridRedisCache` package using NuGet:

PM> Install-Package HybridRedisCache

## Usage

To use `HybridRedisCache`, you can create an instance of the `HybridRedisCache` class and then call its `Set` and `Get` methods to cache and retrieve data, respectively.
Here's an example:

```csharp
using HybridRedisCache;

...

// Create a new instance of HybridRedisCache with Redis connection string and instance name
var cache = new HybridCache("localhost:6379", "myapp");

// Cache a string value with key "mykey" for 1 minute
cache.Set("mykey", "myvalue", TimeSpan.FromMinutes(1));

// Retrieve the cached value with key "mykey"
var value = cache.Get<string>("mykey");
```

## Contributing

Contributions are welcome! If you find a bug or have a feature request, please open an issue or submit a pull request.
If you'd like to contribute to HybridCache, please follow these steps:

1. Fork the repository.
2. Create a new branch for your changes.
3. Make your changes and commit them.
4. Push your changes to your fork.
5. Submit a pull request.

## License

`HybridCache` is licensed under the Apache License, Version 2.0. See the [LICENSE](https://raw.githubusercontent.com/bezzad/HybridCache/dev/LICENSE) file for more information.