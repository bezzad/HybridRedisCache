using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace HybridRedisCache.Test;

public class IntegrationTests(ITestOutputHelper testOutputHelper) : BaseCacheTest(testOutputHelper)
{
    [Theory(Timeout = 10_000)]
    [InlineData(true)]
    [InlineData(false)]
    public async Task TestSharedCache(bool localCacheEnable)
    {
        // Arrange
        var key = "TestSharedCache_" + UniqueKey;
        const string value1 = "Value1";
        const string value2 = "value2";
        var expiry = TimeSpan.FromSeconds(50);
        var locker = new SemaphoreSlim(0, 1); // semaphore to wait for cache invalidate message
        await using var instance1 = new HybridCache(Options);
        await using var instance2 = new HybridCache(Options);
        instance2.OnRedisBusMessage += (k, type) =>
        {
            if (key == k && type == MessageType.SetCache)
            {
                // release the semaphore when a cache invalidate message is received
                locker.Release();
            }
        };

        await instance1.SetAsync(key, value1, expiry, expiry, Flags.DemandMaster, localCacheEnable: localCacheEnable);

        // wait to receive the cache invalidate message
        await locker.WaitAsync();

        // retrieve the value from the shared cache using instance2
        var v1I2 = await instance2.GetAsync<string>(key);

        // update the value in the shared cache using instance1
        await instance1.SetAsync(key, value2, expiry, expiry, Flags.DemandMaster, localCacheEnable: localCacheEnable);

        // wait to receive the cache invalidate message
        await locker.WaitAsync();

        // retrieve the updated value from the shared cache using instance2
        var v2I2 = await instance2.GetAsync<string>(key);

        // Assert
        Assert.Equal(value1, v1I2);
        Assert.Equal(value2, v2I2);
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
        const string value1 = "test value 1";
        const string value2 = "test value 2";

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
        const string value1 = "test value 1";
        const string value2 = "test value 2";
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
        await Task.Delay(100);

        var read2Instance1 = await instance1.GetAsync<string>(cacheKey);
        var read2Instance2 = await instance2.GetAsync<string>(cacheKey);
        var read2Instance3 = await instance3.GetAsync<string>(cacheKey);

        // Assert
        Assert.Equal(value1, read1Instance1);
        Assert.Equal(value1, read1Instance2);
        Assert.Equal(value1, read1Instance3);
        Assert.Equal(value2, read2Instance1);
        Assert.Equal(value2, read2Instance2);
        Assert.Equal(value2, read2Instance3);
    }

    [Theory(Timeout = 10_000)]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CacheRedisOnlyAndGetWithLocalCacheDisabledTest(bool getLocalCacheEnable)
    {
        // Arrange
        var cacheKey = UniqueKey;
        var opt = new HybridCacheEntry
        {
            FireAndForget = false,
            LocalCacheEnable = false,
            RedisCacheEnable = true,
            RedisExpiry = TimeSpan.FromSeconds(100)
        };
        await using var instance1 = new HybridCache(Options);
        await using var instance2 = new HybridCache(Options);
        await using var instance3 = new HybridCache(Options);

        // Act
        for (var i = 0; i < 1000; i++)
        {
            var value = "test value " + i;

            await instance1.SetAsync(cacheKey, value, opt);
            var readWithInstance1 = await instance1.GetAsync<string>(cacheKey, getLocalCacheEnable);
            var readWithInstance2 = await instance2.GetAsync<string>(cacheKey, getLocalCacheEnable);
            var readWithInstance3 = await instance3.GetAsync<string>(cacheKey, getLocalCacheEnable);

            Assert.Equal(value, readWithInstance1);
            Assert.Equal(value, readWithInstance2);
            Assert.Equal(value, readWithInstance3);
        }
    }

    [Fact]
    public async Task CacheRedisOnlyAndGetWithLocalCacheEnabledTest()
    {
        // Arrange
        var cacheKey = UniqueKey;
        var opt = new HybridCacheEntry
        {
            FireAndForget = false,
            LocalCacheEnable = true,
            RedisCacheEnable = true,
            RedisExpiry = TimeSpan.FromSeconds(100)
        };
        await using var instance1 = new HybridCache(Options);
        await using var instance2 = new HybridCache(Options);
        await using var instance3 = new HybridCache(Options);

        // Act
        for (var i = 0; i < 1000; i++)
        {
            var value = "test value " + i;

            await instance1.SetAsync(cacheKey, value, opt);
            var readWithInstance1 = await instance1.GetAsync<string>(cacheKey, false);
            var readWithInstance2 = await instance2.GetAsync<string>(cacheKey, false);
            var readWithInstance3 = await instance3.GetAsync<string>(cacheKey, false);

            Assert.Equal(value, readWithInstance1);
            Assert.Equal(value, readWithInstance2);
            Assert.Equal(value, readWithInstance3);
        }
    }

    [Fact]
    public async Task SetRedisCacheOnlyAndTestWhenNotExistBySameSetter()
    {
        // Arrange
        var cacheKey = UniqueKey;
        var value1 = "test value 1";
        var value2 = "test value 2";

        var opt = new HybridCacheEntry
        {
            FireAndForget = false,
            LocalCacheEnable = false,
            RedisCacheEnable = true,
            RedisExpiry = TimeSpan.FromSeconds(100),
            When = Condition.NotExists
        };
        await using var instance1 = new HybridCache(Options);
        await using var instance2 = new HybridCache(Options);
        await using var instance3 = new HybridCache(Options);

        // Act
        var canInsertI1 = await instance1.SetAsync(cacheKey, value1, opt);
        var canInsertI2 = await instance2.SetAsync(cacheKey, value1, opt);
        var canInsertI3 = await instance3.SetAsync(cacheKey, value1, opt);
        var canInsertI12 = await instance1.SetAsync(cacheKey, value2, opt);

        opt.When = Condition.Always;
        var canInsertI22 = await instance2.SetAsync(cacheKey, value2, opt);

        Assert.True(canInsertI1);
        Assert.False(canInsertI2);
        Assert.False(canInsertI3);
        Assert.False(canInsertI12);
        Assert.True(canInsertI22);
    }

    [Fact]
    public async Task TestLockKeyAndExtendIt()
    {
        // Arrange
        var cacheKey = UniqueKey;
        var token1 = "test token";
        var token2 = "test token 2";
        var timespan = TimeSpan.FromSeconds(100);

        await using var instance1 = new HybridCache(Options);
        await using var instance2 = new HybridCache(Options);
        await using var instance3 = new HybridCache(Options);

        // Act

        await instance1.TryLockKeyAsync(cacheKey, token1, TimeSpan.FromSeconds(10));
        var extendLockWithI1T2 = await instance1.TryExtendLockAsync(cacheKey, token2, timespan);
        var extendLockWithI1T1 = await instance1.TryExtendLockAsync(cacheKey, token1, timespan);
        var extendLockWithI2T1 = await instance2.TryExtendLockAsync(cacheKey, token1, timespan);
        var extendLockWithI3T1 = await instance3.TryExtendLockAsync(cacheKey, token1, timespan);
        var expiry = await instance1.GetExpirationAsync(HybridCache.LockKeyPrefix + cacheKey);

        Assert.False(extendLockWithI1T2);
        Assert.True(extendLockWithI1T1);
        Assert.True(extendLockWithI2T1);
        Assert.True(extendLockWithI3T1);
        Assert.True(timespan >= expiry, $"{timespan} should be greater than {expiry}");
        Assert.True(timespan <= expiry.Value.Add(TimeSpan.FromSeconds(3)));
    }

    [Fact]
    public async Task RedisPubSubOnCustomChannelTest()
    {
        // Arrange
        var cacheKey = UniqueKey;
        var channel = "__test_channel__";
        var token1 = "test token";

        await using var instance1 = new HybridCache(Options);
        await using var instance2 = new HybridCache(Options);

        instance2.Subscribe(channel, OnMessage);

        // Act

        await instance1.PublishAsync(channel, cacheKey, token1);

        await Task.Delay(1000);

        void OnMessage(string key, string value)
        {
            Assert.Equal(key, cacheKey);
            Assert.Equal(value, token1);
        }
        
        instance2.Unsubscribe(channel);
    }
}
