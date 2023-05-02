using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace HybridRedisCache.Test;

public class HybridCacheTests : IDisposable
{
    private ILoggerFactory _loggerFactory;
    private HybridCache _cache;
    private HybridCachingOptions _options;

    public HybridCacheTests()
    {
        _loggerFactory = new LoggerFactoryMock();
        _options = new HybridCachingOptions()
        {
            InstancesSharedName = "my-test-app",
            RedisCacheConnectString = "localhost:6379",
            ThrowIfDistributedCacheError = true
        };
        _cache = new HybridCache(_options, _loggerFactory);
    }

    public void Dispose()
    {
        _cache.Dispose();
        _loggerFactory.Dispose();
    }

    [Fact]
    public void ShouldCacheAndRetrieveData()
    {
        // Arrange
        var key = "mykey";
        var value = "myvalue";

        // Act
        _cache.Set(key, value);
        var result = _cache.Get<string>(key);

        // Assert
        Assert.Equal(value, result);
    }

    [Fact]
    public void SetAndGet_CacheEntryDoesNotExist_ReturnsNull()
    {
        // Arrange
        var key = "nonexistentkey";

        // Act
        var result = _cache.Get<string>(key);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void TryGet_CacheEntryDoesNotExist_ReturnsFalse()
    {
        // Arrange
        var key = "tryget_nonexistentkey";

        // Act
        var result = _cache.TryGetValue<string>(key, out var value);

        // Assert
        Assert.False(result);
        Assert.Null(value);
    }

    [Fact]
    public async Task Set_CacheEntryIsRemoved_AfterExpiration()
    {
        // Arrange
        var key = "mykey";
        var value = "myvalue";

        // Act
        _cache.Set(key, value, TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(100));
        await Task.Delay(TimeSpan.FromSeconds(2));
        var result = _cache.Get<string>(key);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void Remove_CacheEntryIsRemoved()
    {
        // Arrange
        var key = "mykey";
        var value = "myvalue";

        // Act
        _cache.Set(key, value);
        _cache.Remove(key);
        var result = _cache.Get<string>(key);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task SetAndGetAsync_CacheEntryExists_ReturnsCachedValue()
    {
        // Arrange
        var key = "mykey";
        var value = "myvalue";

        // Act
        await _cache.SetAsync(key, value);
        var result = await _cache.GetAsync<string>(key);

        // Assert
        Assert.Equal(value, result);
    }

    [Fact]
    public async Task SetAndGetAsync_CacheEntryDoesNotExist_ReturnsNull()
    {
        // Arrange
        var key = "nonexistentkey";

        // Act
        var result = await _cache.GetAsync<string>(key);

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
        var key = "mykey";
        var value = "myvalue";

        // Act
        await _cache.SetAsync(key, value, TimeSpan.FromSeconds(localExpiry), TimeSpan.FromSeconds(redisExpiry));
        await Task.Delay(TimeSpan.FromSeconds(Math.Max(localExpiry, redisExpiry)));
        var result = await _cache.GetAsync<string>(key);

        // Assert
        Assert.Null(result);
    }

    [Theory]
    [InlineData(100, 200)] // local cache expired before redis cache
    [InlineData(200, 100)] // redis cache expired before local cache
    public async Task SetAsync_LocalCacheEntryIsRemoved_RedisCacheIsExist_AfterExpiration(int localExpiry, int redisExpiry)
    {
        // Arrange
        var key = DateTime.Now.GetHashCode().ToString();
        var value = "myvalue";

        // Act
        await _cache.SetAsync(key, value, TimeSpan.FromMilliseconds(localExpiry), TimeSpan.FromMilliseconds(redisExpiry), false);
        await Task.Delay(Math.Min(localExpiry, redisExpiry));
        var valueAfterLocalExpiration = await _cache.GetAsync<string>(key);
        await Task.Delay(Math.Abs(redisExpiry - localExpiry));
        var valueAfterRedisExpiration = await _cache.GetAsync<string>(key);

        // Assert
        Assert.Equal(value, valueAfterLocalExpiration);
        Assert.Null(valueAfterRedisExpiration);
    }

    [Fact]
    public async Task GetExpirationAsyncTest()
    {
        // Arrange
        var key = "mykey";
        var value = "myvalue";
        var expiryTimeMin = 2;

        // Act
        await _cache.SetAsync(key, value, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(expiryTimeMin), false);
        var expiration = await _cache.GetExpirationAsync(key);

        // Assert
        Assert.Equal(expiryTimeMin, Math.Round(expiration.TotalMinutes));
    }

    [Fact]
    public void GetExpirationTest()
    {
        // Arrange
        var key = "mykey";
        var value = "myvalue";
        var expiryTimeMin = 2;

        // Act
        _cache.Set(key, value, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(expiryTimeMin), false);
        var expiration = _cache.GetExpiration(key);

        // Assert
        Assert.Equal(expiryTimeMin, Math.Round(expiration.TotalMinutes));
    }

    [Theory]
    [InlineData("theKey")]
    [InlineData("  theKey")]
    public async Task RemoveAsync_CacheEntryIsRemoved(string key)
    {
        // Arrange
        var value = "myvalue";

        // Act
        await _cache.SetAsync(key, value);
        await _cache.RemoveAsync(key);
        var result = await _cache.GetAsync<string>(key);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void ShouldSerializeAndDeserializeComplexObject()
    {
        // Arrange
        var obj = new { Name = "John", Age = 30 };

        // Act
        _cache.Set("mykey", obj, TimeSpan.FromMinutes(1));
        var value = _cache.Get<dynamic>("mykey");

        // Assert
        Assert.Equal(obj.Name, value.Name);
        Assert.Equal(obj.Age, value.Age);
    }

    [Fact]
    public async Task TestSharedCache()
    {
        // create two instances of HybridCache that share the same Redis cache
        var instance1 = new HybridCache(_options);
        var instance2 = new HybridCache(_options);

        // set a value in the shared cache using instance1
        instance1.Set("mykey", "myvalue", fireAndForget: false);

        // retrieve the value from the shared cache using instance2
        var value = instance2.Get<string>("mykey");
        Assert.Equal("myvalue", value);

        // update the value in the shared cache using instance2
        instance2.Set("mykey", "newvalue", fireAndForget: false);

        // wait for cache invalidation message to be received
        await Task.Delay(1000);

        // retrieve the updated value from the shared cache using instance1
        value = instance1.Get<string>("mykey");
        Assert.Equal("newvalue", value);

        // clean up
        instance1.Dispose();
        instance2.Dispose();
    }

    [Fact]
    public void TestMultiThreadedCacheOperations()
    {
        // create a list of values to store in the cache
        var values = new List<string> { "foo", "bar", "baz", "qux" };

        // create multiple threads, each of which performs cache operations
        var threads = new List<Thread>();
        for (int i = 0; i < values.Count; i++)
        {
            var thread = new Thread((state) =>
            {
                // retrieve the key and value variables from the state object
                var tuple = (Tuple<string, string>)state;
                var threadKey = tuple.Item1;
                var threadValue = tuple.Item2;

                // perform cache operations on the cache instance
                _cache.Set(threadKey, threadValue);
                var retrievedValue = _cache.Get<string>(threadKey);
                Assert.Equal(threadValue, retrievedValue);
                _cache.Remove(threadKey);
            });

            // create a local copy of the i variable to avoid race conditions
            var localI = i;

            // start the thread and pass the key and value variables as a state object
            thread.Start(Tuple.Create($"key{i}", values[i]));

            // add the thread to the list
            threads.Add(thread);
        }

        // wait for the threads to complete
        threads.ForEach(t => t.Join());

        // clean up
        _cache.Dispose();
    }

    [Fact]
    public void CacheSerializationTest()
    {
        // create a complex object to store in the cache
        var complexObject = new ComplexObject
        {
            Name = "John",
            Age = 30,
            Address = new Address
            {
                Street = "123 Main St",
                City = "Anytown",
                State = "CA",
                Zip = "12345"
            },
            PhoneNumbers = new List<string> { "555-1234", "555-5678" }
        };

        // store the object in the cache
        _cache.Set("complexObject", complexObject);

        // retrieve the object from the cache
        var retrievedObject = _cache.Get<ComplexObject>("complexObject");

        // verify that the retrieved object is equal to the original object
        Assert.Equal(complexObject.Name, retrievedObject.Name);
        Assert.Equal(complexObject.Age, retrievedObject.Age);
        Assert.Equal(complexObject.Address.Street, retrievedObject.Address.Street);
        Assert.Equal(complexObject.Address.City, retrievedObject.Address.City);
        Assert.Equal(complexObject.Address.State, retrievedObject.Address.State);
        Assert.Equal(complexObject.Address.Zip, retrievedObject.Address.Zip);
        Assert.Equal(complexObject.PhoneNumbers, retrievedObject.PhoneNumbers);

        // clean up
        _cache.Dispose();
    }

    [Fact]
    public void CacheConcurrencyTest()
    {
        // create a shared key and a list of values to store in the cache
        var key = "sharedKey";
        var values = new List<string> { "foo", "bar", "baz", "qux" };

        // create multiple threads, each of which performs cache operations
        var threads = new List<Thread>();
        for (int i = 0; i < values.Count; i++)
        {
            var value = values[i];
            var thread = new Thread(() =>
            {
                // wait for the initial value to be set by another thread
                lock (values)
                {
                    // perform cache operations on the cache instance
                    var currentValue = _cache.Get<string>(key);
                    if (currentValue == null)
                    {
                        _cache.Set(key, value, fireAndForget: false);
                    }
                    else
                    {
                        _cache.Set(key, currentValue + value, fireAndForget: false);
                    }
                }
            });

            threads.Add(thread);
        }

        // start the threads and wait for them to complete
        threads.ForEach(t => t.Start());

        // set the initial value in the cache
        _cache.Set(key, values[0], fireAndForget: false);

        // waits for a brief period of time before verifying the final value in the cache
        // to ensure that all write operations have completed.
        Thread.Sleep(1000);

        // verify that the final value in the cache is correct
        var actualValue = _cache.Get<string>(key);
        Assert.True(values.All(val => actualValue.Contains(val)), $"value was:{actualValue}");

        // clean up
        _cache.Dispose();
    }

    [Fact]
    public void ShouldCacheAndExistData()
    {
        // Arrange
        var key = "mykey";
        var value = "myvalue";

        // Act
        _cache.Set(key, value);
        var result = _cache.Exists(key);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task ShouldCacheAndExistDataAsync()
    {
        // Arrange
        var key = "mykey";
        var value = "myvalue";

        // Act
        _cache.Set(key, value);
        var result = await _cache.ExistsAsync(key);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void ShouldCacheGetDataIfAlsoNotExistData()
    {
        // Arrange
        var key = Guid.NewGuid().ToString("N");
        var value = "myvalue";
        string dataRetriever(string key)
        {
            Task.Delay(100).Wait();
            return value;
        };

        // Act
        var firstResult = _cache.Get<string>(key);
        var retrievedResult = _cache.Get(key, dataRetriever);
        var isExist = _cache.Exists(key);

        // Assert
        Assert.Null(firstResult);
        Assert.Equal(value, retrievedResult);
        Assert.True(isExist);
    }

    [Fact]
    public async Task ShouldCacheGetDataIfAlsoNotExistDataAsync()
    {
        // Arrange
        var key = Guid.NewGuid().ToString("N");
        var value = "myvalue";
        async Task<string> dataRetriever(string key)
        {
            await Task.Delay(100);
            return value;
        };

        // Act
        var firstResult = await _cache.GetAsync<string>(key);
        var retrievedResult = await _cache.GetAsync<string>(key, dataRetriever);
        var isExist = await _cache.ExistsAsync(key);

        // Assert
        Assert.Null(firstResult);
        Assert.Equal(value, retrievedResult);
        Assert.True(isExist);
    }

    [Fact]
    public void TestSetGetWithLogging()
    {
        // Arrange
        var key = "#NotExistKey#$#NotExistKey#";
        var realCacheKey = _options.InstancesSharedName + ":" + key;
        _options.EnableLogging = true;
        // Use the ILoggerFactory instance to get the ILogger instance
        var logger = _loggerFactory.CreateLogger(nameof(HybridCache)) as LoggerMock;

        // Act
        // get a key which is not exist. So, throw an exception and it will be logged!
        var _ = _cache.Get<string>(key);

        // Assert
        Assert.True(logger.LogHistory.Any());
        var firstLog = logger.LogHistory.LastOrDefault() as IReadOnlyList<KeyValuePair<string, object>>;
        Assert.Equal($"distributed cache can not get the value of `{key}` key", firstLog.FirstOrDefault().Value.ToString());
    }

    [Fact]
    public void TestSetAll()
    {
        // Arrange
        var keyValues = new Dictionary<string, object>
            {
                { "key1", "value1" },
                { "key2", "value2" },
                { "key3", "value3" }
            };

        // Act
        _cache.SetAll(keyValues, TimeSpan.FromMinutes(10));

        // Assert
        foreach (var kvp in keyValues)
        {
            var value = _cache.Get<object>(kvp.Key);
            Assert.Equal(kvp.Value, value);
        }
    }

    [Fact]
    public async Task TestSetAllAsync()
    {
        // Arrange
        var keyValues = new Dictionary<string, object>
            {
                { "key1", "value1" },
                { "key2", "value2" },
                { "key3", "value3" }
            };

        // Act
        await _cache.SetAllAsync(keyValues, TimeSpan.FromMinutes(10)).ConfigureAwait(false);

        // Assert
        foreach (var kvp in keyValues)
        {
            var value = await _cache.GetAsync<string>(kvp.Key).ConfigureAwait(false);
            Assert.Equal(kvp.Value, value);
        }
    }

    [Fact]
    public void Remove_RemovesMultipleKeysFromCache()
    {
        // Arrange
        var key1 = "key1";
        var key2 = "key2";
        var value1 = "value1";
        var value2 = "value2";
        _options.ThrowIfDistributedCacheError = true;

        _cache.Set(key1, value1);
        _cache.Set(key2, value2);

        // Act
        _cache.Remove(key1, key2);

        // Assert
        Assert.Null(_cache.Get<string>(key1));
        Assert.Null(_cache.Get<string>(key2));
    }

    [Fact]
    public async Task Remove_RemovesMultipleKeysFromCacheAsync()
    {
        // Arrange
        var key1 = "key1";
        var key2 = "key2";
        var value1 = "value1";
        var value2 = "value2";

        _cache.Set(key1, value1);
        _cache.Set(key2, value2);


        // Act
        await _cache.RemoveAsync(key1, key2);

        // Assert
        Assert.Null(_cache.Get<string>(key1));
        Assert.Null(_cache.Get<string>(key2));
    }

    [Fact]
    public void FlushDbTest()
    {
        // Arrange
        var key1 = "key1";
        var key2 = "key2";
        var value1 = "value1";
        var value2 = "value2";

        _cache.Set(key1, value1, fireAndForget: false);
        _cache.Set(key2, value2, fireAndForget: false);

        // Act
        var value1b = _cache.Get<string>(key1);
        var value2b = _cache.Get<string>(key2);
        _cache.ClearAll();

        // Assert
        Assert.Equal(value1, value1b);
        Assert.Equal(value2, value2b);
        Assert.Null(_cache.Get<string>(key1));
        Assert.Null(_cache.Get<string>(key2));
    }

    [Fact]
    public async Task FlushDbAsyncTest()
    {
        // Arrange
        var key1 = "key1";
        var key2 = "key2";
        var value1 = "value1";
        var value2 = "value2";

        await _cache.SetAsync(key1, value1, fireAndForget: false);
        await _cache.SetAsync(key2, value2, fireAndForget: false);

        // Act
        var value1b = await _cache.GetAsync<string>(key1);
        var value2b = await _cache.GetAsync<string>(key2);
        await _cache.ClearAllAsync();

        // Assert
        Assert.Equal(value1, value1b);
        Assert.Equal(value2, value2b);
        Assert.Null(await _cache.GetAsync<string>(key1));
        Assert.Null(await _cache.GetAsync<string>(key2));
    }
}