using System;
using System.Threading.Tasks;
using Xunit;

namespace HybridRedisCache.Test;

/// <summary>
/// Pub/sub and bus behaviour that needs real Redis (Garnet's PUBLISH is unreliable, see
/// <see cref="InProcessRedisFixture"/>).
/// </summary>
public class PubSubCoverageTests(ITestOutputHelper output) : BaseCacheTest(output)
{
    private static async Task WaitUntil(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!condition() && DateTime.UtcNow < deadline)
            await Task.Delay(50, TestToken);
    }

    [Fact]
    public async Task SubscribeAsync_ReceivesSyncPublish_AndUnsubscribeAsyncStopsDelivery()
    {
        var channel = "chan-" + UniqueKey;
        var received = 0;
        await Cache.SubscribeAsync(channel, (_, _) => received++, TestToken);
        await Task.Delay(300, TestToken); // SubscribeAsync is fire-and-forget

        Cache.Publish(channel, "k", "v");
        await WaitUntil(() => received > 0);
        Assert.Equal(1, received);

        await Cache.UnsubscribeAsync(channel, TestToken);
        await Task.Delay(300, TestToken);
        Cache.Publish(channel, "k", "v");
        await Task.Delay(300, TestToken);
        Assert.Equal(1, received);
    }

    [Fact]
    public async Task FlushLocalCaches_ClearsOtherInstancesLocalCache()
    {
        var options = Options;
        await using var other = new HybridCache(options, LoggerFactory);
        var key = UniqueKey;
        Assert.True(await other.SetAsync(key, "v", redisCacheEnable: false, token: TestToken));
        Assert.True(await other.ExistsAsync(key, token: TestToken));

        await Cache.FlushLocalCachesAsync(TestToken);
        await WaitUntil(() => !other.Exists(key));
        Assert.False(other.Exists(key));
    }
}
