using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace HybridRedisCache.Test;

/// <summary>
/// Broad operation coverage against the in-process Garnet server (no Docker required).
/// Behaviours that depend on key-space notifications live in the container-backed suites.
/// </summary>
public class InProcessCacheOperationTests(InProcessRedisFixture fixture, ITestOutputHelper output)
    : InProcessCacheTest(fixture, output)
{
    // ---------- get / set ----------

    [Fact]
    public async Task GetAsync_MissingKey_ReturnsDefault()
    {
        Assert.Null(await Cache.GetAsync<string>(UniqueKey));
        Assert.Equal(0, await Cache.GetAsync<int>(UniqueKey));
    }

    [Fact]
    public async Task TryGetValueAsync_MissingKey_ReturnsFalse()
    {
        var (success, value) = await Cache.TryGetValueAsync<string>(UniqueKey);
        Assert.False(success);
        Assert.Null(value);
    }

    [Fact]
    public async Task SetAsync_ThenGetAsync_RoundTrips()
    {
        var key = UniqueKey;
        Assert.True(await Cache.SetAsync(key, "value"));
        Assert.Equal("value", await Cache.GetAsync<string>(key));
    }

    [Fact]
    public async Task SetAsync_OverwritesExistingValue()
    {
        var key = UniqueKey;
        await Cache.SetAsync(key, "first");
        await Cache.SetAsync(key, "second");
        Assert.Equal("second", await Cache.GetAsync<string>(key));
    }

    [Fact]
    public async Task SetAsync_WithConditionNotExists_DoesNotOverwrite()
    {
        var key = UniqueKey;
        Assert.True(await Cache.SetAsync(key, "first"));

        var second = await Cache.SetAsync(key, "second", when: Condition.NotExists);

        Assert.False(second);
        Assert.Equal("first", await Cache.GetAsync<string>(key));
    }

    [Fact]
    public async Task SetAsync_WithConditionExists_OnMissingKey_DoesNotWrite()
    {
        var key = UniqueKey;
        Assert.False(await Cache.SetAsync(key, "v", when: Condition.Exists));
        Assert.False(await Cache.ExistsAsync(key));
    }

    [Fact]
    public async Task LocalExpiry_IsClampedToRedisExpiry()
    {
        // SetValidExpiryTimes must never let the local copy outlive the Redis copy.
        var key = UniqueKey;
        await Cache.SetAsync(key, "v",
            localExpiry: TimeSpan.FromHours(10),
            redisExpiry: TimeSpan.FromMinutes(5));

        var ttl = await Cache.GetExpirationAsync(key);

        Assert.NotNull(ttl);
        Assert.True(ttl <= TimeSpan.FromMinutes(5), $"redis ttl was {ttl}");
    }

    [Fact]
    public async Task GetAsync_WithDataRetriever_PopulatesAndCaches()
    {
        var key = UniqueKey;
        var calls = 0;

        Task<string> Retriever(string _)
        {
            calls++;
            return Task.FromResult("retrieved");
        }

        Assert.Equal("retrieved", await Cache.GetAsync(key, Retriever));
        Assert.Equal("retrieved", await Cache.GetAsync(key, Retriever));
        Assert.Equal(1, calls); // second read is served from cache
    }

    [Fact]
    public async Task GetAsync_WithDataRetrieverReturningNull_DoesNotCache()
    {
        var key = UniqueKey;
        var calls = 0;

        Task<string> Retriever(string _)
        {
            calls++;
            return Task.FromResult<string>(null);
        }

        Assert.Null(await Cache.GetAsync(key, Retriever));
        Assert.Null(await Cache.GetAsync(key, Retriever));
        Assert.Equal(2, calls); // nulls are not cached, so the retriever runs again
    }

    // ---------- exists / remove ----------

    [Fact]
    public async Task ExistsAsync_ReflectsWritesAndRemovals()
    {
        var key = UniqueKey;
        Assert.False(await Cache.ExistsAsync(key));

        await Cache.SetAsync(key, "v");
        Assert.True(await Cache.ExistsAsync(key));

        await Cache.RemoveAsync(key);
        Assert.False(await Cache.ExistsAsync(key));
    }

    [Fact]
    public async Task RemoveAsync_MissingKey_ReturnsFalse()
    {
        Assert.False(await Cache.RemoveAsync(UniqueKey));
    }

    [Fact]
    public async Task RemoveAsync_MultipleKeys_RemovesAll()
    {
        var keys = Enumerable.Range(0, 5).Select(_ => UniqueKey).ToArray();
        foreach (var k in keys)
            await Cache.SetAsync(k, "v");

        Assert.True(await Cache.RemoveAsync(keys));

        foreach (var k in keys)
            Assert.False(await Cache.ExistsAsync(k));
    }

    [Fact]
    public async Task RemoveAsync_WithEmptyArray_Throws()
    {
        await Assert.ThrowsAnyAsync<ArgumentException>(() => Cache.RemoveAsync([]));
    }

    // ---------- increment / decrement ----------

    [Fact]
    public async Task ValueIncrementAsync_StartsFromZero()
    {
        Assert.Equal(5, await Cache.ValueIncrementAsync(UniqueKey, 5L));
    }

    [Fact]
    public async Task ValueIncrementAndDecrement_Accumulate()
    {
        var key = UniqueKey;
        Assert.Equal(10, await Cache.ValueIncrementAsync(key, 10L));
        Assert.Equal(7, await Cache.ValueDecrementAsync(key, 3L));
        Assert.Equal(8, await Cache.ValueIncrementAsync(key));
        Assert.Equal(7, await Cache.ValueDecrementAsync(key));
    }

    [Fact]
    public async Task ValueIncrementAsync_WithDouble_Accumulates()
    {
        var key = UniqueKey;
        Assert.Equal(1.5, await Cache.ValueIncrementAsync(key, 1.5));
        Assert.Equal(3.0, await Cache.ValueIncrementAsync(key, 1.5));
    }

    // ---------- hash ----------

    [Fact]
    public async Task HashSetAndGet_SingleField_RoundTrips()
    {
        var key = UniqueKey;
        await Cache.HashSetAsync(key, "field", "value");
        Assert.Equal("value", await Cache.HashGetAsync(key, "field"));
    }

    [Fact]
    public async Task HashGetAsync_ReturnsAllFields()
    {
        var key = UniqueKey;
        await Cache.HashSetAsync(key, "a", "1");
        await Cache.HashSetAsync(key, "b", "2");

        var all = await Cache.HashGetAsync(key);

        Assert.Equal(2, all.Count);
        Assert.Equal("1", all["a"]);
        Assert.Equal("2", all["b"]);
    }

    [Fact]
    public async Task HashSetAsync_WithEmptyFields_IsNoOp()
    {
        var key = UniqueKey;
        await Cache.HashSetAsync(key, new Dictionary<string, string>());
        Assert.Equal(0, await Cache.HashLengthAsync(key));
    }

    [Fact]
    public async Task HashExistsAsync_ReflectsFieldPresence()
    {
        var key = UniqueKey;
        await Cache.HashSetAsync(key, "present", "v");

        Assert.True(await Cache.HashExistsAsync(key, "present"));
        Assert.False(await Cache.HashExistsAsync(key, "absent"));
    }

    [Fact]
    public async Task HashDeleteAsync_RemovesField()
    {
        var key = UniqueKey;
        await Cache.HashSetAsync(key, "f", "v");

        Assert.True(await Cache.HashDeleteAsync(key, "f"));
        Assert.False(await Cache.HashExistsAsync(key, "f"));
    }

    [Fact]
    public async Task HashDeleteAsync_MultipleFields_ReturnsRemovedCount()
    {
        var key = UniqueKey;
        await Cache.HashSetAsync(key, "a", "1");
        await Cache.HashSetAsync(key, "b", "2");
        await Cache.HashSetAsync(key, "c", "3");

        Assert.Equal(2, await Cache.HashDeleteAsync(key, ["a", "b"]));
        Assert.Equal(1, await Cache.HashLengthAsync(key));
    }

    [Fact]
    public async Task HashKeysAndValues_ReturnFieldNamesAndValues()
    {
        var key = UniqueKey;
        await Cache.HashSetAsync(key, "a", "1");
        await Cache.HashSetAsync(key, "b", "2");

        Assert.Equal(["a", "b"], (await Cache.HashKeysAsync(key)).OrderBy(x => x).ToArray());
        Assert.Equal(["1", "2"], (await Cache.HashValuesAsync(key)).OrderBy(x => x).ToArray());
    }

    [Fact]
    public async Task HashLengthAsync_MissingKey_ReturnsZero()
    {
        Assert.Equal(0, await Cache.HashLengthAsync(UniqueKey));
    }

    // ---------- locks ----------

    [Fact]
    public async Task TryLockKeyAsync_SecondCallerIsRejected()
    {
        var key = UniqueKey;
        Assert.True(await Cache.TryLockKeyAsync(key, "token-1", TimeSpan.FromMinutes(1)));
        Assert.False(await Cache.TryLockKeyAsync(key, "token-2", TimeSpan.FromMinutes(1)));
    }

    [Fact]
    public async Task TryReleaseLockAsync_WithWrongToken_Fails()
    {
        var key = UniqueKey;
        await Cache.TryLockKeyAsync(key, "right", TimeSpan.FromMinutes(1));

        Assert.False(await Cache.TryReleaseLockAsync(key, "wrong"));
        Assert.True(await Cache.TryReleaseLockAsync(key, "right"));
    }

    [Fact]
    public async Task TryLockKeyAsync_AfterRelease_CanBeReacquired()
    {
        var key = UniqueKey;
        await Cache.TryLockKeyAsync(key, "t1", TimeSpan.FromMinutes(1));
        await Cache.TryReleaseLockAsync(key, "t1");

        Assert.True(await Cache.TryLockKeyAsync(key, "t2", TimeSpan.FromMinutes(1)));
    }

    [Fact]
    public async Task TryExtendLockAsync_WithCorrectToken_Succeeds()
    {
        var key = UniqueKey;
        await Cache.TryLockKeyAsync(key, "tok", TimeSpan.FromSeconds(30));

        Assert.True(await Cache.TryExtendLockAsync(key, "tok", TimeSpan.FromMinutes(5)));
        Assert.False(await Cache.TryExtendLockAsync(key, "other", TimeSpan.FromMinutes(5)));
    }

    // ---------- keys / patterns ----------

    [Fact]
    public async Task KeysAsync_ReturnsMatchingKeysWithoutPrefix()
    {
        var marker = UniqueKey;
        for (var i = 0; i < 3; i++)
            await Cache.SetAsync($"{marker}-{i}", "v");

        var keys = new List<string>();
        await foreach (var k in Cache.KeysAsync($"{marker}-*"))
            keys.Add(k);

        Assert.Equal(3, keys.Count);
        // Keys come back without the InstancesSharedName prefix, ready to feed straight back in.
        Assert.All(keys, k => Assert.StartsWith(marker, k));
    }

    [Fact]
    public async Task RemoveWithPatternOnRedisAsync_RemovesMatchingKeysOnly()
    {
        var marker = UniqueKey;
        var keeper = UniqueKey;

        for (var i = 0; i < 3; i++)
            await Cache.SetAsync($"{marker}-{i}", "v");
        await Cache.SetAsync(keeper, "keep");

        await Cache.RemoveWithPatternOnRedisAsync($"{marker}-*");

        // Checked through KeysAsync (a server-side SCAN) because the pattern delete happens entirely
        // on Redis and leaves this instance's local copies behind until the bus tells it otherwise.
        var remaining = new List<string>();
        await foreach (var k in Cache.KeysAsync($"{marker}-*"))
            remaining.Add(k);

        Assert.Empty(remaining);

        var keepers = new List<string>();
        await foreach (var k in Cache.KeysAsync(keeper))
            keepers.Add(k);

        Assert.Single(keepers);
    }

    // ---------- expiration ----------

    [Fact]
    public async Task GetExpirationAsync_ReturnsRemainingTtl()
    {
        var key = UniqueKey;
        await Cache.SetAsync(key, "v", redisExpiry: TimeSpan.FromMinutes(10));

        var ttl = await Cache.GetExpirationAsync(key);

        Assert.NotNull(ttl);
        Assert.InRange(ttl.Value, TimeSpan.FromMinutes(9), TimeSpan.FromMinutes(10));
    }

    [Fact]
    public async Task KeyExpireAsync_SetsTtlOnExistingKey()
    {
        var key = UniqueKey;
        await Cache.SetAsync(key, "v", redisExpiry: TimeSpan.FromHours(5));

        await Cache.KeyExpireAsync(key, TimeSpan.FromMinutes(2));

        var ttl = await Cache.GetExpirationAsync(key);
        Assert.NotNull(ttl);
        Assert.True(ttl <= TimeSpan.FromMinutes(2), $"ttl was {ttl}");
    }

    // ---------- server info ----------

    [Fact]
    public async Task PingAsync_ReturnsNonNegativeDuration()
    {
        Assert.True(await Cache.PingAsync() >= TimeSpan.Zero);
    }

    [Fact]
    public async Task EchoAsync_ReturnsMessage()
    {
        Assert.Contains("hello", await Cache.EchoAsync("hello"));
    }

    [Fact]
    public async Task TimeAsync_ReturnsRecentServerTime()
    {
        var serverTime = await Cache.TimeAsync();
        Assert.InRange(serverTime, DateTime.UtcNow.AddMinutes(-5), DateTime.UtcNow.AddMinutes(5));
    }

    [Fact]
    public async Task DatabaseSizeAsync_ReturnsNonNegative()
    {
        Assert.True(await Cache.DatabaseSizeAsync() >= 0);
    }

    [Fact]
    public void GetServerVersion_ReturnsVersion()
    {
        Assert.NotNull(Cache.GetServerVersion());
    }

    // ---------- local cache ----------

    [Fact]
    public async Task SetAsync_WithLocalCacheDisabled_StillReadableFromRedis()
    {
        var key = UniqueKey;
        Assert.True(await Cache.SetAsync(key, "v", localCacheEnable: false));
        Assert.Equal("v", await Cache.GetAsync<string>(key));
    }
}
