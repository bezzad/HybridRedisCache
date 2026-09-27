using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

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
        Assert.Null(await Cache.GetAsync<string>(UniqueKey, token: TestToken));
        Assert.Equal(0, await Cache.GetAsync<int>(UniqueKey, token: TestToken));
    }

    [Fact]
    public async Task TryGetValueAsync_MissingKey_ReturnsFalse()
    {
        var (success, value) = await Cache.TryGetValueAsync<string>(UniqueKey, token: TestToken);
        Assert.False(success);
        Assert.Null(value);
    }

    [Fact]
    public async Task SetAsync_ThenGetAsync_RoundTrips()
    {
        var key = UniqueKey;
        Assert.True(await Cache.SetAsync(key, "value", token: TestToken));
        Assert.Equal("value", await Cache.GetAsync<string>(key, token: TestToken));
    }

    [Fact]
    public async Task SetAsync_OverwritesExistingValue()
    {
        var key = UniqueKey;
        await Cache.SetAsync(key, "first", token: TestToken);
        await Cache.SetAsync(key, "second", token: TestToken);
        Assert.Equal("second", await Cache.GetAsync<string>(key, token: TestToken));
    }

    [Fact]
    public async Task SetAsync_WithConditionNotExists_DoesNotOverwrite()
    {
        var key = UniqueKey;
        Assert.True(await Cache.SetAsync(key, "first", token: TestToken));

        var second = await Cache.SetAsync(key, "second", when: Condition.NotExists, token: TestToken);

        Assert.False(second);
        Assert.Equal("first", await Cache.GetAsync<string>(key, token: TestToken));
    }

    [Fact]
    public async Task SetAsync_WithConditionExists_OnMissingKey_DoesNotWrite()
    {
        var key = UniqueKey;
        Assert.False(await Cache.SetAsync(key, "v", when: Condition.Exists, token: TestToken));
        Assert.False(await Cache.ExistsAsync(key, token: TestToken));
    }

    [Fact]
    public async Task LocalExpiry_IsClampedToRedisExpiry()
    {
        // SetValidExpiryTimes must never let the local copy outlive the Redis copy.
        var key = UniqueKey;
        await Cache.SetAsync(key, "v",
            localExpiry: TimeSpan.FromHours(10),
            redisExpiry: TimeSpan.FromMinutes(5), token: TestToken);

        var ttl = await Cache.GetExpirationAsync(key, token: TestToken);

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

        Assert.Equal("retrieved", await Cache.GetAsync(key, Retriever, token: TestToken));
        Assert.Equal("retrieved", await Cache.GetAsync(key, Retriever, token: TestToken));
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

        Assert.Null(await Cache.GetAsync(key, Retriever, token: TestToken));
        Assert.Null(await Cache.GetAsync(key, Retriever, token: TestToken));
        Assert.Equal(2, calls); // nulls are not cached, so the retriever runs again
    }

    // ---------- exists / remove ----------

    [Fact]
    public async Task ExistsAsync_ReflectsWritesAndRemovals()
    {
        var key = UniqueKey;
        Assert.False(await Cache.ExistsAsync(key, token: TestToken));

        await Cache.SetAsync(key, "v", token: TestToken);
        Assert.True(await Cache.ExistsAsync(key, token: TestToken));

        await Cache.RemoveAsync(key, token: TestToken);
        Assert.False(await Cache.ExistsAsync(key, token: TestToken));
    }

    [Fact]
    public async Task RemoveAsync_MissingKey_ReturnsFalse()
    {
        Assert.False(await Cache.RemoveAsync(UniqueKey, token: TestToken));
    }

    [Fact]
    public async Task RemoveAsync_MultipleKeys_RemovesAll()
    {
        var keys = Enumerable.Range(0, 5).Select(_ => UniqueKey).ToArray();
        foreach (var k in keys)
            await Cache.SetAsync(k, "v", token: TestToken);

        Assert.True(await Cache.RemoveAsync(keys, token: TestToken));

        foreach (var k in keys)
            Assert.False(await Cache.ExistsAsync(k, token: TestToken));
    }

    [Fact]
    public async Task RemoveAsync_WithEmptyArray_Throws()
    {
        await Assert.ThrowsAnyAsync<ArgumentException>(() => Cache.RemoveAsync([], token: TestToken));
    }

    // ---------- increment / decrement ----------

    [Fact]
    public async Task ValueIncrementAsync_StartsFromZero()
    {
        Assert.Equal(5, await Cache.ValueIncrementAsync(UniqueKey, 5L, token: TestToken));
    }

    [Fact]
    public async Task ValueIncrementAndDecrement_Accumulate()
    {
        var key = UniqueKey;
        Assert.Equal(10, await Cache.ValueIncrementAsync(key, 10L, token: TestToken));
        Assert.Equal(7, await Cache.ValueDecrementAsync(key, 3L, token: TestToken));
        Assert.Equal(8, await Cache.ValueIncrementAsync(key, token: TestToken));
        Assert.Equal(7, await Cache.ValueDecrementAsync(key, token: TestToken));
    }

    [Fact]
    public async Task ValueIncrementAsync_WithDouble_Accumulates()
    {
        var key = UniqueKey;
        Assert.Equal(1.5, await Cache.ValueIncrementAsync(key, 1.5, token: TestToken));
        Assert.Equal(3.0, await Cache.ValueIncrementAsync(key, 1.5, token: TestToken));
    }

    // ---------- hash ----------

    [Fact]
    public async Task HashSetAndGet_SingleField_RoundTrips()
    {
        var key = UniqueKey;
        await Cache.HashSetAsync(key, "field", "value", token: TestToken);
        Assert.Equal("value", await Cache.HashGetAsync(key, "field", token: TestToken));
    }

    [Fact]
    public async Task HashGetAsync_ReturnsAllFields()
    {
        var key = UniqueKey;
        await Cache.HashSetAsync(key, "a", "1", token: TestToken);
        await Cache.HashSetAsync(key, "b", "2", token: TestToken);

        var all = await Cache.HashGetAsync(key, token: TestToken);

        Assert.Equal(2, all.Count);
        Assert.Equal("1", all["a"]);
        Assert.Equal("2", all["b"]);
    }

    [Fact]
    public async Task HashSetAsync_WithEmptyFields_IsNoOp()
    {
        var key = UniqueKey;
        await Cache.HashSetAsync(key, new Dictionary<string, string>(), token: TestToken);
        Assert.Equal(0, await Cache.HashLengthAsync(key, token: TestToken));
    }

    [Fact]
    public async Task HashExistsAsync_ReflectsFieldPresence()
    {
        var key = UniqueKey;
        await Cache.HashSetAsync(key, "present", "v", token: TestToken);

        Assert.True(await Cache.HashExistsAsync(key, "present", token: TestToken));
        Assert.False(await Cache.HashExistsAsync(key, "absent", token: TestToken));
    }

    [Fact]
    public async Task HashDeleteAsync_RemovesField()
    {
        var key = UniqueKey;
        await Cache.HashSetAsync(key, "f", "v", token: TestToken);

        Assert.True(await Cache.HashDeleteAsync(key, "f", token: TestToken));
        Assert.False(await Cache.HashExistsAsync(key, "f", token: TestToken));
    }

    [Fact]
    public async Task HashDeleteAsync_MultipleFields_ReturnsRemovedCount()
    {
        var key = UniqueKey;
        await Cache.HashSetAsync(key, "a", "1", token: TestToken);
        await Cache.HashSetAsync(key, "b", "2", token: TestToken);
        await Cache.HashSetAsync(key, "c", "3", token: TestToken);

        Assert.Equal(2, await Cache.HashDeleteAsync(key, ["a", "b"], token: TestToken));
        Assert.Equal(1, await Cache.HashLengthAsync(key, token: TestToken));
    }

    [Fact]
    public async Task HashKeysAndValues_ReturnFieldNamesAndValues()
    {
        var key = UniqueKey;
        await Cache.HashSetAsync(key, "a", "1", token: TestToken);
        await Cache.HashSetAsync(key, "b", "2", token: TestToken);

        Assert.Equal(["a", "b"], (await Cache.HashKeysAsync(key, token: TestToken)).OrderBy(x => x).ToArray());
        Assert.Equal(["1", "2"], (await Cache.HashValuesAsync(key, token: TestToken)).OrderBy(x => x).ToArray());
    }

    [Fact]
    public async Task HashLengthAsync_MissingKey_ReturnsZero()
    {
        Assert.Equal(0, await Cache.HashLengthAsync(UniqueKey, token: TestToken));
    }

    // ---------- locks ----------

    [Fact]
    public async Task TryLockKeyAsync_SecondCallerIsRejected()
    {
        var key = UniqueKey;
        Assert.True(await Cache.TryLockKeyAsync(key, "token-1", TimeSpan.FromMinutes(1), cancellationToken: TestToken));
        Assert.False(await Cache.TryLockKeyAsync(key, "token-2", TimeSpan.FromMinutes(1), cancellationToken: TestToken));
    }

    [Fact]
    public async Task TryReleaseLockAsync_WithWrongToken_Fails()
    {
        var key = UniqueKey;
        await Cache.TryLockKeyAsync(key, "right", TimeSpan.FromMinutes(1), cancellationToken: TestToken);

        Assert.False(await Cache.TryReleaseLockAsync(key, "wrong", cancellationToken: TestToken));
        Assert.True(await Cache.TryReleaseLockAsync(key, "right", cancellationToken: TestToken));
    }

    [Fact]
    public async Task TryLockKeyAsync_AfterRelease_CanBeReacquired()
    {
        var key = UniqueKey;
        await Cache.TryLockKeyAsync(key, "t1", TimeSpan.FromMinutes(1), cancellationToken: TestToken);
        await Cache.TryReleaseLockAsync(key, "t1", cancellationToken: TestToken);

        Assert.True(await Cache.TryLockKeyAsync(key, "t2", TimeSpan.FromMinutes(1), cancellationToken: TestToken));
    }

    [Fact]
    public async Task TryExtendLockAsync_WithCorrectToken_Succeeds()
    {
        var key = UniqueKey;
        await Cache.TryLockKeyAsync(key, "tok", TimeSpan.FromSeconds(30), cancellationToken: TestToken);

        Assert.True(await Cache.TryExtendLockAsync(key, "tok", TimeSpan.FromMinutes(5), cancellationToken: TestToken));
        Assert.False(await Cache.TryExtendLockAsync(key, "other", TimeSpan.FromMinutes(5), cancellationToken: TestToken));
    }

    // ---------- keys / patterns ----------

    [Fact]
    public async Task KeysAsync_ReturnsMatchingKeysWithoutPrefix()
    {
        var marker = UniqueKey;
        for (var i = 0; i < 3; i++)
            await Cache.SetAsync($"{marker}-{i}", "v", token: TestToken);

        var keys = new List<string>();
        await foreach (var k in Cache.KeysAsync($"{marker}-*", token: TestToken))
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
            await Cache.SetAsync($"{marker}-{i}", "v", token: TestToken);
        await Cache.SetAsync(keeper, "keep", token: TestToken);

        await Cache.RemoveWithPatternOnRedisAsync($"{marker}-*", token: TestToken);

        // Checked through KeysAsync (a server-side SCAN) because the pattern delete happens entirely
        // on Redis and leaves this instance's local copies behind until the bus tells it otherwise.
        var remaining = new List<string>();
        await foreach (var k in Cache.KeysAsync($"{marker}-*", token: TestToken))
            remaining.Add(k);

        Assert.Empty(remaining);

        var keepers = new List<string>();
        await foreach (var k in Cache.KeysAsync(keeper, token: TestToken))
            keepers.Add(k);

        Assert.Single(keepers);
    }

    // ---------- expiration ----------

    [Fact]
    public async Task GetExpirationAsync_ReturnsRemainingTtl()
    {
        var key = UniqueKey;
        await Cache.SetAsync(key, "v", redisExpiry: TimeSpan.FromMinutes(10), token: TestToken);

        var ttl = await Cache.GetExpirationAsync(key, token: TestToken);

        Assert.NotNull(ttl);
        Assert.InRange(ttl.Value, TimeSpan.FromMinutes(9), TimeSpan.FromMinutes(10));
    }

    [Fact]
    public async Task KeyExpireAsync_SetsTtlOnExistingKey()
    {
        var key = UniqueKey;
        await Cache.SetAsync(key, "v", redisExpiry: TimeSpan.FromHours(5), token: TestToken);

        await Cache.KeyExpireAsync(key, TimeSpan.FromMinutes(2), token: TestToken);

        var ttl = await Cache.GetExpirationAsync(key, token: TestToken);
        Assert.NotNull(ttl);
        Assert.True(ttl <= TimeSpan.FromMinutes(2), $"ttl was {ttl}");
    }

    // ---------- server info ----------

    [Fact]
    public async Task PingAsync_ReturnsNonNegativeDuration()
    {
        Assert.True(await Cache.PingAsync(token: TestToken) >= TimeSpan.Zero);
    }

    [Fact]
    public async Task EchoAsync_ReturnsMessage()
    {
        Assert.Contains("hello", await Cache.EchoAsync("hello", token: TestToken));
    }

    [Fact]
    public async Task TimeAsync_ReturnsRecentServerTime()
    {
        var serverTime = await Cache.TimeAsync(token: TestToken);
        Assert.InRange(serverTime, DateTime.UtcNow.AddMinutes(-5), DateTime.UtcNow.AddMinutes(5));
    }

    [Fact]
    public async Task DatabaseSizeAsync_ReturnsNonNegative()
    {
        Assert.True(await Cache.DatabaseSizeAsync(token: TestToken) >= 0);
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
        Assert.True(await Cache.SetAsync(key, "v", localCacheEnable: false, token: TestToken));
        Assert.Equal("v", await Cache.GetAsync<string>(key, token: TestToken));
    }
}
