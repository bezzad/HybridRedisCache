using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using HybridRedisCache.Serializers;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Xunit;

namespace HybridRedisCache.Test;

/// <summary>
/// Serializer that can be told to fail, so the error paths around serialization run without a
/// broken server.
/// </summary>
internal sealed class FaultySerializer : ICachingSerializer
{
    private readonly ICachingSerializer _inner = new BsonCachingSerializer(new JsonSerializerSettings());

    public bool FailSerialize { get; set; }
    public Exception DeserializeError { get; set; }

    public byte[] Serialize<T>(T value)
    {
        if (FailSerialize) throw new InvalidOperationException("serialize failed on purpose");
        return _inner.Serialize(value);
    }

    public T Deserialize<T>(byte[] bytes)
    {
        if (DeserializeError != null) throw DeserializeError;
        return _inner.Deserialize<T>(bytes);
    }
}

/// <summary>
/// Error paths and rarely used members that the behaviour tests do not reach.
/// </summary>
public class FaultInjectionTests(InProcessRedisFixture fixture, ITestOutputHelper output)
    : InProcessCacheTest(fixture, output)
{
    private readonly FaultySerializer _serializer = new();

    private (HybridCache cache, HybridCachingOptions options) CreateFaultyCache(bool throwOnError)
    {
        HybridCachingOptions captured = null;
        var cache = CreateCache(o =>
        {
            o.Serializer = _serializer;
            o.ThrowIfDistributedCacheError = throwOnError;
            captured = o;
        });
        return (cache, captured);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Set_WhenSerializerFails_ThrowsOrReturnsFalse(bool throwOnError)
    {
        await using var cache = CreateFaultyCache(throwOnError).cache;
        _serializer.FailSerialize = true;
        var entry = new HybridCacheEntry();
        var batch = new Dictionary<string, string> { [UniqueKey] = "v" };

        if (throwOnError)
        {
            Assert.Throws<InvalidOperationException>(() => cache.Set(UniqueKey, "v", entry));
            await Assert.ThrowsAsync<InvalidOperationException>(() => cache.SetAsync(UniqueKey, "v", entry, TestToken));
            Assert.Throws<InvalidOperationException>(() => cache.SetAll(batch, entry));
            await Assert.ThrowsAsync<InvalidOperationException>(() => cache.SetAllAsync(batch, entry, TestToken));
        }
        else
        {
            Assert.False(cache.Set(UniqueKey, "v", entry));
            Assert.False(await cache.SetAsync(UniqueKey, "v", entry, TestToken));
            Assert.False(cache.SetAll(batch, entry));
            Assert.False(await cache.SetAllAsync(batch, entry, TestToken));
        }
    }

    [Fact]
    public async Task Get_WhenStoredValueCannotBeDeserialized_ReportsMiss()
    {
        await using var cache = CreateFaultyCache(throwOnError: true).cache;
        var key = UniqueKey;
        Assert.True(await cache.SetAsync(key, "v", localCacheEnable: false, token: TestToken));
        _serializer.DeserializeError = new JsonSerializationException("bad payload");

        Assert.False(cache.TryGetValue(key, out string _));
        Assert.False((await cache.TryGetValueAsync<string>(key, token: TestToken)).success);
        Assert.Empty(cache.GetAll<string>([key]));
        Assert.Empty(await cache.GetAllAsync<string>([key], token: TestToken));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Get_WhenDeserializerThrowsOtherError_ThrowsOrReturnsDefault(bool throwOnError)
    {
        await using var cache = CreateFaultyCache(throwOnError).cache;
        var key = UniqueKey;
        Assert.True(await cache.SetAsync(key, "v", localCacheEnable: false, token: TestToken));
        _serializer.DeserializeError = new InvalidOperationException("boom");

        if (throwOnError)
        {
            Assert.Throws<InvalidOperationException>(() => cache.Get<string>(key));
            await Assert.ThrowsAsync<InvalidOperationException>(async () => await cache.GetAsync<string>(key, token: TestToken));
            Assert.Throws<InvalidOperationException>(() => cache.GetAll<string>([key]));
            await Assert.ThrowsAsync<InvalidOperationException>(() => cache.GetAllAsync<string>([key], token: TestToken));
        }
        else
        {
            Assert.Null(cache.Get<string>(key));
            Assert.Null(await cache.GetAsync<string>(key, token: TestToken));
            Assert.Empty(cache.GetAll<string>([key]));
            Assert.Empty(await cache.GetAllAsync<string>([key], token: TestToken));
        }
    }

#pragma warning disable CS0618 // the sync data-retriever overload is obsolete but still public
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SyncGetWithRetriever_WhenRetrieverThrows_ThrowsOrReturnsDefault(bool throwOnError)
    {
        await using var cache = CreateFaultyCache(throwOnError).cache;
        Func<string, string> failing = _ => throw new InvalidOperationException("retriever failed");

        if (throwOnError)
            Assert.Throws<InvalidOperationException>(() => cache.Get(UniqueKey, failing));
        else
            Assert.Null(cache.Get(UniqueKey, failing));
    }

    [Fact]
    public void SyncGetWithRetriever_WhenRetrieverReturnsNull_ReturnsNullAndCachesNothing()
    {
        var key = UniqueKey;
        Assert.Null(Cache.Get<string>(key, _ => null));
        Assert.False(Cache.Exists(key));
    }
#pragma warning restore CS0618

    [Fact]
    public async Task Exists_FindsKeyOnlyInRedis()
    {
        var key = UniqueKey;
        Assert.True(await Cache.SetAsync(key, "v", localCacheEnable: false, token: TestToken));
        Assert.True(Cache.Exists(key));
        Assert.True(await Cache.ExistsAsync(key, token: TestToken));
    }

    [Fact]
    public async Task SyncLockRelease_AndLockObjectDispose_ReleaseTheLock()
    {
        var key = UniqueKey;
        Assert.True(await Cache.TryLockKeyAsync(key, "t1", TimeSpan.FromMinutes(1), cancellationToken: TestToken));
        Assert.False(Cache.TryReleaseLock(key, "wrong"));
        Assert.True(Cache.TryReleaseLock(key, "t1"));

        var lockObject = await Cache.LockKeyAsync(key, TimeSpan.FromMinutes(1), cancellationToken: TestToken);
        lockObject.Dispose();
        Assert.True(await Cache.TryLockKeyAsync(key, "t2", TimeSpan.FromMinutes(1), cancellationToken: TestToken));
        Assert.True(Cache.TryReleaseLock(key, "t2"));

        lockObject = await Cache.LockKeyAsync(key, TimeSpan.FromMinutes(1), cancellationToken: TestToken);
        Assert.True(lockObject.Release());
    }

    [Fact]
    public async Task ValueDecrementAsync_Double_Decrements()
    {
        var key = UniqueKey;
        await Cache.ValueIncrementAsync(key, 5.5, token: TestToken);
        Assert.Equal(3.0, await Cache.ValueDecrementAsync(key, 2.5, token: TestToken), 3);
    }

    [Fact]
    public async Task KeyExpire_Sync_SetsTtl()
    {
        var key = UniqueKey;
        Assert.True(await Cache.SetAsync(key, "v", token: TestToken));
        Cache.KeyExpire(key, TimeSpan.FromMinutes(3));
        var ttl = Cache.GetExpiration(key);
        Assert.NotNull(ttl);
        Assert.InRange(ttl.Value, TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(3));
    }

    [Fact]
    public async Task SentinelCommands_OnNonSentinelServer_Throw()
    {
        await Assert.ThrowsAnyAsync<Exception>(() => Cache.SentinelGetMasterAddressByNameAsync("mymaster", token: TestToken));
        await Assert.ThrowsAnyAsync<Exception>(() => Cache.SentinelGetSentinelAddressesAsync("mymaster", token: TestToken));
        await Assert.ThrowsAnyAsync<Exception>(() => Cache.SentinelGetReplicaAddressesAsync("mymaster", token: TestToken));
    }

    [Fact]
    public async Task Tracing_TagsActivities_WhenAListenerIsAttached()
    {
        var tags = new List<string>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == new HybridCachingOptions().TracingActivitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = a => { lock (tags) tags.AddRange(a.Tags.Select(t => t.Key)); }
        };
        ActivitySource.AddActivityListener(listener);

        var key = UniqueKey;
        await Cache.GetAsync(key, _ => Task.FromResult("v"), token: TestToken);
        await Cache.GetAsync<string>(key, token: TestToken);

        lock (tags)
        {
            Assert.Contains("HybridRedisCache.RetrievalStrategy", tags);
            Assert.Contains("HybridRedisCache.CacheResult", tags);
        }
    }

    [Fact]
    public async Task AddHybridRedisCaching_RegistersAWorkingCache()
    {
        var options = Options;
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHybridRedisCaching(o =>
        {
            o.InstancesSharedName = options.InstancesSharedName;
            o.RedisConnectionString = options.RedisConnectionString;
            o.AbortOnConnectFail = false;
        });

        await using var provider = services.BuildServiceProvider();
        var cache = provider.GetRequiredService<IHybridCache>();
        var key = UniqueKey;
        Assert.True(await cache.SetAsync(key, "v", token: TestToken));
        Assert.Equal("v", await cache.GetAsync<string>(key, token: TestToken));
    }

    [Fact]
    public void AddHybridRedisCaching_RejectsNullArguments()
    {
        Assert.ThrowsAny<ArgumentException>(() => ((IServiceCollection)null).AddHybridRedisCaching(_ => { }));
        Assert.ThrowsAny<ArgumentException>(() => new ServiceCollection().AddHybridRedisCaching(null));
    }
}

/// <summary>Pure helpers that need no server.</summary>
public class HelperUnitTests
{
    [Fact]
    public void MessageType_RoundTripsEveryValue()
    {
        foreach (var type in Enum.GetValues<MessageType>())
        {
            var value = type.GetValue();
            var parsed = ((StackExchange.Redis.RedisValue)value).GetMessageType();
            Assert.Equal(type, parsed);
        }

        Assert.Equal(MessageType.RenameKey, ((StackExchange.Redis.RedisValue)"rename_to").GetMessageType());
        Assert.Equal(MessageType.BuiltInMessage, ((StackExchange.Redis.RedisValue)"unknown").GetMessageType());
        Assert.Throws<ArgumentOutOfRangeException>(() => ((MessageType)999).GetValue());
    }

    [Fact]
    public void ToTimeSpan_ClampsPastTimesToZero()
    {
        DateTime? past = DateTime.UtcNow.AddMinutes(-1);
        DateTime? none = null;
        Assert.Equal(TimeSpan.Zero, past.ToTimeSpan());
        Assert.Null(none.ToTimeSpan());
    }
}
