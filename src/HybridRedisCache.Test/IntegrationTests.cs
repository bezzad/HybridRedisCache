using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace HybridRedisCache.Test;

[Collection("Sequential")]
public class IntegrationTests(ITestOutputHelper testOutputHelper) : BaseCacheTest(testOutputHelper)
{
    [Theory(Timeout = 10_000)]
    [InlineData(true)]
    [InlineData(false)]
    public async Task TestSharedCache(bool localCacheEnable)
    {
        // Arrange
        var key = "TestSharedCache_" + UniqueKey;
        var value1 = "oldValue";
        var value2 = "newValue";
        var locker = new SemaphoreSlim(0, 1); // semaphore to wait for cache invalidate message

        // create two instances of HybridCache that share the same Redis cache
        var instance1 = new HybridCache(Options);
        var instance2 = new HybridCache(Options);
        var expiry = TimeSpan.FromSeconds(50);
        instance1.OnRedisBusMessage += (k, type) =>
        {
            if (key == k && type == MessageType.SetCache)
            {
                // release the semaphore when a cache invalidate message is received
                locker.Release();
            }
        };

        await instance1.SetAsync(key, value1, expiry, expiry, localCacheEnable);
        await locker.WaitAsync(); // wait to receive cache invalidate message

        var v1I2 = await instance2.GetAsync<string>(key); // retrieve the value from the shared cache using instance2
        // update the value in the shared cache using instance2
        await instance2.SetAsync(key, value2, expiry, expiry, localCacheEnable);
        await locker.WaitAsync(); // wait to receive cache invalidate message

        // retrieve the updated value from the shared cache using instance1
        var v2I1 = await instance1.GetAsync<string>(key);

        // Assert
        Assert.Equal(value1, v1I2);
        Assert.Equal(value2, v2I1);

        // clean up
        await instance1.DisposeAsync();
        await instance2.DisposeAsync();
    }

    [Fact]
    public async Task CacheJustOnRedisAndFetchTwiceWithoutLocalCacheTest()
    {
        // When calling the Cache.Get<T> method, if the key does not exist in local memory,
        // it will fetch the value from Redis and populate the local memory cache using the
        // same expiration time as the Redis entry.
        // If the first caller decides not to store the fetched value in the local cache (based on condition checks),
        // later calls to Get may incorrectly read from the local cache or
        // trigger another Redis fetch depending on the caching logic.

        // Arrange
        var cacheKey = UniqueKey;
        var value1 = "test value 1";
        var value2 = "test value 2";

        // create two instances of HybridCache that share the same Redis cache
        await using var instance1 = new HybridCache(Options);
        await using var instance2 = new HybridCache(Options);
        var opt = new HybridCacheEntry
        {
            FireAndForget = false,
            LocalCacheEnable = false,
            RedisCacheEnable = true,
            RedisExpiry = TimeSpan.FromSeconds(100)
        };

        // Act
        await instance1.SetAsync(cacheKey, value1, opt);
        var readValue1Instance1 = await instance1.GetAsync<string>(cacheKey);
        var readValue1Instance2 = await instance2.GetAsync<string>(cacheKey);

        await instance2.SetAsync(cacheKey, value2, opt);
        // can read new value2 which write from another instance
        var readValue2Instance1 = await instance1.GetAsync<string>(cacheKey);
        var readValue2Instance2 = await instance2.GetAsync<string>(cacheKey);

        // Assert
        Assert.Equal(value1, readValue1Instance1);
        Assert.Equal(value1, readValue1Instance2);
        Assert.Equal(value2, readValue2Instance1);
        Assert.Equal(value2, readValue2Instance2);
    }

    [Fact]
    public async Task CacheHybridAndFetchTripleWithLocalCacheTest()
    {
        // Arrange
        var cacheKey = UniqueKey;
        var value1 = "test value 1";
        var value2 = "test value 2";
        var opt = new HybridCacheEntry
        {
            FireAndForget = false,
            LocalCacheEnable = true,
            RedisCacheEnable = true,
            LocalExpiry = TimeSpan.FromSeconds(10),
            RedisExpiry = TimeSpan.FromSeconds(100)
        };
        await using var instance1 = new HybridCache(Options);
        await using var instance2 = new HybridCache(Options);
        await using var instance3 = new HybridCache(Options);

        // Act
        await instance1.SetAsync(cacheKey, value1, opt);
        var read1Instance1 = await instance1.GetAsync<string>(cacheKey);
        var read1Instance2 = await instance2.GetAsync<string>(cacheKey);
        var read1Instance3 = await instance3.GetAsync<string>(cacheKey);

        await instance1.SetAsync(cacheKey, value2, opt);

        var read2Instance1 = await instance1.GetAsync<string>(cacheKey);
        var read2Instance2 = await instance2.GetAsync<string>(cacheKey);
        var read2Instance3 = await instance3.GetAsync<string>(cacheKey);

        // Assert
        Assert.Equal(value1, read1Instance1);
        Assert.Equal(value1, read1Instance2);
        Assert.Equal(value1, read1Instance3);
        Assert.Equal(value2, read2Instance1);
        Assert.Equal(value1, read2Instance2);
        Assert.Equal(value1, read2Instance3);
    }
}