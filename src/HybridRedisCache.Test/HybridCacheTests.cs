using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace HybridRedisCache.Test;

public class HybridCacheTests : IDisposable
{
    private static string UniqueKey => "test_Key_" + Guid.NewGuid().ToString("N");
    private static readonly ILoggerFactory LoggerFactory = new LoggerFactoryMock();

    private readonly HybridCachingOptions _options = new()
    {
        InstancesSharedName = "my-test-app",
        RedisConnectString = "localhost:6379",
        ThrowIfDistributedCacheError = true,
        AbortOnConnectFail = false,
        ConnectRetry = 3,
        FlushLocalCacheOnBusReconnection = false,
        AllowAdmin = true,
        SyncTimeout = 5000,
        AsyncTimeout = 5000,
        KeepAlive = 60,
        ConnectionTimeout = 5000,
    };

    private HybridCache _cache;

    // Lazy Cache: options change inner methods and after that create Cache with first call
    private HybridCache Cache => _cache ??= new HybridCache(_options, LoggerFactory);

    public void Dispose()
    {
        Cache.Dispose();
        LoggerFactory.Dispose();
    }

    [Fact]
    public void ShouldCacheAndRetrieveData()
    {
        // Arrange
        var key = UniqueKey;
        const string value = "Value";

        // Act
        Cache.Set(key, value);
        var result = Cache.Get<string>(key);

        // Assert
        Assert.Equal(value, result);
    }

    [Fact]
    public async Task ShouldCacheAndRetrieveDataFromRedis()
    {
        // Arrange
        var key = UniqueKey;
        const string value = "Value";

        // Act
        await Cache.SetAsync(key, value, TimeSpan.FromTicks(1), TimeSpan.FromSeconds(600), false);
        await Task.Delay(100);
        var result = await Cache.GetAsync<string>(key);

        // Assert
        Assert.Equal(value, result);
    }

    [Fact]
    public async Task ShouldCacheNumberAndRetrieveData()
    {
        // Arrange
        var key = UniqueKey;
        var value = 12345679.5;

        // Act
        await Cache.SetAsync(key, value, TimeSpan.FromTicks(1), TimeSpan.FromSeconds(600), false);
        await Task.Delay(100);
        var result = await Cache.GetAsync<double>(key);

        // Assert
        Assert.Equal(value, result);
    }

    [Fact]
    public void SetAndGet_CacheEntryDoesNotExist_ReturnsNull()
    {
        // Arrange
        var key = UniqueKey;

        // Act
        var result = Cache.Get<string>(key);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void TryGetValue_CacheEntryDoesNotExist_ReturnFalse()
    {
        // Arrange
        var key = UniqueKey;

        // Act
        var result = Cache.TryGetValue<object>(key, out var value);

        // Assert
        Assert.False(result);
        Assert.Null(value);
    }

    [Fact]
    public async Task Set_CacheEntryIsRemoved_AfterExpiration()
    {
        // Arrange
        var key = UniqueKey;
        const string value = "Value";

        // Act
        await Cache.SetAsync(key, value, TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(100));
        await Task.Delay(TimeSpan.FromSeconds(2));
        var result = Cache.Get<string>(key);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void Remove_CacheEntryIsRemoved()
    {
        // Arrange
        var key = UniqueKey;
        const string value = "value";

        // Act
        Cache.Set(key, value);
        Cache.Remove(key);
        var result = Cache.Get<string>(key);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task SetAndGetAsync_CacheEntryExists_ReturnsCachedValue()
    {
        // Arrange
        var key = UniqueKey;
        const string value = "Value";

        // Act
        await Cache.SetAsync(key, value);
        var result = await Cache.GetAsync<string>(key);

        // Assert
        Assert.Equal(value, result);
    }

    [Fact]
    public async Task SetAndGetAsync_CacheEntryDoesNotExist_ReturnsNull()
    {
        // Arrange
        var key = UniqueKey;

        // Act
        var result = await Cache.GetAsync<string>(key);

        // Assert
        Assert.Null(result);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(1, 2)]
    [InlineData(2, 1)]
    public async Task SetAsync_CacheEntryIsRemoved_AfterExpiration(int localExpiry, int redisExpiry)
    {
        // Arrange
        var key = UniqueKey;
        const string value = "Value";

        // Act
        await Cache.SetAsync(key, value, TimeSpan.FromSeconds(localExpiry), TimeSpan.FromSeconds(redisExpiry),
            fireAndForget: false);
        await Task.Delay(TimeSpan.FromSeconds(Math.Max(localExpiry, redisExpiry)));
        var result = await Cache.GetAsync<string>(key);

        // Assert
        Assert.Null(result);
    }

    [Theory]
    [InlineData(1500, 1700)] // local cache expired before redis cache
    [InlineData(200, 100)] // redis cache expired before local cache
    public async Task SetAsync_LocalCacheEntryIsRemoved_RedisCacheIsExist_AfterExpiration(int localExpiry,
        int redisExpiry)
    {
        // Arrange
        var key = UniqueKey;
        const string value = "Value";

        // Act
        await Cache.SetAsync(key, value, TimeSpan.FromMilliseconds(localExpiry),
            TimeSpan.FromMilliseconds(redisExpiry),
            false);
        await Task.Delay(Math.Min(localExpiry, redisExpiry));
        var valueAfterLocalExpiration = await Cache.GetAsync<string>(key);
        await Task.Delay(localExpiry + redisExpiry);
        var valueAfterRedisExpiration = await Cache.GetAsync<string>(key);

        // Assert
        Assert.Equal(value, valueAfterLocalExpiration);
        Assert.Null(valueAfterRedisExpiration);
    }

    [Fact]
    public async Task GetExpirationAsyncTest()
    {
        // Arrange
        var key = UniqueKey;
        const string value = "Value";
        var expiryTimeMin = 2;

        // Act
        await Cache.SetAsync(key, value, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(expiryTimeMin), false);
        var expiration = await Cache.GetExpirationAsync(key);

        // Assert
        Assert.Equal(expiryTimeMin, Math.Round(expiration?.TotalMinutes ?? 0));
    }

    [Fact]
    public void GetExpirationTest()
    {
        // Arrange
        var key = UniqueKey;
        const string value = "Value";
        var expiryTimeMin = 2;

        // Act
        Cache.Set(key, value, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(expiryTimeMin), false);
        var expiration = Cache.GetExpiration(key);

        // Assert
        Assert.Equal(expiryTimeMin, Math.Round(expiration?.TotalMinutes ?? 0));
    }

    [Theory]
    [InlineData("theKey#1234")]
    [InlineData("  theKey#1234   ")]
    public async Task RemoveAsync_CacheEntryIsRemoved(string key)
    {
        // Arrange
        const string value = "Value";

        // Act
        await Cache.SetAsync(key, value);
        await Cache.RemoveAsync(key);
        var result = await Cache.GetAsync<string>(key);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void ShouldSerializeAndDeserializeComplexObject()
    {
        // Arrange
        var key = UniqueKey;
        var obj = new { Name = "John", Age = 30 };

        // Act
        Cache.Set(key, obj, TimeSpan.FromMinutes(1));
        var value = Cache.Get<dynamic>(key);

        // Assert
        Assert.Equal(obj.Name, value.Name);
        Assert.Equal(obj.Age, value.Age);
    }

    [Fact]
    public async Task TestSharedCache()
    {
        // Arrange
        var key = UniqueKey;
        var value1 = "myValue1";
        var value2 = "newValue2";

        // create two instances of HybridCache that share the same Redis cache
        var instance1 = new HybridCache(_options);
        var instance2 = new HybridCache(_options);

        // set a value in the shared cache using instance1
        await instance1.SetAsync(key, value1);

        // retrieve the value from the shared cache using instance2
        var value = await instance2.GetAsync<string>(key);
        Assert.Equal(value1, value);

        // update the value in the shared cache using instance2
        await instance2.SetAsync(key, value2);

        // wait for cache invalidation message to be received
        await Task.Delay(1000);

        // retrieve the updated value from the shared cache using instance1
        value = await instance1.GetAsync<string>(key);
        Assert.Equal(value2, value);

        // clean up
        instance1.Dispose();
        instance2.Dispose();
    }

    [Fact]
    public void TestMultiThreadedCacheOperations()
    {
        // create multiple threads, each of which performs cache operations
        var threads = new List<Thread>();
        for (int i = 0; i < 100; i++)
        {
            var thread = new Thread(() =>
            {
                // retrieve the key and value variables from the state object
                var threadKey = UniqueKey;
                var threadValue = "test_threading_value";

                // perform cache operations on the cache instance
                Cache.Set(threadKey, threadValue);
                Thread.Sleep(10);
                var retrievedValue = Cache.Get<string>(threadKey);
                Assert.Equal(threadValue, retrievedValue);
                Cache.Remove(threadKey);
            });

            // create a local copy of the i variable to avoid race conditions
            var localI = i;

            // start the thread and pass the key and value variables as a state object
            thread.Start();

            // add the thread to the list
            threads.Add(thread);
        }

        // wait for the threads to complete
        threads.ForEach(t => t.Join());

        // clean up
        Cache.Dispose();
    }

    [Fact]
    public void CacheSerializationTest()
    {
        // Arrange
        var key = UniqueKey;
        var complexValue = new ComplexObject
        {
            Name = "John",
            Age = 30,
            Address = new Address
            {
                Street = "123 Main St",
                City = "AnyTown",
                State = "CA",
                Zip = "12345"
            },
            PhoneNumbers = ["555-1234", "555-5678"]
        };

        // Act
        Cache.Set(key, complexValue);
        var retrievedObject = Cache.Get<IComplexObject>(key);

        // Assert
        // verify that the retrieved object is equal to the original object
        Assert.Equal(complexValue.Name, retrievedObject.Name);
        Assert.Equal(complexValue.Age, retrievedObject.Age);
        Assert.Equal(complexValue.Address.Street, retrievedObject.Address.Street);
        Assert.Equal(complexValue.Address.City, retrievedObject.Address.City);
        Assert.Equal(complexValue.Address.State, retrievedObject.Address.State);
        Assert.Equal(complexValue.Address.Zip, retrievedObject.Address.Zip);
        Assert.Equal(complexValue.PhoneNumbers, retrievedObject.PhoneNumbers);
    }

    [Fact]
    public async Task CacheDeserializationFromRedisStringTest()
    {
        // Arrange
        var key = UniqueKey;
        var complexValue = new ComplexObject
        {
            Name = "John",
            Age = 30,
            Address = new Address
            {
                Street = "123 Main St",
                City = "AnyTown",
                State = "CA",
                Zip = "12345"
            },
            PhoneNumbers = ["555-1234", "555-5678"]
        };

        // Act
        await Cache.SetAsync(key, complexValue, TimeSpan.FromTicks(1), TimeSpan.FromSeconds(60), false);
        await Task.Delay(100);
        var retrievedObject = Cache.Get<IComplexObject>(key);

        // Assert
        // verify that the retrieved object is equal to the original object
        Assert.Equal(complexValue.Name, retrievedObject.Name);
        Assert.Equal(complexValue.Age, retrievedObject.Age);
        Assert.Equal(complexValue.Address.Street, retrievedObject.Address.Street);
        Assert.Equal(complexValue.Address.City, retrievedObject.Address.City);
        Assert.Equal(complexValue.Address.State, retrievedObject.Address.State);
        Assert.Equal(complexValue.Address.Zip, retrievedObject.Address.Zip);
        Assert.Equal(complexValue.PhoneNumbers, retrievedObject.PhoneNumbers);
    }

    [Fact]
    public async Task CacheConcurrencyTest()
    {
        // create a shared key and a list of values to store in the cache
        var key = UniqueKey;
        var values = new List<string> { "foo", "bar", "baz", "qux", "esx", "rdc", "tfv", "ygb", "uhn", "ijm" };
        var locker = new SemaphoreSlim(1);

        // create multiple tasks, each of which performs cache operations

        // to ensure that all write operations have completed.
        await Task.WhenAll(values.Select(GetSetTask).ToArray());

        // verify that the final value in the cache is correct
        var actualValue = Cache.Get<string>(key);
        Assert.True(values.All(val => actualValue.Contains(val)), $"value was:{actualValue}");

        // clean up
        Cache.Dispose();
        return;

        async Task GetSetTask(string value)
        {
            // wait for the initial value to be set by another thread
            try
            {
                await locker.WaitAsync();
                // perform cache operations on the cache instance
                var currentValue = Cache.Get<string>(key);
                await Cache.SetAsync(key, (currentValue ?? "") + value);
            }
            finally
            {
                locker.Release();
            }
        }
    }

    [Fact]
    public void ShouldCacheAndExistData()
    {
        // Arrange
        var key = UniqueKey;
        const string value = "Value";

        // Act
        Cache.Set(key, value);
        var result = Cache.Exists(key);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task ShouldCacheAndExistDataAsync()
    {
        // Arrange
        var key = UniqueKey;
        const string value = "Value";

        // Act
        await Cache.SetAsync(key, value);
        var result = await Cache.ExistsAsync(key);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void ShouldCacheGetDataIfAlsoNotExistData()
    {
        // Arrange
        var cacheKey = UniqueKey;
        const string value = "value";

        string DataRetriever(string key)
        {
            return value;
        }

        // Act
        var firstResult = Cache.Get<string>(cacheKey);
        var retrievedResult = Cache.Get(cacheKey, DataRetriever);
        var isExist = Cache.Exists(cacheKey);

        // Assert
        Assert.Null(firstResult);
        Assert.Equal(value, retrievedResult);
        Assert.True(isExist);
    }

    [Fact]
    public async Task ShouldCacheGetDataIfAlsoNotExistDataAsync()
    {
        // Arrange
        var cacheKey = UniqueKey;
        const string value = "Value";

        async Task<string> DataRetriever(string key)
        {
            await Task.Delay(100);
            return value;
        }

        // Act
        var firstResult = await Cache.GetAsync<string>(cacheKey);
        var retrievedResult = await Cache.GetAsync(cacheKey, DataRetriever);
        var isExist = await Cache.ExistsAsync(cacheKey);

        // Assert
        Assert.Null(firstResult);
        Assert.Equal(value, retrievedResult);
        Assert.True(isExist);
    }

    [Fact]
    public void TestSetGetWithLogging()
    {
        // Arrange
        var key = UniqueKey;
        _options.EnableLogging = true;
        // Use the ILoggerFactory instance to get the ILogger instance
        var logger = LoggerFactory.CreateLogger(nameof(HybridCache)) as LoggerMock;

        // Act
        // get a key which is not exist. So, throw an exception and it will be logged!
        _ = Cache.Get<string>(key);

        // Assert
        Assert.True(logger?.LogHistory.Any());
        var firstLog = logger?.LogHistory.LastOrDefault() as IReadOnlyList<KeyValuePair<string, object>>;
        Assert.Equal($"distributed cache can not get the value of `{key}` key",
            firstLog?.FirstOrDefault().Value.ToString());
    }

    [Fact]
    public void TestSetAll()
    {
        // Arrange
        var keyValues = new Dictionary<string, object>
        {
            { UniqueKey, "value1" },
            { UniqueKey, "value2" },
            { UniqueKey, "value3" }
        };

        // Act
        Cache.SetAll(keyValues, TimeSpan.FromMinutes(10));

        // Assert
        foreach (var kvp in keyValues)
        {
            var value = Cache.Get<object>(kvp.Key);
            Assert.Equal(kvp.Value, value);
        }
    }

    [Fact]
    public async Task TestSetAllAsync()
    {
        // Arrange
        var keyValues = new Dictionary<string, object>
        {
            { UniqueKey, "value1" },
            { UniqueKey, "value2" },
            { UniqueKey, "value3" }
        };

        // Act
        await Cache.SetAllAsync(keyValues, TimeSpan.FromMinutes(10));

        // Assert
        foreach (var kvp in keyValues)
        {
            var value = await Cache.GetAsync<string>(kvp.Key);
            Assert.Equal(kvp.Value, value);
        }
    }

    [Fact]
    public void Remove_RemovesMultipleKeysFromCache()
    {
        // Arrange
        var key1 = UniqueKey;
        var key2 = UniqueKey;
        var value1 = "value1";
        var value2 = "value2";
        _options.ThrowIfDistributedCacheError = true;

        Cache.Set(key1, value1);
        Cache.Set(key2, value2);

        // Act
        Cache.Remove([key1, key2]);

        // Assert
        Assert.Null(Cache.Get<string>(key1));
        Assert.Null(Cache.Get<string>(key2));
    }

    [Fact]
    public async Task RemovesMultipleKeysFromCacheAsync()
    {
        // Arrange
        var key1 = UniqueKey;
        var key2 = UniqueKey;
        var value1 = "value1";
        var value2 = "value2";

        await Cache.SetAsync(key1, value1);
        await Cache.SetAsync(key2, value2);

        // Act
        await Cache.RemoveAsync([key1, key2]);

        // Assert
        Assert.Null(Cache.Get<string>(key1));
        Assert.Null(Cache.Get<string>(key2));
    }

    [Fact]
    public void FlushDbTest()
    {
        // Arrange
        var key1 = UniqueKey;
        var key2 = UniqueKey;
        var value1 = "value1";
        var value2 = "value2";

        Cache.Set(key1, value1);
        Cache.Set(key2, value2);

        // Act
        var value1B = Cache.Get<string>(key1);
        var value2B = Cache.Get<string>(key2);
        Cache.ClearAll();

        // Assert
        Assert.Equal(value1, value1B);
        Assert.Equal(value2, value2B);
        Assert.Null(Cache.Get<string>(key1));
        Assert.Null(Cache.Get<string>(key2));
    }

    [Fact]
    public async Task FlushDbAsyncTest()
    {
        // Arrange
        var key1 = UniqueKey;
        var key2 = UniqueKey;
        var value1 = "value1";
        var value2 = "value2";

        await Cache.SetAsync(key1, value1);
        await Cache.SetAsync(key2, value2);

        // Act
        var value1B = await Cache.GetAsync<string>(key1);
        var value2B = await Cache.GetAsync<string>(key2);
        await Cache.ClearAllAsync();

        // Assert
        Assert.Equal(value1, value1B);
        Assert.Equal(value2, value2B);
        Assert.Null(await Cache.GetAsync<string>(key1));
        Assert.Null(await Cache.GetAsync<string>(key2));
    }

    [Fact]
    public async Task FlushLocalDbAsyncTest()
    {
        // Arrange
        var key1 = UniqueKey;
        var key2 = UniqueKey;
        var value1 = "value1";
        var value2 = "value2";

        await Cache.SetAsync(key1, value1, TimeSpan.FromMinutes(1),
            TimeSpan.FromMilliseconds(1)); // without redis caching
        await Cache.SetAsync(key2, value2, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1), fireAndForget: false);

        // Act
        var value1B = await Cache.GetAsync<string>(key1);
        var value2B = await Cache.GetAsync<string>(key2);
        await Cache.FlushLocalCachesAsync();
        await Task.Delay(100);
        var value1A = await Cache.GetAsync<string>(key1);
        var value2A = await Cache.GetAsync<string>(key2);

        // Assert
        Assert.Equal(value1, value1B);
        Assert.Equal(value2, value2B);
        Assert.Equal(value2, value2A); // read from redis
        Assert.Null(value1A); // read from redis, but also redis has been expired
    }

    [Fact]
    [SuppressMessage("ReSharper", "MethodHasAsyncOverload")]
    public async Task FlushLocalDbTest()
    {
        // Arrange
        var key1 = UniqueKey;
        var key2 = UniqueKey;
        const string value1 = "value1";
        const string value2 = "value2";

        Cache.Set(key1, value1, TimeSpan.FromMinutes(1), TimeSpan.FromMilliseconds(1));
        Cache.Set(key2, value2, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));

        // Act
        var value1B = Cache.Get<string>(key1);
        var value2B = Cache.Get<string>(key2);
        Cache.FlushLocalCaches(); // Just test Sync method
        await Task.Delay(100);
        var value1A = Cache.Get<string>(key1);
        var value2A = Cache.Get<string>(key2);

        // Assert
        Assert.Equal(value1, value1B);
        Assert.Equal(value2, value2B);
        Assert.Equal(value2, value2A); // read from redis
        Assert.Null(value1A); // read from redis, but also redis has been expired
    }

    [Fact]
    public async Task TestSearchPatternsWhenCacheMultipleSamenessKeys()
    {
        // Arrange
        var keyPattern = "key_#";
        var valuePattern = "value_";
        var keyValues = new Dictionary<string, string>();
        for (int i = 0; i < 1000; i++)
        {
            keyValues.Add(keyPattern + i, valuePattern + i);
        }

        // Act
        await Cache.SetAllAsync(keyValues, new HybridCacheEntry()
        {
            RedisExpiry = TimeSpan.FromMinutes(1),
            FireAndForget = false,
            LocalCacheEnable = false
        });

        // Assert
        for (int i = 0; i < 1000; i++)
        {
            var value = await Cache.GetAsync<string>(keyPattern + i);
            Assert.Equal(valuePattern + i, value);
        }
    }

    [Fact]
    public async Task TestCacheOnRedisOnly()
    {
        // Arrange
        var key = UniqueKey;
        var localValue = "test_local_value";
        var redisValue = "test_redis_value";

        // Act
        await Cache.SetAsync(key, localValue, new HybridCacheEntry()
        {
            RedisExpiry = TimeSpan.FromMinutes(1),
            LocalExpiry = TimeSpan.FromMilliseconds(500),
            FireAndForget = false,
            LocalCacheEnable = true,
            RedisCacheEnable = false
        });

        await Cache.SetAsync(key, redisValue, new HybridCacheEntry()
        {
            RedisExpiry = TimeSpan.FromMinutes(1),
            LocalExpiry = TimeSpan.FromMinutes(1),
            FireAndForget = false,
            LocalCacheEnable = false,
            RedisCacheEnable = true
        });

        var local = await Cache.GetAsync<object>(key); // get local value
        await Task.Delay(500); // wait to expire local cache
        var redis = await Cache.GetAsync<object>(key); // Now, get Redis cache

        // Assert
        Assert.Equal(localValue, local);
        Assert.Equal(redisValue, redis);
    }

    [Fact]
    public async Task TestSearchKeysWithPattern()
    {
        // Arrange
        var keyPattern = "keyPatternX_";
        var value = "test_value";
        var foundKeys = new List<string>();

        // Act
        for (var i = 0; i < 10; i++)
        {
            await Cache.SetAsync(keyPattern + i, value, new HybridCacheEntry()
            {
                FireAndForget = false,
                LocalCacheEnable = false,
                RedisCacheEnable = true
            });
        }

        await foreach (var key in Cache.KeysAsync(keyPattern + "*"))
        {
            // Search with pattern
            foundKeys.Add(key);
        }

        // Assert
        Assert.Equal(10, foundKeys.Count);
        for (var i = 0; i < 10; i++)
            Assert.True(foundKeys.IndexOf(_options.InstancesSharedName + ":" + keyPattern + i) >= 0);
    }

    [Fact]
    public async Task TestSearchKeysWithInnerPattern()
    {
        // Arrange
        var keyPattern = "keyPattern_{0}_X";
        var value = "test_value";
        var foundKeys = new List<string>();

        // Act
        for (var i = 0; i < 10; i++)
        {
            await Cache.SetAsync(string.Format(keyPattern, i), value, new HybridCacheEntry()
            {
                FireAndForget = false,
                LocalCacheEnable = false,
                RedisCacheEnable = true
            });
        }

        await foreach (var key in Cache.KeysAsync("*" + string.Format(keyPattern, "*"))) // "*keyPattern_*_X"
        {
            // Search with pattern
            foundKeys.Add(key);
        }

        // Assert
        Assert.Equal(10, foundKeys.Count);
        for (var i = 0; i < 10; i++)
            Assert.True(foundKeys.IndexOf(_options.InstancesSharedName + ":" + string.Format(keyPattern, i)) >= 0);
    }

    [Fact]
    public async Task TestRemoveWithPatternAsync()
    {
        // Arrange 
        var key = "key_";
        var keyPattern = key + "*";
        var value = "test_value_";

        // Act
        for (var i = 0; i < 10; i++)
        {
            await Cache.SetAsync(key + i, value, new HybridCacheEntry()
            {
                FireAndForget = false,
                LocalCacheEnable = false,
                RedisCacheEnable = true
            });
        }

        await Cache.RemoveWithPatternAsync(keyPattern);

        // Assert
        for (var i = 0; i < 10; i++)
        {
            var result = await Cache.GetAsync<string>(key + i);
            Assert.Null(result);
        }
    }

    [Fact]
    public async Task TestRemoveWithNotExistPatternAsync()
    {
        // Arrange 
        var key = "key_" + Guid.NewGuid().ToString("N");

        // Act
        await Cache.RemoveWithPatternAsync(key);
        var result = await Cache.GetAsync<string>(key);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task PingAsyncTest()
    {
        // act
        var duration = await Cache.PingAsync();

        // assert
        Assert.True(duration.TotalMilliseconds > 0);
    }

    [Fact]
    public async Task DataRetrieverWhenValueIsPrimitiveTypeTest()
    {
        var key = UniqueKey;
        const int value = 12345;

        // act
        var cachedValue = await Cache.GetAsync(key, _ => Task.FromResult(value));

        // assert
        Assert.Equal(value, cachedValue);
    }

    [Fact]
    public void TryGetWhenValueIsPrimitiveTypeTest()
    {
        var key = UniqueKey;

        // act
        var isSuccess = Cache.TryGetValue(key, out int _);

        // assert
        Assert.False(isSuccess);
    }

    [Fact]
    public async Task ShouldRetryOnConnectionFailure()
    {
        // arrange 
        var delayPerRetry = 1000;
        var retryCount = 3;
        _options.RedisConnectString = "localhost:80"; //Invalid Redis Connection (On Purpose)
        _options.ConnectionTimeout = delayPerRetry;
        _options.ConnectRetry = retryCount;

        // act
        var stopWatch = Stopwatch.StartNew();

        try
        {
            await Cache.PingAsync();
        }
        catch
        {
            stopWatch.Stop();
        }

        // assert
        Assert.True(stopWatch.ElapsedMilliseconds >= delayPerRetry * retryCount,
            $"Actual value {stopWatch.ElapsedMilliseconds}");
    }

    [Fact]
    public async Task TestLocalExpirationAfterSecondRecacheFromRedisWithNewExpiry()
    {
        var key = UniqueKey;
        const int value = 1213;
        var localExpire = TimeSpan.FromSeconds(1);
        var nearToRedisExpire = TimeSpan.FromSeconds(3);
        var redisExpire = TimeSpan.FromSeconds(4);

        // act
        await Cache.SetAsync(key, value, localExpire, redisExpire, false);

        // wait 3sec to near to redis expiry time (local cache expired)
        await Task.Delay(nearToRedisExpire);

        // fetch redis value and update local cache with new expiration time
        var isSuccess = Cache.TryGetValue(key, out int _);

        // Wait 1 second to make sure that Redis has also expired and
        // the local cache should not be alive.
        await Task.Delay(localExpire);
        var isFailed = Cache.TryGetValue(key, out int _);

        // assert
        Assert.True(isSuccess);
        Assert.False(isFailed);
    }

    [Fact]
    public async Task TestSetRedisExpiryTime()
    {
        var key = UniqueKey;
        var value = 1213;
        var entry = new HybridCacheEntry()
        {
            LocalCacheEnable = false,
            RedisCacheEnable = true,
            FireAndForget = false
        };
        entry.SetRedisExpiryUtcTime(DateTime.UtcNow.AddSeconds(1).ToString("T"));

        // act
        await Cache.SetAsync(key, value, entry);

        // wait 1sec 
        await Task.Delay(TimeSpan.FromSeconds(1));
        var isSuccess = Cache.TryGetValue(key, out int _);

        // assert
        Assert.False(isSuccess);
    }

    [Theory]
    [InlineData(1000, 100)]
    [InlineData(10_000, 100)]
    [InlineData(10_000, 1000)]
    [InlineData(10_000, 10_000)]
    [InlineData(10_000, 500)]
    [InlineData(10_000, 5000)]
    [InlineData(10_000, 15_000)]
    [InlineData(100_000, 1000)]
    [SuppressMessage("ReSharper", "StringLiteralTypo")]
    public async Task TestRemoveWithPatternKeysPerformance(int insertCount, int batchRemovePackSize)
    {
        // Arrange
        var keyPattern = "[Tt]est[Rr]emove[Ww]ith[Pp]attern?*";
        var keyFormats = new[]
        {
            "TestRemovewithPattern:{id}",
            "testRemovewithPattern {id}",
            "TestremovewithPattern-{id}",
            "testremovewithPattern={id}",
            "TestRemoveWithPattern.{id}",
            "testRemoveWithpattern_{id}",
            "TestremoveWithPattern_{id}",
            "testremoveWithPattern:{id}",
            "TestRemoveWithPattern_{id}",
        };
        var keyValues = new Dictionary<string, string>();

        for (var i = 0; i < insertCount; i++)
        {
            var key =
                Random.Shared.GetItems(keyFormats, 1)
                    .First()
                    .Replace("{id}", Guid.NewGuid().ToString());
            keyValues.Add(key, key);
        }

        // Act
        // Clear database first
        await Cache.ClearAllAsync();

        // Insert Keys
        await Cache.SetAllAsync(keyValues, new HybridCacheEntry()
        {
            RedisExpiry = TimeSpan.FromMinutes(5),
            FireAndForget = false,
            LocalCacheEnable = false,
            RedisCacheEnable = true,
            KeepTTL = false,
            Flags = Flags.PreferMaster,
            When = Condition.Always
        });

        // Remove keys
        var sw = Stopwatch.StartNew();
        var removedKeysCount = await Cache.RemoveWithPatternAsync(keyPattern,
            flags: Flags.FireAndForget | Flags.PreferReplica,
            batchRemovePackSize: batchRemovePackSize);
        sw.Stop();

        // Assert
        Assert.True(keyValues.Count <= removedKeysCount);

        // Are keys removed?
        foreach (var keyValue in keyValues)
        {
            var isExist = await Cache.ExistsAsync(keyValue.Key);
            Assert.False(isExist, $"The key {keyValue.Key} is still exist!");
        }

        Assert.True(sw.ElapsedMilliseconds < (insertCount / 100) + 50,
            $"Remove keys with pattern duration is {sw.ElapsedMilliseconds}ms");
    }

    [Fact]
    [SuppressMessage("ReSharper", "StringLiteralTypo")]
    public async Task TestGetKeysWithPattern()
    {
        // Arrange
        var dataCount = 1_000;
        var keyPattern = "[Pp]attern[Kk]eys[0-9]?*";
        var keyFormats = new[]
        {
            "PatternKeys{i}:{id}",
            "patternkeys{i}-{id}",
            "patternKeys{i}_{id}",
            "Patternkeys{i} {id}",
            "patternkeys{i}={id}",
            "patternkeys{i}#{id}",
            "patternkeys{i}@{id}"
        };
        var keyValues = new Dictionary<string, string>();
        for (int i = 0; i < dataCount; i++)
        {
            var key =
                Random.Shared.GetItems(keyFormats, 1)
                    .First()
                    .Replace("{i}", i.ToString())
                    .Replace("{id}", Guid.NewGuid().ToString());
            keyValues.Add(key, key);
        }

        // Act
        // Clear database first
        await Cache.ClearAllAsync();

        // Insert Keys
        await Cache.SetAllAsync(keyValues, new HybridCacheEntry()
        {
            RedisExpiry = TimeSpan.FromMinutes(5),
            FireAndForget = false,
            LocalCacheEnable = false,
            RedisCacheEnable = true,
            KeepTTL = false,
            Flags = Flags.PreferMaster,
            When = Condition.Always
        });

        // Get Keys
        var keys = new List<string>();
        await foreach (var key in Cache.KeysAsync(keyPattern))
        {
            keys.Add(key);
        }

        // Assert
        Assert.Equal(dataCount, keys.Count);
        foreach (var key in keys)
        {
            Assert.True(keyValues.ContainsKey(key.Replace(_options.InstancesSharedName + ":", "")),
                $"The key {key} is not Exist!");
        }
    }


    [Fact]
    public async Task TestKeepTtl()
    {
        // Arrange
        var key = "TestKeepTtl:1";
        var expiryTime = TimeSpan.FromSeconds(20);
        var expityTime2 = TimeSpan.FromSeconds(300);
        var option = new HybridCacheEntry()
        {
            RedisExpiry = expiryTime,
            LocalCacheEnable = false,
            RedisCacheEnable = true,
            Flags = Flags.PreferMaster,
            When = Condition.Always,
            KeepTTL = false
        };
        
        // Action
        await Cache.SetAsync(key, key, option);
        option.RedisExpiry = expityTime2;
        option.KeepTTL = true; // keep Redis expire time in the old time: 20
        await Cache.SetAsync(key, key, option);
        var actualTime = await Cache.GetExpirationAsync(key);
        
        // Assert
        Assert.True(actualTime <= expiryTime, $"This actual expire time is: {actualTime}");        
    }
    
    
    // TODO
    // TEST WHEN.NotExist
    // TEST Flags.PreferReplica
    // Release new version
}