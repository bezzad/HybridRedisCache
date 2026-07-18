using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace HybridRedisCache.Test;

/// <summary>
/// Covers the <see cref="CancellationToken"/> parameters on the async API.
/// </summary>
/// <remarks>
/// StackExchange.Redis takes no token on its command APIs, so cancellation bounds how long the caller
/// awaits rather than aborting the Redis command. These tests assert the caller-visible contract:
/// an already-cancelled token faults the call, and a live token lets it complete normally.
/// </remarks>
public class CancellationTokenTests(InProcessRedisFixture fixture, ITestOutputHelper output)
    : InProcessCacheTest(fixture, output)
{
    private static CancellationToken Cancelled
    {
        get
        {
            var cts = new CancellationTokenSource();
            cts.Cancel();
            return cts.Token;
        }
    }

    [Fact]
    public async Task SetAsync_WithCancelledToken_Throws()
    {
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => Cache.SetAsync(UniqueKey, "v", token: Cancelled));
    }

    [Fact]
    public async Task GetAsync_WithCancelledToken_Throws()
    {
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => Cache.GetAsync<string>(UniqueKey, token: Cancelled));
    }

    [Fact]
    public async Task TryGetValueAsync_WithCancelledToken_Throws()
    {
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await Cache.TryGetValueAsync<string>(UniqueKey, token: Cancelled));
    }

    [Fact]
    public async Task ExistsAsync_WithCancelledToken_Throws()
    {
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => Cache.ExistsAsync(UniqueKey, token: Cancelled));
    }

    [Fact]
    public async Task RemoveAsync_WithCancelledToken_Throws()
    {
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => Cache.RemoveAsync(UniqueKey, token: Cancelled));
    }

    [Fact]
    public async Task SetAllAsync_WithCancelledToken_Throws()
    {
        var data = new Dictionary<string, string> { [UniqueKey] = "v" };
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => Cache.SetAllAsync(data, token: Cancelled));
    }

    [Fact]
    public async Task HashSetAsync_WithCancelledToken_Throws()
    {
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => Cache.HashSetAsync(UniqueKey, "field", "value", token: Cancelled));
    }

    [Fact]
    public async Task HashGetAsync_WithCancelledToken_Throws()
    {
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => Cache.HashGetAsync(UniqueKey, "field", token: Cancelled));
    }

    [Fact]
    public async Task ValueIncrementAsync_WithCancelledToken_Throws()
    {
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => Cache.ValueIncrementAsync(UniqueKey, 1L, token: Cancelled));
    }

    [Fact]
    public async Task GetExpirationAsync_WithCancelledToken_Throws()
    {
        // GetExpirationAsync swallows Redis errors and returns null; cancellation must still surface.
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => Cache.GetExpirationAsync(UniqueKey, Cancelled));
    }

    [Fact]
    public async Task RemoveWithPatternOnRedisAsync_WithCancelledToken_Throws()
    {
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await Cache.RemoveWithPatternOnRedisAsync("x*", token: Cancelled));
    }

    [Fact]
    public async Task KeysAsync_WithCancelledToken_Throws()
    {
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var _ in Cache.KeysAsync("*", token: Cancelled)) { }
        });
    }

    [Fact]
    public async Task SetAllAsync_CancelledMidWay_StopsWriting()
    {
        var cts = new CancellationTokenSource();
        var keys = new Dictionary<string, string>();
        for (var i = 0; i < 50; i++)
            keys["cancel-" + UniqueKey] = "v";

        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => Cache.SetAllAsync(keys, token: cts.Token));
    }

    [Fact]
    public async Task AsyncMethods_WithLiveToken_CompleteNormally()
    {
        using var cts = new CancellationTokenSource();
        var key = UniqueKey;

        Assert.True(await Cache.SetAsync(key, "alive", token: cts.Token));
        Assert.Equal("alive", await Cache.GetAsync<string>(key, token: cts.Token));
        Assert.True(await Cache.ExistsAsync(key, token: cts.Token));
        Assert.True(await Cache.RemoveAsync(key, token: cts.Token));
    }

    [Fact]
    public async Task AsyncMethods_WithDefaultToken_CompleteNormally()
    {
        // default(CancellationToken) cannot be cancelled, so the fast path skips WaitAsync entirely.
        var key = UniqueKey;
        Assert.True(await Cache.SetAsync(key, 7));
        Assert.Equal(7, await Cache.GetAsync<int>(key));
    }
}
