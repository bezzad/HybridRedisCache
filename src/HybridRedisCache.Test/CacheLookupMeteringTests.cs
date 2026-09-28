using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Xunit;

namespace HybridRedisCache.Test;

/// <summary>
/// The lookup counter lives on a process-wide static meter, so a listener sees
/// every cache instance's reads. Two things keep the counts honest: the collection
/// does not run in parallel with others, and every measurement is matched against
/// the "cache" tag of the cache instances this test itself created.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class CacheLookupMeteringCollection : ICollectionFixture<InProcessRedisFixture>
{
    public const string Name = "CacheLookupMetering";
}

[Collection(CacheLookupMeteringCollection.Name)]
public sealed class CacheLookupMeteringTests : IAsyncDisposable
{
    private readonly InProcessRedisFixture _fixture;
    private readonly ILoggerFactory _loggerFactory;
    private readonly MeterListener _listener = new();
    private readonly ConcurrentDictionary<(string Layer, string Result), long> _lookups = new();
    private readonly ConcurrentDictionary<string, byte> _ownCacheNames = new();
    private readonly List<HybridCache> _caches = [];

    private static string UniqueKey => Guid.NewGuid().ToString("N");

    /// <summary>The token xunit cancels when the run is cancelled or a Timeout elapses.</summary>
    private static CancellationToken TestToken => TestContext.Current.CancellationToken;

    public CacheLookupMeteringTests(InProcessRedisFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _loggerFactory = LoggerFactory.Create(builder => builder.AddProvider(new TestOutputLoggerProvider(output)));

        _listener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name == KeyMeter.MeterName && instrument.Name == KeyMeter.LookupsMetricName)
                listener.EnableMeasurementEvents(instrument);
        };
        _listener.SetMeasurementEventCallback<long>((_, value, tags, _) =>
        {
            string layer = null, result = null, cache = null;
            foreach (var tag in tags)
            {
                if (tag.Key == "layer") layer = tag.Value as string;
                if (tag.Key == "result") result = tag.Value as string;
                if (tag.Key == "cache") cache = tag.Value as string;
            }

            // Another test's cache publishes on the same instrument; ignore it.
            if (cache is null || !_ownCacheNames.ContainsKey(cache))
                return;

            _lookups.AddOrUpdate((layer, result), value, (_, total) => total + value);
        });
        _listener.Start();
    }

    private HybridCache CreateCache(bool enableMeterData = true)
    {
        var sharedName = "lookup-metering-" + UniqueKey;
        _ownCacheNames[sharedName] = 0;
        var cache = new HybridCache(new HybridCachingOptions
        {
            InstancesSharedName = sharedName,
            RedisConnectionString = _fixture.ConnectionString,
            ThrowIfDistributedCacheError = true,
            AbortOnConnectFail = false,
            ConnectRetry = 3,
            EnableMeterData = enableMeterData,
        }, _loggerFactory);
        _caches.Add(cache);

        return cache;
    }

    private long Count(string layer, string result) => _lookups.GetValueOrDefault((layer, result));

    private void AssertLookups(long localHit = 0, long localMiss = 0, long redisHit = 0, long redisMiss = 0)
    {
        Assert.Equal(localHit, Count("local", "hit"));
        Assert.Equal(localMiss, Count("local", "miss"));
        Assert.Equal(redisHit, Count("redis", "hit"));
        Assert.Equal(redisMiss, Count("redis", "miss"));
    }

    [Fact]
    public async Task LocalHit_IsCountedOnce_AndRedisIsNotAsked()
    {
        var cache = CreateCache();
        var key = UniqueKey;
        await cache.SetAsync(key, "value", TimeSpan.FromMinutes(1), token: TestToken);

        Assert.Equal("value", await cache.GetAsync<string>(key, token: TestToken));

        AssertLookups(localHit: 1);
    }

    [Fact]
    public async Task LocalMiss_FallsThroughToRedisHit_ThenLocalHitsTheCopy()
    {
        var cache = CreateCache();
        var key = UniqueKey;
        await cache.SetAsync(key, "value", redisExpiry: TimeSpan.FromMinutes(1), localCacheEnable: false, token: TestToken);

        Assert.Equal("value", await cache.GetAsync<string>(key, token: TestToken));
        AssertLookups(localMiss: 1, redisHit: 1);

        // The Redis hit wrote the value back to local memory.
        Assert.Equal("value", await cache.GetAsync<string>(key, token: TestToken));
        AssertLookups(localHit: 1, localMiss: 1, redisHit: 1);
    }

    [Fact]
    public async Task MissingKey_IsAMissOnBothLayers()
    {
        var cache = CreateCache();

        var (success, _) = await cache.TryGetValueAsync<string>(UniqueKey, token: TestToken);

        Assert.False(success);
        AssertLookups(localMiss: 1, redisMiss: 1);
    }

    [Fact]
    public void SyncRead_IsCountedLikeTheAsyncOne()
    {
        var cache = CreateCache();
        var key = UniqueKey;
        cache.Set(key, "value", redisExpiry: TimeSpan.FromMinutes(1), localCacheEnable: false);

        Assert.True(cache.TryGetValue(key, out string value));
        Assert.Equal("value", value);
        Assert.False(cache.TryGetValue(UniqueKey, out string _));

        AssertLookups(localMiss: 2, redisHit: 1, redisMiss: 1);
    }

    [Fact]
    public async Task GetWithDataRetriever_CountsTheLookupOnly_NotTheRetrieverRun()
    {
        var cache = CreateCache();

        var value = await cache.GetAsync(UniqueKey, _ => Task.FromResult("fetched"), TimeSpan.FromMinutes(1), token: TestToken);

        Assert.Equal("fetched", value);
        AssertLookups(localMiss: 1, redisMiss: 1);
    }

    [Fact]
    public async Task NothingIsCounted_WhenMeterDataIsDisabled()
    {
        var cache = CreateCache(enableMeterData: false);
        var key = UniqueKey;
        await cache.SetAsync(key, "value", TimeSpan.FromMinutes(1), token: TestToken);

        await cache.GetAsync<string>(key, token: TestToken);
        await cache.GetAsync<string>(UniqueKey, token: TestToken);

        AssertLookups();
    }

    public async ValueTask DisposeAsync()
    {
        _listener.Dispose();
        foreach (var cache in _caches)
            await cache.DisposeAsync();

        _loggerFactory.Dispose();
    }
}
