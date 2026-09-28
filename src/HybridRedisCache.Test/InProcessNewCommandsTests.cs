using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace HybridRedisCache.Test;

/// <summary>
/// Covers GetAll, GETEX, RENAME/PERSIST, HyperLogLog, bit and server admin commands
/// against the in-process Garnet server. Garnet has no TOUCH, so
/// <see cref="HybridCacheTests"/> covers KeyTouchAsync.
/// </summary>
public class InProcessNewCommandsTests(InProcessRedisFixture fixture, ITestOutputHelper output)
    : InProcessCacheTest(fixture, output)
{
    [Fact]
    public async Task GetAllAsync_ReturnsFoundKeys_AndSkipsMissingKeys()
    {
        var (k1, k2, missing) = (UniqueKey, UniqueKey, UniqueKey);
        await Cache.SetAsync(k1, "v1", token: TestToken);
        await Cache.SetAsync(k2, "v2", localCacheEnable: false, token: TestToken);

        var result = await Cache.GetAllAsync<string>([k1, k2, missing, k1], token: TestToken);

        Assert.Equal(2, result.Count);
        Assert.Equal("v1", result[k1]);
        Assert.Equal("v2", result[k2]);
    }

    [Fact]
    public async Task GetAll_ReadsFromRedis_WhenLocalCacheIsCleared()
    {
        var (k1, k2) = (UniqueKey, UniqueKey);
        await Cache.SetAllAsync(new Dictionary<string, int> { [k1] = 1, [k2] = 2 }, token: TestToken);
        Cache.FlushLocalCaches();

        var result = Cache.GetAll<int>([k1, k2]);

        Assert.Equal(1, result[k1]);
        Assert.Equal(2, result[k2]);
    }

    [Fact]
    public async Task GetAndExpireAsync_ReturnsValue_AndSetsNewTtl()
    {
        var key = UniqueKey;
        await Cache.SetAsync(key, "v", redisExpiry: TimeSpan.FromHours(1), token: TestToken);

        var value = await Cache.GetAndExpireAsync<string>(key, TimeSpan.FromMinutes(1), token: TestToken);

        Assert.Equal("v", value);
        var ttl = await Cache.GetExpirationAsync(key, token: TestToken);
        Assert.NotNull(ttl);
        Assert.True(ttl <= TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task GetAndExpireAsync_MissingKey_ReturnsDefault()
    {
        Assert.Null(await Cache.GetAndExpireAsync<string>(UniqueKey, TimeSpan.FromMinutes(1), token: TestToken));
    }

    [Fact]
    public async Task KeyRenameAsync_MovesValue_AndDropsOldKey()
    {
        var (key, newKey) = (UniqueKey, UniqueKey);
        await Cache.SetAsync(key, "v", token: TestToken);

        Assert.True(await Cache.KeyRenameAsync(key, newKey, token: TestToken));

        Assert.Null(await Cache.GetAsync<string>(key, token: TestToken));
        Assert.Equal("v", await Cache.GetAsync<string>(newKey, token: TestToken));
    }

    [Fact]
    public async Task KeyRenameAsync_WithNotExists_DoesNotOverwrite()
    {
        var (key, newKey) = (UniqueKey, UniqueKey);
        await Cache.SetAsync(key, "old", token: TestToken);
        await Cache.SetAsync(newKey, "new", token: TestToken);

        Assert.False(await Cache.KeyRenameAsync(key, newKey, Condition.NotExists, token: TestToken));
        Assert.Equal("new", await Cache.GetAsync<string>(newKey, token: TestToken));
    }

    [Fact]
    public async Task KeyPersistAsync_RemovesTtl()
    {
        var key = UniqueKey;
        await Cache.SetAsync(key, "v", redisExpiry: TimeSpan.FromMinutes(5), token: TestToken);

        Assert.True(await Cache.KeyPersistAsync(key, token: TestToken));
        Assert.Null(await Cache.GetExpirationAsync(key, token: TestToken));
    }

    [Fact]
    public async Task HyperLogLog_CountsUniqueValues()
    {
        var key = UniqueKey;
        Assert.True(await Cache.HyperLogLogAddAsync(key, ["a", "b", "c"], token: TestToken));
        await Cache.HyperLogLogAddAsync(key, ["a", "b"], token: TestToken);

        Assert.Equal(3, await Cache.HyperLogLogLengthAsync(key, token: TestToken));
    }

    [Fact]
    public async Task BitOperations_SetGetAndCount()
    {
        var key = UniqueKey;
        Assert.False(await Cache.StringSetBitAsync(key, 3, true, token: TestToken));
        await Cache.StringSetBitAsync(key, 10, true, token: TestToken);

        Assert.True(await Cache.StringGetBitAsync(key, 3, token: TestToken));
        Assert.False(await Cache.StringGetBitAsync(key, 4, token: TestToken));
        Assert.Equal(2, await Cache.StringBitCountAsync(key, token: TestToken));
    }

    [Fact]
    public async Task ServerInfoAsync_ReturnsText()
    {
        Assert.False(string.IsNullOrWhiteSpace(await Cache.ServerInfoAsync(token: TestToken)));
    }

    [Fact]
    public async Task ClientListAsync_ContainsThisConnection()
    {
        Assert.NotEmpty(await Cache.ClientListAsync(token: TestToken));
    }
}
