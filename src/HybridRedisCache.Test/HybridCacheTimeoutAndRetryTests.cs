using System.Diagnostics;
using Xunit;

namespace HybridRedisCache.Test;

public class HybridCacheTimeoutAndRetryTests
{
    private readonly HybridCachingOptions _cachingOptions = new()
    {
        InstancesSharedName = "my-test-app",
        RedisConnectString = "localhost:9379", //Invalid Redis Connection (On Purpose)
        ThrowIfDistributedCacheError = true,
        AbortOnConnectFail = true,
        ConnectRetry = 4,
        FlushLocalCacheOnBusReconnection = false,
        ConnectionTimeout = 1000,
        SyncTimeout = 1000,
        AsyncTimeout = 1000
    };

    [Fact]
    public void Should_Retry_On_ConnectionFailure()
    {
        var stopWatch = Stopwatch.StartNew();

        try
        {
            var cache = new HybridCache(_cachingOptions, new LoggerFactoryMock());
        }
        catch
        {
            stopWatch.Stop();
        }

        Assert.True(stopWatch.ElapsedMilliseconds >= 2000, $"Actual value {stopWatch.ElapsedMilliseconds}");
    }
}