using System.Threading.Tasks;
using StackExchange.Redis;
using Xunit;

namespace HybridRedisCache.Test;

/// <summary>
/// <see cref="ObjectHelper.PingAsync(IDatabase,int)"/> decides whether the reconnect loop tears the
/// connection down and rebuilds it, so a false negative is expensive.
/// </summary>
public class PingRetryTests(InProcessRedisFixture fixture, ITestOutputHelper output)
    : InProcessCacheTest(fixture, output)
{
    [Theory]
    [InlineData(0)] // ConnectRetry = 0 used to skip the loop entirely and report the server as down
    [InlineData(-1)]
    [InlineData(1)]
    [InlineData(3)]
    public async Task PingAsync_ReportsALiveServerAsAlive_ForAnyRetryCount(int retryCount)
    {
        Assert.True(await Cache.RedisDb.PingAsync(retryCount));
    }

    [Fact]
    public async Task PingAsync_ReportsFalse_WhenThereIsNoDatabase()
    {
        Assert.False(await ((IDatabase)null).PingAsync(3));
    }
}
