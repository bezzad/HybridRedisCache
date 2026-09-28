using System;
using System.Linq;
using System.Threading.Tasks;
using StackExchange.Redis;
using Xunit;

namespace HybridRedisCache.Test;

/// <summary>
/// <see cref="InvalidationMode.ClientTracking"/> needs real Redis 6+; Garnet has no CLIENT TRACKING.
/// </summary>
public class ClientTrackingTests(ITestOutputHelper output) : BaseCacheTest(output)
{
    private const string SharedName = "tracking-tests";

    private HybridCache CreateTrackingCache(Action<HybridCachingOptions> configure = null)
    {
        var options = Options;
        options.InstancesSharedName = SharedName;
        options.InvalidationMode = InvalidationMode.ClientTracking;
        configure?.Invoke(options);
        return new HybridCache(options, LoggerFactory);
    }

    private async Task<ConnectionMultiplexer> ConnectRawAsync() =>
        await ConnectionMultiplexer.ConnectAsync(Container.GetConnectionString() + ",allowAdmin=true");

    private static async Task<bool> WaitUntil(Func<Task<bool>> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (DateTime.UtcNow < deadline)
        {
            if (await condition()) return true;
            await Task.Delay(100, TestToken);
        }

        return await condition();
    }

    [Fact]
    public void DefaultOptions_UseKeySpaceMode()
    {
        Assert.Equal(InvalidationMode.KeySpace, new HybridCachingOptions().InvalidationMode);
    }

    [Fact]
    public async Task Startup_DoesNotCallConfigSet()
    {
        await using var raw = await ConnectRawAsync();
        var server = raw.GetServers()[0];
        await server.ConfigSetAsync("notify-keyspace-events", "");

        await using var cache = CreateTrackingCache();

        var value = (await server.ConfigGetAsync("notify-keyspace-events")).Single().Value;
        Assert.Equal("", value);
    }

    [Fact]
    public async Task Startup_WhenTrackingIsRefused_LogsAndStillWorks()
    {
        // Without AllowAdmin SE.Redis refuses the CLIENT commands, exactly like a server that rejects them.
        await using var cache = CreateTrackingCache(o => o.AllowAdmin = false);
        var key = UniqueKey;
        Assert.True(await cache.SetAsync(key, "v", token: TestToken));
        Assert.Equal("v", await cache.GetAsync<string>(key, token: TestToken));
    }

    [Fact]
    public async Task RemoteChanges_FromAnotherInstanceAndFromRedisCli_InvalidateLocalCache()
    {
        await using var a = CreateTrackingCache();
        await using var b = CreateTrackingCache();
        await using var raw = await ConnectRawAsync();
        var key = UniqueKey;

        Assert.True(await a.SetAsync(key, "v1", token: TestToken));
        Assert.True(await b.SetAsync(key, "v2", token: TestToken));
        Assert.True(await WaitUntil(async () => await a.GetAsync<string>(key, token: TestToken) == "v2"));

        await raw.GetDatabase().KeyDeleteAsync(SharedName + ":" + key);
        Assert.True(await WaitUntil(async () => await a.GetAsync<string>(key, token: TestToken) == null));
    }

    [Fact]
    public async Task OwnWrite_IsServedFromLocalCache()
    {
        await using var a = CreateTrackingCache();
        await using var raw = await ConnectRawAsync();
        var key = UniqueKey;

        Assert.True(await a.SetAsync(key, "mine", token: TestToken));
        await Task.Delay(500, TestToken); // any self-invalidation would have arrived by now

        // Stop tracking so a direct Redis write cannot evict it, then change Redis behind A's back:
        // A still answering "mine" proves the value came from its local cache.
        await a.RedisDb.ExecuteAsync("CLIENT", "TRACKING", "OFF");
        await raw.GetDatabase().StringSetAsync(SharedName + ":" + key, "changed-in-redis");
        Assert.Equal("mine", await a.GetAsync<string>(key, token: TestToken));
    }

    [Fact]
    public async Task FlushAll_ClearsLocalCache()
    {
        await using var a = CreateTrackingCache();
        await using var raw = await ConnectRawAsync();
        var key = UniqueKey;
        Assert.True(await a.SetAsync(key, "v", redisCacheEnable: false, token: TestToken));

        await raw.GetServers()[0].FlushAllDatabasesAsync();
        Assert.True(await WaitUntil(async () => await a.GetAsync<string>(key, token: TestToken) == null));
    }

    [Fact]
    public async Task Reconnect_ClearsLocalCache_AndReEnablesTracking()
    {
        await using var a = CreateTrackingCache();
        await using var raw = await ConnectRawAsync();
        var localOnly = UniqueKey;
        var shared = UniqueKey;
        Assert.True(await a.SetAsync(localOnly, "local", redisCacheEnable: false, token: TestToken));

        // Kill both of A's connections (interactive and subscriber).
        var clients = ((string)await raw.GetDatabase().ExecuteAsync("CLIENT", "LIST")).Replace("txt:", "");
        foreach (var id in clients.Split('\n').Where(l => l.Contains($" name={SharedName}:", StringComparison.Ordinal))
                     .Select(l => long.Parse(l.Split(' ')[0][3..])))
            await raw.GetDatabase().ExecuteAsync("CLIENT", "KILL", "ID", id);

        Assert.True(await WaitUntil(async () => await a.GetAsync<string>(localOnly, token: TestToken) == null));

        // Tracking follows the new subscriber: a remote write still invalidates A.
        await raw.GetDatabase().StringSetAsync(SharedName + ":" + shared, "old");
        Assert.True(await WaitUntil(async () =>
        {
            await raw.GetDatabase().StringSetAsync(SharedName + ":" + shared, "old");
            return await a.GetAsync<string>(shared, token: TestToken) == "old";
        }));
        await raw.GetDatabase().StringSetAsync(SharedName + ":" + shared, "new");
        Assert.True(await WaitUntil(async () => await a.GetAsync<string>(shared, token: TestToken) == "new"));
    }

    [Fact]
    public async Task LockWaiter_WakesWhenAnotherInstanceReleases()
    {
        await using var a = CreateTrackingCache();
        await using var b = CreateTrackingCache();
        var key = UniqueKey;

        var held = await a.LockKeyAsync(key, TimeSpan.FromMinutes(1), cancellationToken: TestToken);
        var waiting = b.LockKeyAsync(key, TimeSpan.FromMinutes(1), cancellationToken: TestToken);
        await Task.Delay(300, TestToken);
        Assert.False(waiting.IsCompleted);

        await held.ReleaseAsync();
        var acquired = await waiting.WaitAsync(TimeSpan.FromSeconds(5), TestToken);
        Assert.True(await acquired.ReleaseAsync());
    }
}
