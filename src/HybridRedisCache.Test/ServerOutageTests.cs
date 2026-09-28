using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Garnet;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using Xunit;

namespace HybridRedisCache.Test;

/// <summary>
/// Each test owns a private Garnet server and stops it, so the cache's error handling and reconnect
/// loop run against a real outage without disturbing the shared fixture.
/// </summary>
public sealed class ServerOutageTests : IAsyncDisposable
{
    private readonly int _port = GetFreeTcpPort();
    private readonly ILoggerFactory _loggerFactory;
    private GarnetServer _server;

    public ServerOutageTests(ITestOutputHelper output)
    {
        _loggerFactory = LoggerFactory.Create(b => b.AddProvider(new TestOutputLoggerProvider(output)));
        StartServer();
    }

    private static System.Threading.CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string UniqueKey => Guid.NewGuid().ToString("N");

    private void StartServer()
    {
        _server = new GarnetServer(["--port", _port.ToString(), "--bind", "127.0.0.1", "--lua", "--memory", "64m"]);
        _server.Start();
    }

    private void StopServer()
    {
        _server?.Dispose();
        _server = null;
    }

    private HybridCachingOptions CreateOptions() => new()
    {
        InstancesSharedName = "outage-" + UniqueKey,
        RedisConnectionString = $"127.0.0.1:{_port}",
        AbortOnConnectFail = false,
        ThrowIfDistributedCacheError = false,
        ConnectRetry = 1,
        ConnectionTimeout = 500,
        SyncTimeout = 300,
        AsyncTimeout = 300,
        EnableLogging = true,
    };

    [Fact]
    public async Task Operations_WhenServerIsDown_AreSwallowedOrThrownPerOption()
    {
        var options = CreateOptions();
        await using var cache = new HybridCache(options, _loggerFactory);
        var key = UniqueKey;
        StopServer();

        // Swallowed: the call reports a miss or failure instead of throwing.
        Assert.False(cache.Exists(key));
        Assert.False(await cache.ExistsAsync(key, token: Ct));
        Assert.False(cache.Remove(key));
        Assert.False(await cache.RemoveAsync(key, token: Ct));
        Assert.Null(cache.GetExpiration(key));
        Assert.Null(await cache.GetExpirationAsync(key, token: Ct));
        Assert.Null(cache.Get<string>(key));
        Assert.Null(await cache.GetAsync<string>(key, token: Ct));
        Assert.Empty(cache.GetAll<string>([key]));
        Assert.Empty(await cache.GetAllAsync<string>([key], token: Ct));
        Assert.False(await cache.SetAsync(key, "v", token: Ct));
        Assert.False(await cache.SetAllAsync(new Dictionary<string, string> { [key] = "v" }, token: Ct));
        cache.FlushLocalCaches();
        await cache.FlushLocalCachesAsync(Ct);
        Assert.False(await cache.RedisDb.PingAsync(2));

        // The option is read on every call, so flipping it switches the same instance to throwing.
        options.ThrowIfDistributedCacheError = true;
        Assert.ThrowsAny<RedisException>(() => cache.Exists(key));
        await Assert.ThrowsAnyAsync<RedisException>(() => cache.ExistsAsync(key, token: Ct));
        Assert.ThrowsAny<RedisException>(() => cache.Remove(key));
        await Assert.ThrowsAnyAsync<RedisException>(() => cache.RemoveAsync(key, token: Ct));
        Assert.ThrowsAny<RedisException>(() => cache.Get<string>(key));
        Assert.ThrowsAny<RedisException>(() => cache.GetAll<string>([key]));
        await Assert.ThrowsAnyAsync<RedisException>(() => cache.GetAllAsync<string>([key], token: Ct));
        Assert.ThrowsAny<RedisException>(() => cache.Set(key, "v"));
    }

    [Fact]
    public async Task ReconnectLoop_GivesUp_AfterMaxReconfigureAttempts()
    {
        var options = CreateOptions();
        options.ReconfigureOnConnectFail = true;
        options.MaxReconfigureAttempts = 1;
        await using var cache = new HybridCache(options, _loggerFactory);
        StopServer();

        // Let the ConnectionFailed handler run its loop until it gives up.
        await Task.Delay(TimeSpan.FromSeconds(4), TestContext.Current.CancellationToken);
        Assert.False(cache.Exists(UniqueKey));
    }

    [Fact]
    public async Task Reconnect_AfterServerRestart_FlushesLocalCacheAndServesRedis()
    {
        var options = CreateOptions();
        options.FlushLocalCacheOnBusReconnection = true;
        await using var cache = new HybridCache(options, _loggerFactory);
        var key = UniqueKey;
        Assert.True(await cache.SetAsync(key, "v", redisCacheEnable: false, token: Ct));
        Assert.Equal("v", await cache.GetAsync<string>(key, token: Ct));

        StopServer();
        await Task.Delay(500, TestContext.Current.CancellationToken);
        StartServer();

        // The multiplexer reconnects on its own and ConnectionRestored flushes the local cache.
        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (await cache.GetAsync<string>(key, token: Ct) != null && DateTime.UtcNow < deadline)
            await Task.Delay(200, TestContext.Current.CancellationToken);

        Assert.Null(await cache.GetAsync<string>(key, token: Ct));
        Assert.True(await cache.SetAsync(key, "after", token: Ct));
        Assert.Equal("after", await cache.GetAsync<string>(key, localCacheEnable: false, token: Ct));
    }

    [Fact]
    public void Constructor_WithAbortOnConnectFail_ThrowsWhenNoServer()
    {
        StopServer();
        var options = CreateOptions();
        options.AbortOnConnectFail = true;
        Assert.ThrowsAny<RedisConnectionException>(() => new HybridCache(options, _loggerFactory));
    }

    public ValueTask DisposeAsync()
    {
        StopServer();
        _loggerFactory.Dispose();
        return ValueTask.CompletedTask;
    }

    private static int GetFreeTcpPort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
