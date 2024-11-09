using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace HybridRedisCache.Test;

[Collection("Sequential")] // run tests in order
public abstract class BaseCacheTest : IDisposable
{
    private HybridCache _cache;
    private static ILoggerFactory _loggerFactory;
    protected readonly ITestOutputHelper TestOutputHelper;
    protected static string UniqueKey => Guid.NewGuid().ToString("N");
    protected readonly HybridCachingOptions Options = new()
    {
        InstancesSharedName = "xunit-tests",
        RedisConnectionString = "localhost:6379,allowAdmin=true", // STAGE "172.23.44.61:6379", 
        ThrowIfDistributedCacheError = true,
        AbortOnConnectFail = false,
        ConnectRetry = 3,
        FlushLocalCacheOnBusReconnection = false,
        AllowAdmin = true,
        SyncTimeout = 500000,
        AsyncTimeout = 500000,
        KeepAlive = 6000,
        ConnectionTimeout = 5000,
        ThreadPoolSocketManagerEnable = true,
        EnableTracing = true,
        EnableLogging = true,
        SupportOldInvalidateBus = false
    };

    // Lazy Cache: options change inner methods and after that create Cache with first call
    protected HybridCache Cache => _cache ??= new HybridCache(Options, _loggerFactory);

    protected BaseCacheTest(ITestOutputHelper testOutputHelper)
    {
        TestOutputHelper = testOutputHelper;
        // Create an ILoggerFactory that logs to the ITestOutputHelper
        _loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(builder =>
        {
            builder.AddProvider(new TestOutputLoggerProvider(testOutputHelper));
        });
    }
    
    protected async Task AssertKeysAreRemoved(Dictionary<string, string> keyValues)
    {
        foreach (var keyValue in keyValues)
        {
            var isExist = await Cache.ExistsAsync(keyValue.Key);
            Assert.False(isExist, $"The key {keyValue.Key} is still exist!");
        }
    }
    
    [SuppressMessage("ReSharper", "StringLiteralTypo")]
    protected async Task<Dictionary<string, string>> PrepareDummyKeys(int insertCount, bool localCacheEnable = true,
        string keyPrefix = "", bool generateNoiseKeys = false)
    {
        keyPrefix ??= string.Empty;
        var hybridOptions = new HybridCacheEntry
        {
            RedisExpiry = TimeSpan.FromMinutes(55),
            FireAndForget = false,
            LocalCacheEnable = localCacheEnable,
            RedisCacheEnable = true,
            KeepTtl = false,
            Flags = Flags.PreferMaster,
            When = Condition.Always
        };

        var keyFormats = new[]
        {
            "TestRemovewithPattern#{id}",
            "testRemovewithPattern#{id}",
            "TestremovewithPattern#{id}",
            "testremovewithPattern#{id}",
            "TestRemoveWithPattern#{id}",
            "testRemoveWithpattern#{id}",
            "TestremoveWithpattern#{id}",
            "testremoveWithpattern#{id}",
            "TestRemoveWithpattern#{id}",
        };

        TestOutputHelper.WriteLine($"Generating dummy keys...");

        var keyValues = Enumerable.Range(0, insertCount)
            .Select(_ => Random.Shared.GetItems(keyFormats, 1).First()
                .Replace("{id}", Guid.NewGuid().ToString()))
            .ToDictionary(key => keyPrefix + key, key => key);

        TestOutputHelper.WriteLine($"Generating dummy noise keys...");

        if (generateNoiseKeys)
        {
            var noiseKeys = Enumerable.Range(0, insertCount)
                .Select(_ => Guid.NewGuid().ToString("N"))
                .ToDictionary(key => keyPrefix + key, key => key);

            await Cache.SetAllAsync(noiseKeys, hybridOptions);
            TestOutputHelper.WriteLine($"{noiseKeys.Count} keys added to redis as noise keys");
        }

        TestOutputHelper.WriteLine("Adding dummy keys...");
        await Cache.SetAllAsync(keyValues, hybridOptions);
        TestOutputHelper.WriteLine($"{keyValues.Count} keys added to redis as pattern searchable keys");

        return keyValues;
    }

    public void Dispose()
    {
        Cache.Dispose();
    }
}