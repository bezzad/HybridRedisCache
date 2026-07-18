using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace HybridRedisCache.Test;

/// <summary>
/// Base class for tests that run against the shared in-process Garnet server.
/// See <see cref="InProcessRedisFixture"/> for the behaviours this server does not support.
/// </summary>
[Collection(InProcessRedisCollection.Name)]
public abstract class InProcessCacheTest : IAsyncDisposable
{
    private HybridCache _cache;
    private readonly InProcessRedisFixture _fixture;

    protected readonly ILoggerFactory LoggerFactory;
    protected static string UniqueKey => Guid.NewGuid().ToString("N");

    protected InProcessCacheTest(InProcessRedisFixture fixture, ITestOutputHelper testOutputHelper)
    {
        _fixture = fixture;
        LoggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(builder =>
            builder.AddProvider(new TestOutputLoggerProvider(testOutputHelper)));
    }

    protected HybridCachingOptions Options => new()
    {
        // A per-instance shared name keeps keys from different test classes in separate namespaces,
        // so the tests stay independent while sharing one server.
        InstancesSharedName = "inproc-" + UniqueKey,
        RedisConnectionString = _fixture.ConnectionString,
        ThrowIfDistributedCacheError = true,
        AbortOnConnectFail = false,
        ConnectRetry = 3,
        AllowAdmin = true,
        EnableTracing = true,
        EnableLogging = true,
    };

    protected HybridCache Cache => _cache ??= new HybridCache(Options, LoggerFactory);

    /// <summary>Creates an additional independent cache instance over the same server.</summary>
    protected HybridCache CreateCache(Action<HybridCachingOptions> configure = null)
    {
        var options = Options;
        configure?.Invoke(options);
        return new HybridCache(options, LoggerFactory);
    }

    public async ValueTask DisposeAsync()
    {
        if (_cache != null)
            await _cache.DisposeAsync();

        LoggerFactory?.Dispose();
        GC.SuppressFinalize(this);
    }
}
