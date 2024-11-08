using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace HybridRedisCache.Test;

public class PerformanceTest(ITestOutputHelper testOutputHelper) : BaseCacheTest(testOutputHelper)
{
    private const string KeyPattern = "[Tt]est[Rr]emove[Ww]ith[Pp]attern#*";

    [Fact]
    public async Task Test_CRUD_Performance()
    {
        var key = UniqueKey;
        var value = UniqueKey;
        var sw = new Stopwatch();
        var count = 10000;

        // act
        sw.Start();
        for (var i = 0; i < count; i++)
        {
            await Cache.SetAsync(key, value);
        }

        sw.Stop();
        TestOutputHelper.WriteLine($"SetAsync: {count} times in {sw.ElapsedMilliseconds} ms");
        sw.Reset();
        sw.Start();
        for (var i = 0; i < count; i++)
        {
            await Cache.GetAsync<string>(key);
        }

        sw.Stop();
        TestOutputHelper.WriteLine($"GetAsync: {count} times in {sw.ElapsedMilliseconds} ms");
        sw.Reset();
        sw.Start();
        for (var i = 0; i < count; i++)
        {
            await Cache.RemoveAsync(key);
        }

        sw.Stop();
        TestOutputHelper.WriteLine($"RemoveAsync: {count} times in {sw.ElapsedMilliseconds} ms");
    }

    [Fact]
    public async Task Test_Redis_Invalidate_Messages()
    {
        // Arrange
        // docker exec -it redis redis-cli
        // redis-cli set "xunit-tests:TEST_KEY" "\"test value from redis\"" EX 100
        // redis-cli get "xunit-tests:TEST_KEY" 
        var key = "TEST_KEY"; // real key 'xunit-tests:TEST_KEY'
        var value1 = "test value from main cache";
        var value2 = "test value from second cache";
        var counter = 100;
        var hybridOpt = new HybridCacheEntry()
        {
            LocalCacheEnable = false,
            RedisExpiry = TimeSpan.FromMinutes(100)
        };

        // Act
        var inserted = await Cache.SetAsync(key, value1, hybridOpt);
        await Task.Delay(50);
        var fetched = Cache.TryGetValue(key, out string _);
        await Task.Delay(50);
        var insertedSecondTime = await Cache.SetAsync(key, value2, hybridOpt);
        await Task.Delay(50);
        while (counter-- > 0)
        {
            fetched &= Cache.TryGetValue(key, out string value);
            // testOutputHelper.WriteLine($"key[{key}]: " + value);
            await Task.Delay(500);
        }

        // Assert
        Assert.True(inserted);
        Assert.True(fetched);
        Assert.True(insertedSecondTime);
    }


    [Theory]
    [InlineData(1000, 100)]
    [InlineData(10_000, 100)]
    [InlineData(10_000, 1000)]
    [InlineData(10_000, 10_000)]
    [InlineData(10_000, 500)]
    [InlineData(10_000, 5000)]
    [InlineData(10_000, 15_000)]
    // [InlineData(100_000, 1000)]
    [SuppressMessage("ReSharper", "StringLiteralTypo")]
    public async Task TestRemoveWithPatternKeysPerformance(int insertCount, int batchRemovePackSize)
    {
        // Arrange
        await Cache.ClearAllAsync(); // Clear local cache first
        var keyValues =
            await PrepareDummyKeys(insertCount, keyPrefix: "", localCacheEnable: false, generateNoiseKeys: true);

        // Action
        var sw = Stopwatch.StartNew();
        var removedKeys = await Cache.RemoveWithPatternAsync(KeyPattern,
            flags: Flags.FireAndForget | Flags.PreferReplica,
            batchRemovePackSize: batchRemovePackSize);
        sw.Stop();
        TestOutputHelper.WriteLine($"Remove with pattern operation duration: {sw.ElapsedMilliseconds}ms");

        // Assert
        Assert.True(insertCount <= removedKeys);
        await AssertKeysAreRemoved(keyValues);
        Assert.True(sw.ElapsedMilliseconds < (insertCount / 100) + (insertCount / batchRemovePackSize * 10) + 100,
            $"Remove keys with pattern duration is {sw.ElapsedMilliseconds}ms");
    }

    [Theory]
    [InlineData(1000)]
    [SuppressMessage("ReSharper", "StringLiteralTypo")]
    public async Task TestDeleteKeysByPatternOnRedisAsync(int insertCount)
    {
        // Arrange
        await Cache.ClearAllAsync(); // Clear local cache first
        var keyValues =
            await PrepareDummyKeys(insertCount, keyPrefix: "", localCacheEnable: false, generateNoiseKeys: true);

        // Action
        var sw = Stopwatch.StartNew();
        await Cache.RemoveWithPatternOnRedisAsync(KeyPattern, Flags.PreferReplica | Flags.FireAndForget);
        sw.Stop();
        TestOutputHelper.WriteLine($"### Remove with pattern operation duration: {sw.ElapsedMilliseconds}ms");

        // Assert
        await AssertKeysAreRemoved(keyValues);
        Assert.True(sw.ElapsedMilliseconds < insertCount / 10,
            $"Remove keys with pattern duration is {sw.ElapsedMilliseconds}ms");
    }
}