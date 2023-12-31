using System.Diagnostics;
using Xunit;

namespace HybridRedisCache.Test;

public class HybridCacheTimeoutAndRetryTests
{
    private HybridCachingOptions GetOption()
    {
        return new HybridCachingOptions()
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
    }

    [Fact]
    public void Should_Retry_On_ConnectionFailure()
    {
        // arrange 
        var delayPerRetry = 1000;
        var retryCount = 3;
        var opt = GetOption();
        opt.ConnectionTimeout = delayPerRetry;
        opt.ConnectRetry = retryCount;
        
        // act
        var stopWatch = Stopwatch.StartNew();

        try
        {
            var cache = new HybridCache(opt, new LoggerFactoryMock());
        }
        catch
        {
            stopWatch.Stop();
        }

        // assert
        Assert.True(stopWatch.ElapsedMilliseconds >= delayPerRetry * retryCount, $"Actual value {stopWatch.ElapsedMilliseconds}");
    }
}