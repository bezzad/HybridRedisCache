using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Garnet;
using Xunit;

namespace HybridRedisCache.Test;

/// <summary>
/// Runs a Redis-compatible server (Microsoft Garnet) inside the test process, so the tests that use
/// it need no Docker daemon and no <c>Testcontainers</c> pull. One server is shared by every test in
/// the <see cref="InProcessRedisCollection"/> collection.
/// </summary>
/// <remarks>
/// <para>
/// Garnet speaks RESP and covers most of what this library issues (strings, single-field hashes,
/// locks, SCAN, Lua, INCR/DECR, FLUSHDB, DBSIZE, TIME, ECHO). It does not cover everything, so the
/// following stay on the container-backed <see cref="BaseCacheTest"/>:
/// </para>
/// <list type="bullet">
/// <item><description>
/// <b>Key-space notifications.</b> Garnet rejects <c>CONFIG SET notify-keyspace-events</c>, so
/// cross-instance local cache invalidation, <c>OnRedisBusMessage</c>, and the
/// <see cref="HybridCache.LockKeyAsync"/> release signal never fire here.
/// </description></item>
/// <item><description>
/// <b>Pub/sub.</b> Garnet 1.1.10 can throw <c>SynchronizationLockException</c> inside its own
/// network sender while handling <c>PUBLISH</c> and drop the connection, which makes anything built
/// on the bus (such as <see cref="HybridCache.FlushLocalCachesAsync"/>) flaky here.
/// </description></item>
/// <item><description>
/// <b>Redis 8 hash commands.</b> <c>HSETEX</c> (multi-field <c>HashSetAsync</c>) and <c>HGETDEL</c>
/// (<c>HashFieldGetAndDeleteAsync</c>) return "unknown command".
/// </description></item>
/// </list>
/// <para>Use this fixture for everything else.</para>
/// </remarks>
public sealed class InProcessRedisFixture : IAsyncLifetime
{
    private GarnetServer _server;

    public string ConnectionString { get; private set; }

    public Task InitializeAsync()
    {
        var port = GetFreeTcpPort();

        _server = new GarnetServer(
        [
            "--port", port.ToString(),
            "--bind", "127.0.0.1",
            "--lua", // RemoveWithPatternOnRedisAsync evaluates a Lua script
            "--index", "64m",
            "--memory", "128m"
        ]);

        _server.Start();
        ConnectionString = $"127.0.0.1:{port}";
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _server?.Dispose();
        return Task.CompletedTask;
    }

    private static int GetFreeTcpPort()
    {
        // Bind to port 0 and let the OS pick a free port, then release it for Garnet to claim.
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}

[CollectionDefinition(Name)]
public sealed class InProcessRedisCollection : ICollectionFixture<InProcessRedisFixture>
{
    public const string Name = "InProcessRedis";
}
